import { createFileRoute, Link, Outlet, redirect, useNavigate } from '@tanstack/react-router';
import { Button } from '@vsite/ui';
import { useSessionStore } from '../stores/session-store';

/**
 * Layout route dùng chung + route guard cho toàn bộ Portal sau đăng nhập
 * (Docs/tasks/SHOP-001/brief.md mục "Việc FE cần làm" #4). `beforeLoad` chạy trước mọi route con
 * mỗi lần điều hướng — chưa đăng nhập thì redirect `/login`, không render children.
 */
export const Route = createFileRoute('/_authenticated')({
  beforeLoad: () => {
    if (!useSessionStore.getState().isAuthenticated) {
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
        <Link to="/shops" className="font-semibold text-foreground">
          vsite portal
        </Link>
        <Button
          variant="outline"
          size="sm"
          onClick={() => {
            clearSession();
            void navigate({ to: '/login' });
          }}
        >
          Đăng xuất
        </Button>
      </header>
      <Outlet />
    </div>
  );
}
