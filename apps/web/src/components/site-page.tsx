import type { SiteLoaderData } from '../server/load-site-core';
import { SiteView } from './site-view';

/**
 * Điểm render Shop Site theo loader data (WEBSITE-005 T4). Các nhánh là PLACEHOLDER tối thiểu:
 * nhánh `site` do `SiteView` (T5); T8 thay notFound/suspended/error bằng trang hệ thống.
 * Không đọc `window`/`document`/`Date` (#23) — năm lấy từ loader data.
 */
export function SitePage({ data }: Readonly<{ data: Exclude<SiteLoaderData, { kind: 'platform' }> }>) {
  switch (data.kind) {
    case 'site':
      return <SiteView data={data} />;
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
