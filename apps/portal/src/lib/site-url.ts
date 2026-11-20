/**
 * URL site công khai của shop từ `VITE_SHOP_SITE_ORIGIN_TEMPLATE` (vd. `http://{slug}.vsite.local:3000`).
 * Template thiếu/rỗng (production trước khi deploy) hoặc slug rỗng ⇒ `null` — UI ẩn link "Mở site".
 */
export function shopSiteUrl(slug: string): string | null {
  const template = (import.meta.env.VITE_SHOP_SITE_ORIGIN_TEMPLATE as string | undefined)?.trim();
  if (!template || !slug) return null;
  return template.split('{slug}').join(slug);
}
