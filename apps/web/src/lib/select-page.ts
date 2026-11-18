import { reservedRoutes } from './reserved-routes';

/**
 * Chọn trang của Shop Site theo path (WEBSITE-005 T5, A10, R6, R7). Thuần, isomorphic (#23): loader dùng để
 * quyết định status 404, `SiteView` dùng để biết render trang nào — cùng một hàm nên không lệch.
 *
 * Quy tắc: "/" → trang slug ""; còn lại bỏ MỘT "/" cuối, decodeURIComponent (lỗi → không khớp), một đoạn duy
 * nhất, so khớp chính xác (phân biệt hoa/thường). Đoạn đầu thuộc `reservedShopSitePaths` (không phân biệt hoa/thường) không bao giờ là trang.
 */
export interface SelectablePage {
  readonly slug: string | null;
  readonly systemType: string | null;
}

export type PageSelection<P extends SelectablePage> =
  | { readonly kind: 'page'; readonly page: P }
  | { readonly kind: 'notFound'; readonly page: P | null };

const RESERVED: readonly string[] = reservedRoutes.reservedShopSitePaths.map((r) => r.toLowerCase());

function slugFromPath(path: string): string | null {
  if (path === '/') return '';
  const stripped = path.endsWith('/') ? path.slice(0, -1) : path;
  if (!stripped.startsWith('/') || stripped.length < 2) return null;
  const raw = stripped.slice(1);
  if (RESERVED.includes((raw.split('/')[0] ?? '').toLowerCase())) return null;
  let slug: string;
  try {
    slug = decodeURIComponent(raw);
  } catch {
    return null;
  }
  if (slug === '' || slug.includes('/') || RESERVED.includes(slug.toLowerCase())) return null;
  return slug;
}

export function selectPage<P extends SelectablePage>(
  snapshot: { readonly pages: readonly P[] },
  path: string,
): PageSelection<P> {
  const slug = slugFromPath(path);
  const match = slug === null ? undefined : snapshot.pages.find((p) => p.slug === slug);
  if (match) return { kind: 'page', page: match };
  const notFound = snapshot.pages.find((p) => p.slug === null && p.systemType === 'NotFound') ?? null;
  return { kind: 'notFound', page: notFound };
}
