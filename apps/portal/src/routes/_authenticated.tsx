import { createFileRoute, Link, Outlet, redirect, useNavigate } from '@tanstack/react-router';
import { Button } from '@vsite/ui';
import { logout, waitForPendingRefresh } from '@vsite/api-sdk';
import { ensureSession, useSessionStore } from '../stores/session-store';

/**
 * Layout route dùng chung + route guard cho toàn bộ Portal sau đăng nhập
 * (Docs/tasks/SHOP-001/brief.md mục "Việc FE cần làm" #4). `beforeLoad` chạy trước mọi route con
 * mỗi lần điều hướng — chưa đăng nhập thì redirect `/login`, không render children. Store rỗng (F5)
 * thì chờ khôi phục phiên bằng cookie httpOnly trước khi quyết (#101), nên không nháy `/login`.
 */
export const Route = createFileRoute('/_authenticated')({
  beforeLoad: async () => {
    if (!(await ensureSession())) {
      // TanStack Router quy ước throw redirect() để huỷ load route — không phải Error thật.
      // eslint-disable-next-line @typescript-eslint/only-throw-error
      throw redirect({ to: '/login' });
    }
  },
  component: AuthenticatedLayout,
});

function AuthenticatedLayout() {
  const navigate = useNavigate();
  const clearSession = useSessionStore((state) => state.clearSession);

  return (
    <div className="min-h-screen bg-background">
      <header className="flex items-center justify-between border-b border-border px-6 py-4">
        <div className="flex items-center gap-4">
          <Link to="/shops" className="font-semibold text-foreground">
            vsite portal
          </Link>
        </div>
        <Button
          variant="outline"
          size="sm"
          onClick={() => {
            // Thoát phía FE NGAY — không chờ mạng, nên server treo cũng không kẹt nút (I3).
            clearSession();
            void navigate({ to: '/login' });
            // Rồi thu hồi refresh token + xoá cookie ở BE (#101), SAU khi refresh đang bay (nếu có) xong
            // (I1). Lỗi mạng/500 chỉ để cookie sống tới hết hạn — chấp nhận, FE đã thoát.
            void waitForPendingRefresh()
              .then(() => logout())
              .catch(() => undefined);
          }}
        >
          Đăng xuất
        </Button>
      </header>
      <Outlet />
    </div>
  );
}
