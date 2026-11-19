import { useEffect, useState } from 'react';
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
  /** Gọi đúng một lần mỗi lần xuất bản kết thúc (thành công hoặc lỗi). */
  onSettled?: () => void;
  /** URL site công khai (`shopSiteUrl(slug)`); `null` ⇒ ẩn link "Mở site". */
  siteUrl?: string | null;
}

/** Dialog xuất bản dùng chung cho màn tổng quan (F4) và toolbar editor (F5). */
export function PublishDialog({ shopId, open, onOpenChange, currentPageId, onSettled, siteUrl = null }: PublishDialogProps) {
  const [note, setNote] = useState('');
  const { publish, isPending, result, failure, reset } = usePublishWebsiteFlow(shopId);

  useEffect(() => {
    if (open) {
      setNote('');
      reset();
    }
  }, [open, reset]);

  const trimmedLength = note.trim().length;
  const tooLong = trimmedLength > PUBLISH_NOTE_MAX;
  const close = () => { onOpenChange(false); };

  return (
    <Dialog open={open} onClose={close} title="Xuất bản website">
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
              Đóng
            </Button>
          </div>
        </div>
      ) : (
        <form
          className="space-y-4"
          onSubmit={(event) => {
            event.preventDefault();
            if (isPending || tooLong) return;
            publish(note, onSettled);
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
            <div role="alert" className="space-y-2 text-sm text-destructive">
              <p>{failure.message}</p>
              {failure.kind === 'blocked' && (
                <PublishErrors shopId={shopId} params={failure.params} currentPageId={currentPageId} />
              )}
            </div>
          )}

          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={close}>
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
