import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Button, Dialog } from '@vsite/ui';
import { getErrorCode } from '@vsite/shared';
import {
  getGetWebsiteQueryKey,
  getListPagesQueryKey,
  getListPublicationsQueryKey,
  useListPublications,
  useRollbackPublication,
  type PublicationDto,
} from '@vsite/api-sdk';
import { useIsPublishing } from '../../hooks/use-publication-mutations';
import { getErrorMessage } from '../../lib/error-messages';

const vnTime = new Intl.DateTimeFormat('vi-VN', {
  timeZone: 'Asia/Ho_Chi_Minh',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

/** Giờ Việt Nam (G3), không phụ thuộc múi giờ máy. */
export function formatPublishedAt(iso: string): string {
  const date = new Date(iso);
  return Number.isNaN(date.getTime()) ? iso : vnTime.format(date);
}

/**
 * Lịch sử bản xuất bản + rollback (brief WEBSITE-004 §7). Danh sách ≤ 50, không phân trang, đúng một `isCurrent`.
 * Rollback không tự thử lại: 404/409 ⇒ thông điệp + nạp lại danh sách, người dùng tự bấm lại. Dữ liệu ở TanStack Query (#20).
 */
export function PublicationHistory({ shopId }: { shopId: string }) {
  const queryClient = useQueryClient();
  const isPublishing = useIsPublishing();
  const publicationsQuery = useListPublications(shopId);
  const [pending, setPending] = useState<PublicationDto | null>(null);

  const rollback = useRollbackPublication({
    mutation: {
      retry: false,
      onSuccess: () => {
        setPending(null);
      },
      onError: (error) => {
        // Bản đã biến mất: bấm lại chỉ 404 mãi — đóng xác nhận, thông điệp hiện ở mức danh sách.
        if (getErrorCode(error) === 'PUBLICATION_NOT_FOUND') setPending(null);
      },
      onSettled: () => {
        void queryClient.invalidateQueries({ queryKey: getGetWebsiteQueryKey(shopId) });
        void queryClient.invalidateQueries({ queryKey: getListPagesQueryKey(shopId) });
        void queryClient.invalidateQueries({ queryKey: getListPublicationsQueryKey(shopId) });
      },
    },
  });

  if (publicationsQuery.isPending) return <p className="text-muted-foreground">Đang tải lịch sử xuất bản...</p>;

  if (publicationsQuery.isError) {
    return (
      <div className="space-y-3">
        <p role="alert" className="text-destructive">
          {getErrorMessage(getErrorCode(publicationsQuery.error))}
        </p>
        <Button type="button" variant="outline" size="sm" onClick={() => void publicationsQuery.refetch()}>
          Thử lại
        </Button>
      </div>
    );
  }

  const publications = publicationsQuery.data;
  const busy = rollback.isPending;
  const locked = busy || isPublishing; // R6: khoá chéo với publish đang chạy

  return (
    <section className="space-y-3">
      <h2 className="text-base font-semibold text-foreground">Lịch sử xuất bản</h2>

      {!pending && rollback.isError && (
        <p role="alert" className="text-sm text-destructive">
          {getErrorMessage(getErrorCode(rollback.error))}
        </p>
      )}

      {pending && (
        <Dialog
          open
          title="Quay về bản cũ"
          dismissible={!busy}
          onClose={() => {
            if (busy) return;
            rollback.reset();
            setPending(null);
          }}
        >
          <div className="space-y-4">
            <p className="text-sm">{`Quay về bản v${String(pending.version)}? Site công khai sẽ chạy lại nội dung của bản này ngay lập tức (không tạo bản mới).`}</p>
            {rollback.isError && (
              <p role="alert" className="text-sm text-destructive">
                {getErrorMessage(getErrorCode(rollback.error))}
              </p>
            )}
            <div className="flex justify-end gap-2">
              <Button
                type="button"
                variant="outline"
                disabled={busy}
                onClick={() => {
                  rollback.reset();
                  setPending(null);
                }}
              >
                Huỷ
              </Button>
              <Button
                type="button"
                disabled={locked}
                onClick={() => {
                  rollback.mutate({ shopId, publicationId: pending.id });
                }}
              >
                {busy ? 'Đang quay về...' : 'Xác nhận quay về'}
              </Button>
            </div>
          </div>
        </Dialog>
      )}

      {publications.length === 0 ? (
        <p className="rounded-md bg-muted px-4 py-3 text-sm text-muted-foreground">Chưa có bản nào</p>
      ) : (
        <ul aria-label="Lịch sử xuất bản" className="divide-y divide-border rounded-md border border-border">
          {publications.map((p) => (
            <li key={p.id} className="flex items-center justify-between gap-3 px-4 py-3">
              <div>
                <p className="font-medium text-foreground">
                  <span>v{p.version}</span>
                  {p.isCurrent && (
                    <span className="ml-2 rounded-full bg-primary/10 px-2 py-0.5 text-xs text-primary">Đang chạy</span>
                  )}
                </p>
                <p className="text-sm text-muted-foreground">{formatPublishedAt(p.publishedAt)}</p>
                {p.note && <p className="text-sm text-foreground">{p.note}</p>}
              </div>
              {!p.isCurrent && (
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  disabled={locked}
                  onClick={() => {
                    rollback.reset();
                    setPending(p);
                  }}
                >
                  Quay về bản này
                </Button>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
