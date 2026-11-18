import { SystemPageShell } from './system-page-shell';

/** 404 chung (G4: không phân biệt vì sao — host lạ, shop không tồn tại, trang không có). */
export function NotFoundPage() {
  return (
    <SystemPageShell>
      <p className="text-5xl font-semibold" aria-hidden="true">
        404
      </p>
      <h1 className="mt-4 text-2xl font-semibold">Không tìm thấy trang</h1>
      <p className="mt-2 opacity-70">Trang bạn tìm không tồn tại hoặc đã được chuyển đi.</p>
    </SystemPageShell>
  );
}
