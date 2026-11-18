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

/**
 * Đoạn đầu của path thuộc `reservedShopSitePaths` (media/api/assets/static) — không phân biệt hoa/thường, xét cả bản
 * decode (`/%6dedia` = `/media`). Loader dùng để trả 404 chung mà KHÔNG gọi BE (plan §2 Phát hiện 2); `selectPage`
 * dùng để không bao giờ coi path đó là trang. Một nguồn danh sách duy nhất (#24).
 */
export function isReservedShopSitePath(path: string): boolean {
  if (!path.startsWith('/')) return false;
  const first = path.slice(1).split('/')[0] ?? '';
  if (RESERVED.includes(first.toLowerCase())) return true;
  try {
    return RESERVED.includes(decodeURIComponent(first).toLowerCase());
  } catch {
    return false; // URI hỏng: không phải reserved; selectPage tự trả notFound
  }
}

function slugFromPath(path: string): string | null {
  if (path === '/') return '';
  if (isReservedShopSitePath(path)) return null;
  const stripped = path.endsWith('/') ? path.slice(0, -1) : path;
  if (!stripped.startsWith('/') || stripped.length < 2) return null;
  const raw = stripped.slice(1);
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
