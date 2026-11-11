import type { PropPathSegment } from '../tree/types';

const FORBIDDEN_SEGMENTS = new Set(['__proto__', 'constructor', 'prototype']);

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);

/**
 * Đặt `value` tại `path` — **immutable**: chỉ sao chép các tầng trên đường đi, phần còn lại giữ nguyên tham chiếu
 * (structural sharing). Tạo object/mảng trung gian theo kiểu của segment kế (số → mảng). `value === undefined` xoá
 * khoá (hoặc gỡ phần tử mảng); nếu đường đi không tồn tại thì không làm gì và trả đúng `root`.
 *
 * Phiên bản Inspector (`setAtPath`) không xoá được khoá nên Engine có hàm riêng.
 */
export function setIn(root: unknown, path: readonly PropPathSegment[], value: unknown): unknown {
  const [head, ...rest] = path;
  if (head === undefined) return value;
  if (typeof head === 'string' && FORBIDDEN_SEGMENTS.has(head)) {
    throw new Error(`setIn: segment '${head}' không được phép`);
  }

  if (typeof head === 'number') {
    const list: unknown[] = Array.isArray(root) ? [...(root as unknown[])] : [];
    if (rest.length === 0) {
      if (value === undefined) {
        if (head >= list.length) return root;
        list.splice(head, 1);
      } else {
        list[head] = value;
      }
      return list;
    }
    const child = list[head];
    if (child === undefined && value === undefined) return root;
    list[head] = setIn(child, rest, value);
    return list;
  }

  const record: Record<string, unknown> = isRecord(root) ? { ...root } : {};
  if (rest.length === 0) {
    if (value === undefined) {
      if (!Object.hasOwn(record, head)) return root;
      return Object.fromEntries(Object.entries(record).filter(([key]) => key !== head));
    } else {
      record[head] = value;
    }
    return record;
  }
  const child = record[head];
  if (child === undefined && value === undefined) return root;
  record[head] = setIn(child, rest, value);
  return record;
}
