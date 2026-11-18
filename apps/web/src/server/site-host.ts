import { reservedRoutes } from '../lib/reserved-routes';

/**
 * Nhận diện host Shop Site (WEBSITE-005, chặn M4 ở FE): BE chọn tenant theo nhãn đầu
 * của Host, nên `evil.com` sẽ thành slug `evil`. Chỉ `{label}.{ROOT_DOMAIN}` mới được qua.
 * Hàm thuần — không đọc env/request. `host` trả về đã chuẩn hoá (lowercase, không cổng,
 * không dấu chấm cuối): cùng giá trị dùng làm Host forward và key LRU (A11).
 */
export type SiteHostResult =
  | { readonly kind: 'shop'; readonly host: string; readonly slug: string }
  | { readonly kind: 'platform' };

const PLATFORM: SiteHostResult = { kind: 'platform' };
const SLUG_LABEL = /^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$/;
const PORT = /^:\d{1,5}$/;

function stripTrailingDots(s: string): string {
  let end = s.length;
  while (end > 0 && s[end - 1] === '.') end--;
  return s.slice(0, end);
}

function normalizeHost(raw: string): string | null {
  let h = raw.trim().toLowerCase();
  if (h === '' || h.startsWith('[')) return null; // thiếu Host / IPv6 literal
  const colon = h.indexOf(':');
  if (colon !== -1) {
    if (!PORT.test(h.slice(colon))) return null;
    h = h.slice(0, colon);
  }
  h = stripTrailingDots(h);
  return h === '' ? null : h;
}

export function resolveSiteHost(rawHost: string, rootDomain: string): SiteHostResult {
  const root = stripTrailingDots(rootDomain.trim().toLowerCase().replace(/^\.+/, ''));
  if (root === '') return PLATFORM;
  const host = normalizeHost(rawHost);
  if (host === null) return PLATFORM;

  const suffix = `.${root}`;
  if (!host.endsWith(suffix)) return PLATFORM;
  const label = host.slice(0, -suffix.length);
  if (!SLUG_LABEL.test(label)) return PLATFORM; // gồm cả nhiều nhãn (có '.') và nhãn rỗng
  if (reservedRoutes.reservedSubdomains.includes(label)) return PLATFORM;
  return { kind: 'shop', host, slug: label };
}
