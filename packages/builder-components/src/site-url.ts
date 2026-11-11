import systemPagesJson from '../../../config/system-pages.json';
import type { LinkValue } from '../meta/value-shapes';

/**
 * Quy tắc URL của Shop Site (#103): `basePath` + (`/{locale}` khi khác `defaultLocale`) + `/{slug}`.
 * Trang chủ có `slug ""` → `…/`. `NotFound` (slug null) không có URL. Trang đã xoá → `#`.
 * `basePath` là `''` trên site thật; khác `''` chỉ ở preview. Thuần, isomorphic — không đụng window/document (#23).
 */
export interface SitePageRef {
  id: string;
  slug: string | null;
  systemType: string | null;
  isEnabled: boolean;
}

export interface SiteUrlOptions {
  basePath: string;
  locale: string;
  defaultLocale: string;
  pages: readonly SitePageRef[];
}

export type SiteUrlMatch =
  | { kind: 'page'; pageId: string; enabled: boolean }
  | { kind: 'systemMissing'; systemType: string }
  | { kind: 'deleted' }
  | { kind: 'external' }
  | { kind: 'anchor'; nodeId: string };

const SYSTEM_ROUTES: Readonly<Record<string, { routes: readonly string[] }>> = systemPagesJson;
const DELETED: SiteUrlMatch = { kind: 'deleted' };

function prefixOf({ basePath, locale, defaultLocale }: SiteUrlOptions): { base: string; loc: string } {
  const base = basePath.replace(/\/+$/, '');
  const loc = locale && locale !== defaultLocale ? `/${locale}` : '';
  return { base, loc };
}

function urlForSlug(opts: SiteUrlOptions, slug: string): string {
  const { base, loc } = prefixOf(opts);
  return slug === '' ? `${base}${loc}/` : `${base}${loc}/${slug}`;
}

export function createResolveUrl(opts: SiteUrlOptions): (link: LinkValue) => string {
  return (link) => {
    switch (link.kind) {
      case 'external':
        return link.url;
      case 'anchor':
        return `#${link.nodeId}`;
      case 'page': {
        const page = opts.pages.find((p) => p.id === link.pageId);
        return page && page.slug !== null ? urlForSlug(opts, page.slug) : '#';
      }
      case 'systemPage': {
        const page = opts.pages.find((p) => p.systemType === link.systemType);
        if (page) return page.slug !== null ? urlForSlug(opts, page.slug) : '#';
        const route = SYSTEM_ROUTES[link.systemType]?.routes[0];
        return route !== undefined ? urlForSlug(opts, route) : '#';
      }
      case 'productCategory':
        return '#';
    }
  };
}

/** Nghịch đảo của `createResolveUrl` — tra `href` ngược về trang. Không bao giờ ném lỗi (R6). */
export function matchSiteUrl(href: string, opts: SiteUrlOptions): SiteUrlMatch {
  if (href === '') return DELETED;
  if (href.startsWith('#')) return href.length > 1 ? { kind: 'anchor', nodeId: href.slice(1) } : DELETED;
  if (/^[a-z][a-z0-9+.-]*:/i.test(href) || href.startsWith('//')) return { kind: 'external' };

  const path = href.split(/[?#]/, 1)[0] ?? '';
  const { base, loc } = prefixOf(opts);
  const prefix = base + loc;
  if (!path.startsWith(prefix)) return DELETED;
  const rest = path.slice(prefix.length);
  if (rest !== '' && !rest.startsWith('/')) return DELETED;

  const slug = rest.replace(/^\/+|\/+$/g, '');
  const page = opts.pages.find((p) => p.slug === slug);
  if (page) return { kind: 'page', pageId: page.id, enabled: page.isEnabled };

  if (slug !== '') {
    for (const [systemType, { routes }] of Object.entries(SYSTEM_ROUTES)) {
      if (routes.includes(slug) && !opts.pages.some((p) => p.systemType === systemType)) {
        return { kind: 'systemMissing', systemType };
      }
    }
  }
  return DELETED;
}
