const NAMED_URL_ENTITIES: Record<string, string> = { colon: ':', tab: '\t', newline: '\n', lpar: '(', rpar: ')' };

/**
 * Link an toàn: scheme ∈ https/http/mailto/tel, hoặc đường dẫn nội bộ '/x' (KHÔNG '//host', không '/\') hoặc '#neo'.
 * Chuẩn hoá trước khi so: giải mã entity, bỏ mọi ký tự điều khiển/khoảng trắng, không phân biệt hoa/thường.
 *
 * Một nguồn cho cả Operations Engine (chặn op `update` mang link độc, WEBSITE-002 R7) lẫn bộ fixture dùng chung
 * (`tests/helpers/reference-tree-validator.ts` import lại hàm này). BE có bản .NET tương đương chạy cùng fixture;
 * BE vẫn là nguồn sự thật — hàm này chỉ để FE từ chối sớm.
 */
export function isSafeLinkUrl(url: unknown): boolean {
  if (typeof url !== 'string') return false;
  const decoded = url
    .replace(/&#x([0-9a-f]+);?/gi, (_m, hex: string) => String.fromCodePoint(parseInt(hex, 16)))
    .replace(/&#(\d+);?/g, (_m, dec: string) => String.fromCodePoint(parseInt(dec, 10)))
    .replace(/&([a-z]+);/gi, (m, name: string) => NAMED_URL_ENTITIES[name.toLowerCase()] ?? m);
  // eslint-disable-next-line no-control-regex
  const normalized = decoded.replace(/[\x00-\x20\x7f]+/g, '').toLowerCase();
  const scheme = /^([a-z][a-z0-9+.-]*):/.exec(normalized)?.[1];
  if (scheme !== undefined) return ['https', 'http', 'mailto', 'tel'].includes(scheme);
  if (normalized.startsWith('#')) return true;
  return normalized.startsWith('/') && !normalized.startsWith('//') && !normalized.startsWith('/\\');
}
