import type { SiteLoaderData } from '../server/load-site-core';
import { selectPage } from '../lib/select-page';

/**
 * Chỉ `import type` từ load-site-core: module đó kéo site-client (server-only) vào bundle client nếu import giá trị.
 * Head SEO của Shop Site (WEBSITE-005 T7, G9). Thuần, isomorphic (#23): nhận loader data, trả
 * `{ meta, links }` đúng dạng `head()` của TanStack route. Origin đến từ loader data (dựng ở server).
 */
export interface SiteHead {
  meta?: Record<string, string>[];
  links?: { rel: string; href: string }[];
}

type SiteData = Extract<SiteLoaderData, { kind: 'site' }>;

const NOINDEX = { name: 'robots', content: 'noindex' } as const;

/** Chuỗi rỗng coi như thiếu (BE có thể trả "" thay vì null). */
const filled = (v: string | null | undefined): string | null => (v !== undefined && v !== null && v.trim() !== '' ? v : null);

/** id → URL tuyệt đối `/media/{storageKey}` (file gốc, bỏ qua preset). id thiếu trong `media` → null (R10). */
function mediaUrl(data: SiteData, id: string | null): string | null {
  if (id === null) return null;
  const entry = (data.snapshot.media as Record<string, { storageKey: string } | undefined>)[id];
  const key = entry ? filled(entry.storageKey) : null;
  return key === null ? null : `${data.origin}/media/${encodeURI(key)}`;
}

function canonicalOf(data: SiteData): string {
  const path = data.path !== '/' && data.path.endsWith('/') ? data.path.slice(0, -1) : data.path;
  return `${data.origin}${path === '' ? '/' : encodeURI(path)}`;
}

function siteHead(data: SiteData): SiteHead {
  const { snapshot, shop } = data;
  const sel = selectPage(snapshot, data.path);
  const page = sel.page;
  const title =
    filled(page?.seo.title) ?? filled(page?.title) ?? filled(snapshot.seo.defaultTitle) ?? shop.name;
  const description = filled(page?.seo.description) ?? filled(snapshot.seo.defaultDescription);
  const ogImage = mediaUrl(data, filled(page?.seo.ogImageId) ?? filled(snapshot.seo.ogImageId));
  const favicon = mediaUrl(data, filled(snapshot.seo.faviconAssetId));
  const found = sel.kind === 'page';
  const canonical = found ? canonicalOf(data) : null;

  const meta: Record<string, string>[] = [{ title }, { property: 'og:title', content: title }, { property: 'og:type', content: 'website' }];
  if (description !== null) {
    meta.push({ name: 'description', content: description }, { property: 'og:description', content: description });
  }
  if (ogImage !== null) meta.push({ property: 'og:image', content: ogImage });
  if (canonical !== null) meta.push({ property: 'og:url', content: canonical });
  // R9: noIndex của trang; 404 (trang NotFound của shop hoặc không có) cũng noindex.
  if (!found || page?.seo.noIndex === true) meta.push({ ...NOINDEX });

  const links: { rel: string; href: string }[] = [];
  if (canonical !== null) links.push({ rel: 'canonical', href: canonical });
  if (favicon !== null) links.push({ rel: 'icon', href: favicon });
  return { meta, links };
}

/** `platformNotFound`: route splat trả 404 cho host platform → noindex (T8). Platform `/` (200) giữ nguyên. */
export function buildSiteHead(data: SiteLoaderData | undefined, opts: { platformNotFound?: boolean } = {}): SiteHead {
  if (data === undefined) return {};
  switch (data.kind) {
    case 'platform':
      return opts.platformNotFound === true ? { meta: [{ ...NOINDEX }] } : {};
    case 'site':
      return siteHead(data);
    case 'suspended':
      return { meta: [{ title: data.shop.name }, { ...NOINDEX }] };
    case 'notFound':
    case 'error':
      return { meta: [{ ...NOINDEX }] };
  }
}

/** `lang` của `<html>`: ngôn ngữ mặc định của snapshot; kind khác → undefined (root giữ mặc định). */
export function siteLang(data: SiteLoaderData | undefined): string | undefined {
  return data?.kind === 'site' ? data.snapshot.defaultLocale : undefined;
}
