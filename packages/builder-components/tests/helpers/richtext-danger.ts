import sanitizeProfilesJson from '../../../../config/sanitize-profiles.json';

/**
 * Bộ phân loại "nguy hiểm" CHỈ DÙNG TRONG TEST — kiểm tra nhãn `expect` trong fixture xss-richtext.json có
 * nhất quán với danh sách `dangerous` ở config/sanitize-profiles.json (#67, plan §4 "richText").
 *
 * KHÔNG phải hiện thực của BE: BE phân loại bằng SỰ KIỆN của sanitizer (thứ bị cắt), không so chuỗi
 * (plan R5). Bộ này chạy trên HTML thô nên chỉ đúng với vector không phụ thuộc cách parser tách thẻ
 * (vector phụ thuộc parser đánh dấu `parserDependent` trong fixture và bị bỏ qua).
 */

interface DangerousConfig {
  tags: string[];
  attrPrefixes: string[];
  schemes: string[];
}

export const dangerous = (sanitizeProfilesJson as unknown as { dangerous: DangerousConfig }).dangerous;

const NAMED_ENTITIES: Record<string, string> = { colon: ':', tab: '\t', newline: '\n', lpar: '(', rpar: ')' };

/** Giải mã entity số/tên thường gặp trong URL để bắt `jav&#x09;ascript&colon;`. */
function decodeEntities(value: string): string {
  return value
    .replace(/&#x([0-9a-f]+);?/gi, (_m, hex: string) => String.fromCodePoint(parseInt(hex, 16)))
    .replace(/&#(\d+);?/g, (_m, dec: string) => String.fromCodePoint(parseInt(dec, 10)))
    .replace(/&([a-z]+);/gi, (m, name: string) => NAMED_ENTITIES[name.toLowerCase()] ?? m);
}

export type DangerClass = 'unsafe' | 'safe';

export function classifyDangerous(html: string): DangerClass {
  const tagRe = new RegExp(`<\\s*(?:${dangerous.tags.join('|')})(?=[\\s/>])`, 'i');
  if (tagRe.test(html)) return 'unsafe';

  const attrRe = new RegExp(`<[a-z][^>]*?\\s(?:${dangerous.attrPrefixes.join('|')})[a-z]*\\s*=`, 'i');
  if (attrRe.test(html)) return 'unsafe';

  const urlAttrRe = /\b(?:href|src|action|formaction|xlink:href)\s*=\s*("[^"]*"|'[^']*'|[^\s>]+)/gi;
  for (const match of html.matchAll(urlAttrRe)) {
    const raw = (match[1] ?? '').replace(/^["']|["']$/g, '');
    // eslint-disable-next-line no-control-regex
    const normalized = decodeEntities(raw).replace(/[\u0000- ]+/g, '').toLowerCase();
    if (dangerous.schemes.some((scheme) => normalized.startsWith(`${scheme}:`))) return 'unsafe';
  }
  return 'safe';
}
