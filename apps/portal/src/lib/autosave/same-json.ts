/**
 * So hai giá trị kiểu JSON theo cấu trúc: object không phụ thuộc thứ tự key, mảng theo thứ tự; key mang `undefined`
 * coi như không có, phần tử `undefined` trong mảng coi như `null` (đúng như JSON.stringify khi gửi); `null` khác key vắng. Dùng để biết cây GET về có đúng là cây vừa lưu không.
 */
export function sameJson(a: unknown, b: unknown): boolean {
  if (a === b) return true;
  if (typeof a !== 'object' || typeof b !== 'object' || a === null || b === null) return false;
  if (Array.isArray(a) || Array.isArray(b)) {
    if (!Array.isArray(a) || !Array.isArray(b) || a.length !== b.length) return false;
    // Như JSON.stringify: phần tử `undefined` trong mảng thành `null`.
    return a.every((item: unknown, i) => sameJson(item ?? null, (b[i] as unknown) ?? null));
  }
  const ra = a as Record<string, unknown>;
  const rb = b as Record<string, unknown>;
  const keysA = Object.keys(ra).filter((k) => ra[k] !== undefined);
  const keysB = Object.keys(rb).filter((k) => rb[k] !== undefined);
  if (keysA.length !== keysB.length) return false;
  return keysA.every((k) => Object.prototype.hasOwnProperty.call(rb, k) && sameJson(ra[k], rb[k]));
}
