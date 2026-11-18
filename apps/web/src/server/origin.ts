/**
 * Origin của request gốc cho canonical/og:image (WEBSITE-005 T7, A3). Thuần.
 * `protocol` lấy từ URL request (không tin X-Forwarded-Proto), `host` từ `getRequestHost({ xForwardedHost: false })`.
 * Hạ chữ thường, bỏ dấu chấm cuối của tên miền, giữ cổng (dev `:3000`).
 */
export function buildOrigin(protocol: string, host: string): string {
  const scheme = protocol.replace(/:$/, '') === 'https' ? 'https' : 'http';
  const normalized = host.trim().toLowerCase().replace(/\.(?=:\d+$|$)/, '');
  return `${scheme}://${normalized}`;
}
