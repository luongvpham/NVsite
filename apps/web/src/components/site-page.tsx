import type { SiteLoaderData } from '../server/load-site-core';

/**
 * Điểm render Shop Site theo loader data (WEBSITE-005 T4). Các nhánh là PLACEHOLDER tối thiểu:
 * T5 thay nhánh `site` bằng `SiteView`, T8 thay notFound/suspended/error bằng trang hệ thống.
 * Không đọc `window`/`document`/`Date` (#23) — năm lấy từ loader data.
 */
export function SitePage({ data }: Readonly<{ data: Exclude<SiteLoaderData, { kind: 'platform' }> }>) {
  switch (data.kind) {
    case 'site':
      return (
        <main data-site-path={data.path}>
          <h1>{data.shop.name}</h1>
          <p>{data.snapshot.pages.length} trang</p>
          <footer>© {data.year} {data.shop.name}</footer>
        </main>
      );
    case 'suspended':
      return (
        <main>
          <h1>{data.shop.name}</h1>
          <p>Website đang tạm ngưng.</p>
        </main>
      );
    case 'notFound':
      return (
        <main>
          <h1>Không tìm thấy trang</h1>
        </main>
      );
    case 'error':
      return (
        <main>
          <h1>Đã có lỗi xảy ra</h1>
        </main>
      );
  }
}

/** 404 của host platform cho path khác `/` (Main/Profile chưa làm, #96). */
export function PlatformNotFound() {
  return (
    <main className="p-8">
      <h1 className="text-2xl font-semibold text-foreground">Không tìm thấy trang</h1>
    </main>
  );
}
