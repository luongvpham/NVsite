import type { LinkValue } from '../meta/value-shapes';
import { isSafeLinkUrl } from './link-url';

/**
 * Mục menu đầu vào (tối đa 3 cấp): superset cấu trúc của `SiteNavigationItem` (snapshot, không có `hidden`)
 * và mục nháp ở Portal (có `hidden`). `hidden: true` ⇒ bỏ cả cây con.
 */
export interface NavigationInputItem {
  key: string;
  label: string | null;
  url?: string;
  hidden?: boolean;
  children?: readonly NavigationInputItem[];
}

export interface NavigationPageRef {
  id: string;
  title: string;
  slug: string | null;
  systemType: string | null;
}

export interface ResolvedNavigationItem {
  label: string;
  href: string;
  external: boolean;
  children: ResolvedNavigationItem[];
}

const PAGE_KEY = /^page:(.+)$/;

/**
 * Biến mục menu thành `{label, href, external, children}` để render. Mọi `href` của trang đi qua `resolveUrl`
 * (#103, không tự ghép chuỗi URL); `custom` lọc lại bằng `isSafeLinkUrl` (R3). Trang thiếu/không có URL ⇒ bỏ mục
 * (cha bị bỏ ⇒ bỏ cả nhóm, R5). Thuần, isomorphic (#23).
 */
export function resolveNavigation(
  items: readonly NavigationInputItem[],
  pages: readonly NavigationPageRef[],
  resolveUrl: (link: LinkValue) => string,
): ResolvedNavigationItem[] {
  const byId = new Map(pages.map((p) => [p.id.toLowerCase(), p]));

  const resolveOne = (item: NavigationInputItem): ResolvedNavigationItem | null => {
    if (item.hidden === true) return null;
    const label = item.label?.trim() ? item.label : null;
    let head: { label: string; href: string; external: boolean };

    const pageId = PAGE_KEY.exec(item.key)?.[1];
    if (pageId !== undefined) {
      const page = byId.get(pageId.toLowerCase());
      if (!page || page.slug === null) return null;
      head = { label: label ?? page.title, href: resolveUrl({ kind: 'page', pageId: page.id }), external: false };
    } else if (item.key === 'custom') {
      const url = item.url;
      if (label === null || typeof url !== 'string' || !isSafeLinkUrl(url)) return null;
      head = { label, href: url, external: /^https?:/i.test(url.trim()) };
    } else {
      return null;
    }

    return { ...head, children: resolveList(item.children) };
  };

  const resolveList = (list: readonly NavigationInputItem[] | undefined): ResolvedNavigationItem[] => {
    const out: ResolvedNavigationItem[] = [];
    for (const item of list ?? []) {
      const resolved = resolveOne(item);
      if (resolved) out.push(resolved);
    }
    return out;
  };

  return resolveList(items);
}
