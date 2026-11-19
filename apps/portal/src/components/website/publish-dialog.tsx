import { useState } from 'react';
import { Button, Dialog } from '@vsite/ui';
import { usePublishWebsiteFlow } from '../../hooks/use-publish-website';
import { PublishErrors } from './publish-errors';

export const PUBLISH_NOTE_MAX = 200;

export interface PublishDialogProps {
  shopId: string;
  open: boolean;
  /** Gọi `false` khi người dùng đóng dialog (Huỷ / Đóng / Escape / nền) — caller dùng làm tín hiệu "dialog đã đóng". */
  onOpenChange: (open: boolean) => void;
  /** Trang đang mở trong editor (F5); lỗi thuộc trang này hiện "trang này" thay vì link "Mở trang". */
  currentPageId?: string;
  /**
   * Gọi đúng một lần mỗi lần xuất bản kết thúc (thành công hoặc lỗi) — kể cả khi dialog đã đóng/unmount
   * giữa chừng. Dialog không đóng được khi đang xuất bản.
   */
  onSettled?: () => void;
  /** URL site công khai (`shopSiteUrl(slug)`); `null` ⇒ ẩn link "Mở site". */
  siteUrl?: string | null;
  /** Một dòng ghi chú riêng của nơi mở dialog (editor: sửa sau khi bấm Xuất bản không nằm trong bản này). */
  notice?: string;
}

/**
 * Dialog xuất bản dùng chung cho màn tổng quan (F4) và toolbar editor (F5).
 *
 * Mỗi lần mở (đóng → mở) là một phiên mới: thân dialog mount lại theo `key` ngay trong lần render mở, nên note và
 * kết quả/lỗi của lần trước đã sạch TRƯỚC khi người dùng thấy dialog — không có khe "reset trong effect" để một cú
 * bấm lọt vào trước rồi bị xoá kết quả. Dialog không đóng được khi POST đang chạy ⇒ lúc mở lại không còn POST nào
 * của phiên cũ đang bay; `onSettled` gắn ở tầng mutation nên vẫn chạy cả khi thân cũ đã unmount.
 */
export function PublishDialog(props: PublishDialogProps) {
  const { open } = props;
  const [session, setSession] = useState(0);
  const [wasOpen, setWasOpen] = useState(open);
  if (open !== wasOpen) {
    // Điều chỉnh state theo prop ngay khi render (không effect) — React render lại tức thì trước khi commit.
    setWasOpen(open);
    if (open) setSession((n) => n + 1);
  }
  return <PublishDialogBody key={session} {...props} />;
}

function PublishDialogBody({ shopId, open, onOpenChange, currentPageId, onSettled, siteUrl = null, notice }: PublishDialogProps) {
  const [note, setNote] = useState('');
  const { publish, isPending, result, failure } = usePublishWebsiteFlow(shopId, { onSettled });

  const trimmedLength = note.trim().length;
  const tooLong = trimmedLength > PUBLISH_NOTE_MAX;
  const close = () => {
    if (isPending) return;
    onOpenChange(false);
  };

  return (
    <Dialog open={open} onClose={close} title="Xuất bản website" dismissible={!isPending}>
      {notice && <p className="mb-3 text-sm text-muted-foreground">{notice}</p>}
      {result ? (
        <div className="space-y-4">
          <p className="text-sm text-foreground">Đã xuất bản bản v{result.version}</p>
          <div className="flex justify-end gap-2">
            {siteUrl && (
              <a
                href={siteUrl}
                target="_blank"
                rel="noreferrer"
                className="inline-flex h-10 items-center rounded-md border border-border px-4 text-sm font-medium hover:bg-muted"
              >
                Mở site
              </a>
            )}
            <Button type="button" onClick={close}>
              Xong
            </Button>
          </div>
        </div>
      ) : (
        <form
          className="space-y-4"
          onSubmit={(event) => {
            event.preventDefault();
            if (isPending || tooLong) return;
            publish(note);
          }}
        >
          <div>
            <label htmlFor="publish-note" className="mb-1 block text-sm font-medium text-foreground">
              Ghi chú (tuỳ chọn)
            </label>
            <textarea
              id="publish-note"
              rows={3}
              value={note}
              onChange={(event) => { setNote(event.target.value); }}
              className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm"
            />
            {tooLong && (
              <p className="mt-1 text-sm text-destructive">Ghi chú tối đa {PUBLISH_NOTE_MAX} ký tự.</p>
            )}
          </div>

          {failure && (
            <div className="space-y-2 text-sm text-destructive">
              <p role="alert">{failure.message}</p>
              {failure.kind === 'blocked' && (
                <PublishErrors shopId={shopId} params={failure.params} currentPageId={currentPageId} />
              )}
            </div>
          )}

          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" disabled={isPending} onClick={close}>
              Huỷ
            </Button>
            <Button type="submit" disabled={isPending || tooLong}>
              {isPending ? 'Đang xuất bản…' : 'Xuất bản'}
            </Button>
          </div>
        </form>
      )}
    </Dialog>
  );
}
