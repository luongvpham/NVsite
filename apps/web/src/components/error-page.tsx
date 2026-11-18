import { SystemPageShell } from './system-page-shell';

/** Lỗi chung. Cố ý KHÔNG nhận chi tiết lỗi (message/stack/URL BE không bao giờ lên HTML). */
export function ErrorPage() {
  return (
    <SystemPageShell>
      <h1 className="text-2xl font-semibold">Đã có lỗi xảy ra</h1>
      <p className="mt-2 opacity-70">Chúng tôi không thể tải trang lúc này. Vui lòng thử lại sau ít phút.</p>
    </SystemPageShell>
  );
}

/** `errorComponent` của route: nhận `{ error }` nhưng bỏ qua hoàn toàn. */
export function RouteErrorPage(_props: Readonly<{ error?: unknown }>) {
  return <ErrorPage />;
}
