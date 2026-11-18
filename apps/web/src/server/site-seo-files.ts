import type { SiteSnapshot } from '@vsite/builder-components';
import { reservedRoutes } from '../lib/reserved-routes';
import { buildOrigin } from './origin';
import { SiteFetchError, SiteResultKind, type SiteResult } from './site-client';
import { resolveSiteHost } from './site-host';

/**
 * robots.txt + sitemap.xml theo host (WEBSITE-005 T9, #10, A3, A7, R9). Builder thuần + lõi handler
 * không đọc request/env — vỏ server route (`routes/robots[.]txt.ts`, `routes/sitemap[.]xml.ts`) truyền
 * host gốc (`getRequestHost({ xForwardedHost: false })`), protocol từ URL request và deps.
 */

export const SeoFileKind = { Robots: 'robots', Sitemap: 'sitemap' } as const;
export type SeoFileKind = (typeof SeoFileKind)[keyof typeof SeoFileKind];

const RESERVED: readonly string[] = reservedRoutes.reservedShopSitePaths;
/** A6/#109: giống HTML Shop Site — không cache ở CDN/proxy dùng chung. */
const CACHE_CONTROL = 'private, no-cache';
const TEXT = 'text/plain; charset=utf-8';
const XML = 'application/xml; charset=utf-8';
const W3C_DATETIME = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:\d{2})$/;

export function buildRobotsTxt(origin: string): string {
  return `User-agent: *\nAllow: /\nSitemap: ${origin}/sitemap.xml\n`;
}

function escapeXml(s: string): string {
  return s
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&apos;');
}

/** A7: publishedAt từ BE (ISO, `+00:00` hợp lệ) giữ nguyên; dạng khác → ISO UTC; không đọc được → bỏ lastmod. */
function normalizeLastmod(publishedAt: string): string | null {
  if (W3C_DATETIME.test(publishedAt)) return publishedAt;
  const t = new Date(publishedAt);
  return Number.isNaN(t.getTime()) ? null : t.toISOString();
}


export function buildSitemapXml(origin: string, snapshot: SiteSnapshot, publishedAt: string): string {
  const lastmod = normalizeLastmod(publishedAt);
  const urls: string[] = [];
  for (const page of snapshot.pages) {
    if (page.slug === null || page.seo.noIndex || RESERVED.includes(page.slug)) continue;
    const loc = page.slug === '' ? `${origin}/` : `${origin}/${encodeURIComponent(page.slug)}`;
    urls.push(
      `  <url><loc>${escapeXml(loc)}</loc>${lastmod === null ? '' : `<lastmod>${escapeXml(lastmod)}</lastmod>`}</url>`,
    );
  }
  return `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${urls.join('\n')}${urls.length ? '\n' : ''}</urlset>\n`;
}

export interface SeoFileDeps {
  readonly rootDomain: string;
  /** Nhận host đã chuẩn hoá (kind `shop` của `resolveSiteHost`). */
  readonly fetchSite: (host: string) => Promise<SiteResult>;
  /** Log server-side; `error.cause` có thể chứa BE URL — không bao giờ ra body. */
  readonly logError: (message: string, error: unknown) => void;
}

export interface SeoFileResponse {
  readonly status: 200 | 404 | 500 | 502;
  readonly headers: Readonly<Record<string, string>>;
  readonly body: string;
}

const plain = (status: 404 | 500 | 502, body: string): SeoFileResponse => ({
  status,
  headers: { 'Content-Type': TEXT, 'Cache-Control': CACHE_CONTROL },
  body,
});

export async function handleSeoFile(
  kind: SeoFileKind,
  rawHost: string,
  protocol: string,
  deps: SeoFileDeps,
): Promise<SeoFileResponse> {
  const resolved = resolveSiteHost(rawHost, deps.rootDomain);
  // M4: host ngoài `{label}.{ROOT_DOMAIN}` → 404, không gọi BE.
  if (resolved.kind === 'platform') return plain(404, 'Not found\n');

  let result: SiteResult;
  try {
    result = await deps.fetchSite(resolved.host);
  } catch (error) {
    if (error instanceof SiteFetchError) {
      deps.logError('[seo-files] BE không trả được dữ liệu site', error);
      return plain(502, 'Bad gateway\n');
    }
    deps.logError('[seo-files] lỗi dữ liệu site', error);
    return plain(500, 'Internal server error\n');
  }
  // Suspended không được lập chỉ mục (A5) → 404 cho cả hai file.
  if (result.kind !== SiteResultKind.Site) return plain(404, 'Not found\n');

  const origin = buildOrigin(protocol, rawHost);
  const body =
    kind === SeoFileKind.Robots
      ? buildRobotsTxt(origin)
      : buildSitemapXml(origin, result.snapshot, result.publishedAt);
  return {
    status: 200,
    headers: {
      'Content-Type': kind === SeoFileKind.Robots ? TEXT : XML,
      'Cache-Control': CACHE_CONTROL,
    },
    body,
  };
}
