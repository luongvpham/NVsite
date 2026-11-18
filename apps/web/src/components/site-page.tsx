import type { SiteLoaderData } from '../server/load-site-core';
import { ErrorPage } from './error-page';
import { NotFoundPage } from './not-found-page';
import { SiteView } from './site-view';
import { SuspendedPage } from './suspended-page';

/**
 * Điểm render Shop Site theo loader data: `site` → `SiteView` (T5); suspended/notFound/error → trang hệ thống (T8).
 * Không đọc `window`/`document`/`Date` (#23) — năm lấy từ loader data.
 */
export function SitePage({ data }: Readonly<{ data: Exclude<SiteLoaderData, { kind: 'platform' }> }>) {
  switch (data.kind) {
    case 'site':
      return <SiteView data={data} />;
    case 'suspended':
      return <SuspendedPage shop={data.shop} />;
    case 'notFound':
      return <NotFoundPage />;
    case 'error':
      return <ErrorPage />;
  }
}

/** 404 của host platform cho path khác `/` (Main/Profile chưa làm, #96). Cùng trang 404 chung (G4). */
export function PlatformNotFound() {
  return <NotFoundPage />;
}
