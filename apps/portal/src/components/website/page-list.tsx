import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Button } from '@vsite/ui';
import { getErrorCode } from '@vsite/shared';
import { getListPagesQueryKey, PageKind, useDeletePage, useListPages, type PageDto } from '@vsite/api-sdk';
import { getErrorMessage } from '../../lib/error-messages';
import { PageFormDialog } from './page-form-dialog';

function kindLabel(page: PageDto): string {
  if (page.isHomePage) return 'Trang chủ';
  if (page.kind === PageKind.System) return 'Trang hệ thống';
  return 'Trang nội dung';
}

/** `slug` rỗng = trang chủ, `null` = NotFound (không có URL riêng). */
function slugLabel(page: PageDto): string {
  if (page.slug === null) return '(không có đường dẫn)';
  return `/${page.slug}`;
}

type FormState = { type: 'create' } | { type: 'edit'; page: PageDto } | null;

/** Danh sách trang (Flow 2): tạo, sửa metadata, xoá. Không phân trang; sắp theo `sortOrder` (chỉ đọc). */
export function PageList({ shopId }: { shopId: string }) {
  const queryClient = useQueryClient();
  const pagesQuery = useListPages(shopId);
  const [form, setForm] = useState<FormState>(null);
  const [pendingDelete, setPendingDelete] = useState<PageDto | null>(null);

  const deleteMutation = useDeletePage({
    mutation: {
      onSuccess: () => {
        setPendingDelete(null);
        void queryClient.invalidateQueries({ queryKey: getListPagesQueryKey(shopId) });
      },
    },
  });

  if (pagesQuery.isPending) return <p className="text-muted-foreground">Đang tải danh sách trang...</p>;

  if (pagesQuery.isError) {
    return (
      <div className="space-y-3">
        <p role="alert" className="text-destructive">
          {getErrorMessage(getErrorCode(pagesQuery.error))}
        </p>
        <Button type="button" variant="outline" size="sm" onClick={() => void pagesQuery.refetch()}>
          Thử lại
        </Button>
      </div>
    );
  }

  const pages = [...pagesQuery.data].sort((a, b) => a.sortOrder - b.sortOrder);
  const hasContentPage = pages.some((p) => p.kind === PageKind.Composable && !p.isHomePage);

  return (
    <div className="space-y-4">
      <div className="flex justify-end">
        <Button
          type="button"
          size="sm"
          onClick={() => {
            setForm({ type: 'create' });
          }}
        >
          Tạo trang
        </Button>
      </div>

      {!hasContentPage && (
        <p className="rounded-md bg-muted px-4 py-3 text-sm text-muted-foreground">
          Chưa có trang nội dung nào ngoài trang chủ. Tạo thêm trang (vd. Dịch vụ, Giới thiệu) để khách xem.
        </p>
      )}

      {pendingDelete && (
        <div role="alertdialog" className="space-y-2 rounded border border-destructive/50 bg-destructive/10 p-3">
          <p className="text-sm">{`Xoá trang "${pendingDelete.title}"? Nội dung bản nháp của trang cũng sẽ mất.`}</p>
          {deleteMutation.isError && (
            <p role="alert" className="text-sm text-destructive">
              {getErrorMessage(getErrorCode(deleteMutation.error))}
            </p>
          )}
          <div className="flex justify-end gap-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => {
                deleteMutation.reset();
                setPendingDelete(null);
              }}
            >
              Huỷ
            </Button>
            <Button
              type="button"
              size="sm"
              disabled={deleteMutation.isPending}
              onClick={() => {
                deleteMutation.mutate({ shopId, pageId: pendingDelete.id });
              }}
            >
              {deleteMutation.isPending ? 'Đang xoá...' : 'Xoá trang'}
            </Button>
          </div>
        </div>
      )}

      <ul aria-label="Danh sách trang" className="divide-y divide-border rounded-md border border-border">
        {pages.map((page) => (
          <li key={page.id} className="flex items-center justify-between gap-3 px-4 py-3">
            <div>
              <p className="font-medium text-foreground">{page.title}</p>
              <p className="text-sm text-muted-foreground">
                {slugLabel(page)} · {kindLabel(page)}
              </p>
            </div>
            <div className="flex items-center gap-2 text-xs">
              {!page.isEnabled && <span className="rounded-full bg-muted px-2 py-0.5 text-muted-foreground">Đang tắt</span>}
              {page.isDirty && (
                <span className="rounded-full bg-primary/10 px-2 py-0.5 text-primary">Có thay đổi chưa xuất bản</span>
              )}
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => {
                  setForm({ type: 'edit', page });
                }}
              >
                Sửa
              </Button>
              {/* `deletable = false` với trang chủ và System page — ẩn nút thay vì chờ 409 PAGE_NOT_DELETABLE. */}
              {page.deletable && (
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => {
                    deleteMutation.reset();
                    setPendingDelete(page);
                  }}
                >
                  Xoá
                </Button>
              )}
            </div>
          </li>
        ))}
      </ul>

      {form && (
        <PageFormDialog
          key={form.type === 'edit' ? form.page.id : 'create'}
          shopId={shopId}
          page={form.type === 'edit' ? form.page : undefined}
          open
          onClose={() => {
            setForm(null);
          }}
        />
      )}
    </div>
  );
}
