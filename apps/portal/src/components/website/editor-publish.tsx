import { useEffect, useLayoutEffect, useRef, useState, type ReactNode } from 'react';
import { Button } from '@vsite/ui';
import { useGetShop } from '@vsite/api-sdk';
import type { AutosaveController, AutosaveState, PrepareForPublishResult } from '../../lib/autosave/autosave-controller';
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
 * Theme thành bẩn trong lúc chuẩn bị ⇒ finish ngay, không mở dialog.
 *
 * Trả `button` (đặt trong toolbar), `dialog` (đặt NGOÀI toolbar — dialog không phải con của thanh công cụ) và `busy`
 * (đang chuẩn bị hoặc dialog mở — editor tắt phím tắt soạn trang lúc này).
 */
export function useEditorPublish({
  shopId,
  pageId,
  controller,
  themeDirty,
  autosaveStatus,
}: {
  shopId: string;
  pageId: string;
  controller: AutosaveController;
  themeDirty: boolean;
  /** Trạng thái autosave hiện tại — thông báo chặn (trừ theme) chỉ đúng khi trạng thái chưa đổi kể từ lúc chặn. */
  autosaveStatus: AutosaveState['status'];
}): { button: ReactNode; dialog: ReactNode; busy: boolean } {
  const [phase, setPhase] = useState<'idle' | 'preparing' | 'open'>('idle');
  /** Lý do chặn gần nhất + trạng thái autosave lúc chặn (để tự ẩn khi trạng thái đổi — S2). */
  const [blocked, setBlocked] = useState<{ reason: BlockReason; status: AutosaveState['status'] } | null>(null);
  /**
   * Đã prepare ok mà chưa finish. Phòng thủ: controller chịu được finish gọi thừa (không khoá ⇒ no-op), ref này chỉ
   * giữ đúng tinh thần "một prepare ↔ một finish" khi `onOpenChange(false)` bị gọi lặp.
   */
  const lockedRef = useRef(false);
  const mountedRef = useRef(false);
  /** `themeDirty` mới nhất — đọc lại sau `await prepare` (theme có thể vừa bẩn trong lúc chuẩn bị). */
  const themeDirtyRef = useRef(themeDirty);
  const isRollingBack = useIsRollingBack();
  const isPublishing = useIsPublishing();
  const shopQuery = useGetShop(shopId);
  const siteUrl = shopQuery.data ? shopSiteUrl(shopQuery.data.slug) : null;

  /** Hàm (không đọc thẳng ref) để TS không thu hẹp giá trị qua `await`. */
  const isThemeDirty = () => themeDirtyRef.current;

  useLayoutEffect(() => {
    themeDirtyRef.current = themeDirty;
  }, [themeDirty]);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
      lockedRef.current = false; // dispose() của controller đã/ sẽ bỏ khoá
    };
  }, []);

  const block = (reason: BlockReason) => {
    setBlocked({ reason, status: controller.getState().status });
  };

  const start = async () => {
    setBlocked(null);
    if (isThemeDirty()) {
      block('theme');
      return;
    }
    setPhase('preparing');
    const result = await controller.prepareForPublish();
    if (!mountedRef.current) return;
    if (!result.ok) {
      setPhase('idle');
      block(result.reason);
      return;
    }
    if (isThemeDirty()) {
      // Theme vừa bẩn trong lúc chuẩn bị: bỏ lượt này, mở khoá ngay (prepare ok ⇒ đúng một finish).
      void controller.finishPublish();
      setPhase('idle');
      block('theme');
      return;
    }
    lockedRef.current = true;
    setPhase('open');
  };

  const handleOpenChange = (open: boolean) => {
    if (open) return;
    setPhase('idle');
    if (!lockedRef.current) return;
    lockedRef.current = false;
    // Fire-and-forget: finishPublish không ném và không setState ở đây ⇒ an toàn cả khi editor unmount giữa chừng.
    void controller.finishPublish();
  };

  // Tự ẩn khi hết đúng: theme ⇒ khi theme hết bẩn; lý do autosave ⇒ khi trạng thái autosave đã đổi (S2).
  const stillTrue =
    blocked !== null && (blocked.reason === 'theme' ? themeDirty : blocked.status === autosaveStatus);
  const message = blocked && stillTrue ? BLOCK_MESSAGES[blocked.reason] : null;

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

  return { button, dialog, busy: phase !== 'idle' };
}
