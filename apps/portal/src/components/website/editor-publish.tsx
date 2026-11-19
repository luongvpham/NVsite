import { useEffect, useRef, useState, type ReactNode } from 'react';
import { Button } from '@vsite/ui';
import { useGetShop } from '@vsite/api-sdk';
import type { AutosaveController, PrepareForPublishResult } from '../../lib/autosave/autosave-controller';
import { useIsPublishing, useIsRollingBack } from '../../hooks/use-publication-mutations';
import { shopSiteUrl } from '../../lib/site-url';
import { PublishDialog } from './publish-dialog';

type BlockReason = 'theme' | Extract<PrepareForPublishResult, { ok: false }>['reason'];

const BLOCK_MESSAGES: Record<BlockReason, string> = {
  theme: 'Giao diện còn thay đổi chưa lưu — lưu hoặc hoàn tác ở tab Giao diện trước khi xuất bản.',
  invalid: 'Trang còn chỗ chưa hợp lệ — sửa các lỗi được đánh dấu trước khi xuất bản.',
  conflict: 'Có bản mới hơn của trang này — xử lý hộp thoại xung đột trước khi xuất bản.',
  blocked: 'Trang quá lớn, chưa lưu được — giảm bớt nội dung trước khi xuất bản.',
  error: 'Chưa lưu được trang — thử lại.',
};

const EDITOR_NOTICE = 'Sửa sau khi bấm Xuất bản không nằm trong bản này — sẽ được lưu ngay khi đóng hộp thoại.';

/**
 * Nút "Xuất bản" của toolbar editor (F5, R9). Thứ tự: theme bẩn ⇒ chặn, không gọi gì · `prepareForPublish` (lưu ngay
 * phần chưa lưu rồi khoá autosave) ⇒ `ok: false` ⇒ báo theo lý do · `ok` ⇒ mở dialog. Khoá giữ suốt lúc dialog mở (G5,
 * người dùng có thể thử lại sau 422/409 trong cùng dialog); đóng dialog ⇒ `finishPublish` đúng MỘT lần cho mỗi prepare.
 * Dialog tự không cho đóng khi POST đang chạy, nên "đóng" luôn sau khi POST đã xong.
 * Editor unmount khi đang khoá: `controller.dispose()` (useAutosave) bỏ khoá — không gọi finish (khỏi GET thừa).
 *
 * Trả hai phần: `button` (đặt trong toolbar) và `dialog` (đặt NGOÀI toolbar — dialog không phải con của thanh công cụ).
 */
export function useEditorPublish({
  shopId,
  pageId,
  controller,
  themeDirty,
  onDialogOpenChange,
}: {
  shopId: string;
  pageId: string;
  controller: AutosaveController;
  themeDirty: boolean;
  onDialogOpenChange?: (open: boolean) => void;
}): { button: ReactNode; dialog: ReactNode } {
  const [phase, setPhase] = useState<'idle' | 'preparing' | 'open'>('idle');
  const [blocked, setBlocked] = useState<BlockReason | null>(null);
  /** Đã prepare ok mà chưa finish — chặn finish hai lần (đóng lặp). */
  const lockedRef = useRef(false);
  const mountedRef = useRef(false);
  const isRollingBack = useIsRollingBack();
  const isPublishing = useIsPublishing();
  const shopQuery = useGetShop(shopId);
  const siteUrl = shopQuery.data ? shopSiteUrl(shopQuery.data.slug) : null;

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
      lockedRef.current = false; // dispose() của controller đã/ sẽ bỏ khoá
    };
  }, []);

  const setOpen = (open: boolean) => {
    setPhase(open ? 'open' : 'idle');
    onDialogOpenChange?.(open);
  };

  const start = async () => {
    setBlocked(null);
    if (themeDirty) {
      setBlocked('theme');
      return;
    }
    setPhase('preparing');
    const result = await controller.prepareForPublish();
    if (!mountedRef.current) return;
    if (!result.ok) {
      setPhase('idle');
      setBlocked(result.reason);
      return;
    }
    lockedRef.current = true;
    setOpen(true);
  };

  const handleOpenChange = (open: boolean) => {
    if (open) return;
    setOpen(false);
    if (!lockedRef.current) return;
    lockedRef.current = false;
    // Fire-and-forget: finishPublish không ném và không setState ở đây ⇒ an toàn cả khi editor unmount giữa chừng.
    void controller.finishPublish();
  };

  // Thông báo theme chỉ còn đúng khi theme vẫn bẩn (đã lưu/hoàn tác thì tự ẩn).
  const message = blocked && !(blocked === 'theme' && !themeDirty) ? BLOCK_MESSAGES[blocked] : null;

  const button = (
    <>
      <Button
        type="button"
        size="sm"
        disabled={phase !== 'idle' || isRollingBack || isPublishing}
        onClick={() => {
          void start();
        }}
      >
        Xuất bản
      </Button>
      {message && (
        <p role="alert" className="text-sm text-destructive">
          {message}
        </p>
      )}
    </>
  );

  const dialog = (
    <PublishDialog
      shopId={shopId}
      open={phase === 'open'}
      onOpenChange={handleOpenChange}
      currentPageId={pageId}
      siteUrl={siteUrl}
      notice={EDITOR_NOTICE}
    />
  );

  return { button, dialog };
}
