import { SystemPageShell } from './system-page-shell';

/**
 * Website tạm ngưng: CHỈ tên + logo shop (live `shop`), không nội dung site. `logoUrl` đã có tiền tố `/media/`
 * (không qua mediaUrl) và có thể null → không render img.
 */
export function SuspendedPage({ shop }: Readonly<{ shop: { name: string; logoUrl: string | null } }>) {
  return (
    <SystemPageShell>
      {shop.logoUrl ? <img src={shop.logoUrl} alt={shop.name} className="mx-auto mb-6 h-16 w-auto" /> : null}
      <h1 className="text-2xl font-semibold">{shop.name}</h1>
      <p className="mt-2 opacity-70">Website đang tạm ngưng hoạt động. Vui lòng quay lại sau.</p>
    </SystemPageShell>
  );
}
