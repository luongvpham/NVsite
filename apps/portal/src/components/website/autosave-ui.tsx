import { useState } from 'react';
import { Button, Dialog } from '@vsite/ui';
import type { AutosaveController, AutosaveState, AutosaveStatus as Status } from '../../lib/autosave/autosave-controller';
import { getErrorMessage } from '../../lib/error-messages';

const STATUS_LABEL: Record<Status, string> = {
  saved: 'Đã lưu',
  unsaved: 'Chưa lưu',
  saving: 'Đang lưu…',
  error: 'Lỗi — chưa lưu được',
  blocked: 'Chưa lưu — trang quá lớn',
  invalid: 'Chưa lưu — có chỗ chưa hợp lệ',
  conflict: 'Chưa lưu — có bản mới hơn',
};

/** Chỉ báo trạng thái lưu (Flow 5 bước 6). Lỗi mạng/5xx có nút "Thử lại". */
export function AutosaveStatus({ state, controller }: { state: AutosaveState; controller: AutosaveController }) {
  return (
    <div role="status" aria-live="polite" data-testid="autosave-status" className="ml-auto flex items-center gap-2 text-sm">
      <span
        data-status={state.status}
        className={state.status === 'saved' || state.status === 'saving' ? 'text-muted-foreground' : 'text-destructive'}
      >
        {STATUS_LABEL[state.status]}
      </span>
      {state.status === 'error' && (
        <Button
          type="button"
          size="sm"
          variant="outline"
          onClick={() => {
            controller.retry();
          }}
        >
          Thử lại
        </Button>
      )}
    </div>
  );
}

/**
 * Lỗi toàn trang: quá lớn (client chặn hoặc BE 413) và lỗi 422 không gắn được vào node nào (path gốc, hoặc node đã
 * bị xoá/di chuyển sau lần gửi — R5). Lỗi gắn được vào node thì hiện ở canvas (viền đỏ) + Inspector (ô đỏ).
 */
export function ValidationBanner({ state }: { state: AutosaveState }) {
  const { blocked, invalid } = state;
  if (!blocked && !invalid) return null;

  return (
    <div role="alert" className="space-y-1 border-b border-destructive/40 bg-destructive/10 px-4 py-2 text-sm text-destructive">
      {blocked && (
        <p>
          <strong>Trang quá lớn.</strong> {blocked.message} Bấm "Hoàn tác" hoặc bớt khối để lưu lại.
        </p>
      )}
      {invalid && (
        <>
          <p>
            <strong>{getErrorMessage(invalid.code)}</strong>
            {invalid.mapped.byNode.size > 0 && ' Các khối có lỗi đã được viền đỏ trên trang.'}
          </p>
          {invalid.mapped.banner.length > 0 && (
            <ul className="list-disc pl-5">
              {invalid.mapped.banner.map((error, i) => (
                <li key={`${error.path}:${error.code}:${String(i)}`}>
                  {error.message} <span className="text-xs opacity-70">({error.code})</span>
                </li>
              ))}
            </ul>
          )}
          {invalid.mapped.truncated && <p>Còn lỗi khác chưa hiện — sửa các lỗi trên rồi lưu lại sẽ thấy tiếp.</p>}
        </>
      )}
    </div>
  );
}

/** 409: bắt buộc chọn. "Giữ bản của tôi" ghi đè bản mới hơn nên cần xác nhận thêm một bước. */
export function ConflictDialog({ state, controller }: { state: AutosaveState; controller: AutosaveController }) {
  const [confirmKeep, setConfirmKeep] = useState(false);
  const conflict = state.conflict;
  const updatedAt = conflict?.params?.updatedAt ? new Date(conflict.params.updatedAt) : undefined;
  const when = updatedAt && !Number.isNaN(updatedAt.getTime()) ? updatedAt.toLocaleString('vi-VN') : undefined;

  return (
    <Dialog open={conflict !== null} onClose={() => undefined} dismissible={false} title="Có bản mới hơn">
      <div className="space-y-4">
        <p className="text-sm">
          Trang này vừa được lưu ở tab hoặc thiết bị khác{when ? ` lúc ${when}` : ''}. Thay đổi trên màn hình này chưa được lưu.
        </p>
        {conflict?.resolveFailed && (
          <p role="alert" className="text-sm text-destructive">
            Không lấy được bản mới nhất từ máy chủ. Kiểm tra kết nối rồi thử lại.
          </p>
        )}
        {confirmKeep ? (
          <div className="space-y-3">
            <p className="text-sm font-medium">
              Giữ bản của bạn sẽ ghi đè bản mới hơn trên máy chủ. Chắc chắn?
            </p>
            <div className="flex flex-wrap gap-2">
              <Button
                type="button"
                variant="outline"
                className="border-destructive text-destructive"
                disabled={conflict?.resolving}
                onClick={() => {
                  setConfirmKeep(false);
                  void controller.keepMine();
                }}
              >
                Ghi đè bằng bản của tôi
              </Button>
              <Button
                type="button"
                variant="outline"
                disabled={conflict?.resolving}
                onClick={() => {
                  setConfirmKeep(false);
                }}
              >
                Quay lại
              </Button>
            </div>
          </div>
        ) : (
          <div className="flex flex-wrap gap-2">
            <Button
              type="button"
              disabled={conflict?.resolving}
              onClick={() => {
                void controller.reloadLatest();
              }}
            >
              Tải bản mới
            </Button>
            <Button
              type="button"
              variant="outline"
              disabled={conflict?.resolving}
              onClick={() => {
                setConfirmKeep(true);
              }}
            >
              Giữ bản của tôi
            </Button>
          </div>
        )}
        <p className="text-xs text-muted-foreground">
          "Tải bản mới" bỏ các thay đổi chưa lưu ở màn hình này.
        </p>
      </div>
    </Dialog>
  );
}

/** Rời tab "Giao diện" khi theme chưa lưu: bản sửa còn trên canvas nhưng chưa lên server. */
export function LeaveThemeTabPrompt({ onStay, onLeave }: { onStay: () => void; onLeave: () => void }) {
  return (
    <Dialog open onClose={onStay} title="Giao diện chưa lưu">
      <div className="space-y-4">
        <p className="text-sm">
          Các thay đổi giao diện chưa được lưu. Chúng vẫn hiện trên canvas nhưng sẽ mất khi rời trang.
        </p>
        <div className="flex gap-2">
          <Button type="button" onClick={onStay}>
            Ở lại để lưu
          </Button>
          <Button type="button" variant="outline" onClick={onLeave}>
            Chuyển tab
          </Button>
        </div>
      </div>
    </Dialog>
  );
}

/** Router blocker: rời trang khi còn thay đổi chưa lưu. */
export function LeavePrompt({ onStay, onLeave }: { onStay: () => void; onLeave: () => void }) {
  return (
    <Dialog open onClose={onStay} title="Còn thay đổi chưa lưu">
      <div className="space-y-4">
        <p className="text-sm">Rời trang bây giờ sẽ mất các thay đổi chưa được lưu.</p>
        <div className="flex gap-2">
          <Button type="button" onClick={onStay}>
            Ở lại
          </Button>
          <Button type="button" variant="outline" onClick={onLeave}>
            Rời đi
          </Button>
        </div>
      </div>
    </Dialog>
  );
}
