import { themeOptions } from '@vsite/theme-engine';
import type { TreeValidationError } from '@vsite/shared';

/** Một ô của Theme panel, định danh bằng đường dẫn trong `tokens` (vd. `colors.primary`). */
export type ThemeFieldKey = string;

const COLOR_RE = new RegExp(themeOptions.colorPattern);

export const isValidThemeColor = (value: string): boolean => COLOR_RE.test(value);

const isRecord = (v: unknown): v is Record<string, unknown> => typeof v === 'object' && v !== null && !Array.isArray(v);

/** Đọc một giá trị theo `a.b.c`; thiếu → `undefined`. */
export function getThemeValue(tokens: unknown, key: ThemeFieldKey): unknown {
  let cur: unknown = tokens;
  for (const part of key.split('.')) {
    if (!isRecord(cur)) return undefined;
    cur = cur[part];
  }
  return cur;
}

/** Trả bản sao tokens với một giá trị đã đổi (không đụng bản gốc). `key` đúng hai cấp: `nhóm.tên`. */
export function setThemeValue(tokens: unknown, key: ThemeFieldKey, value: string): Record<string, unknown> {
  const [group, name] = key.split('.');
  const base = isRecord(tokens) ? tokens : {};
  if (!group || !name) return { ...base };
  const groupValue = isRecord(base[group]) ? base[group] : {};
  return { ...base, [group]: { ...groupValue, [name]: value } };
}

/** So sánh sâu hai giá trị JSON (không phụ thuộc thứ tự khoá) — "chưa lưu" = khác bản đã lưu. */
export function themeEquals(a: unknown, b: unknown): boolean {
  if (a === b) return true;
  if (Array.isArray(a) && Array.isArray(b)) {
    return a.length === b.length && a.every((v, i) => themeEquals(v, b[i]));
  }
  if (isRecord(a) && isRecord(b)) {
    const keys = new Set([...Object.keys(a), ...Object.keys(b)]);
    return [...keys].every((k) => themeEquals(a[k], b[k]));
  }
  return false;
}

/** JSON Pointer của BE (`/colors/primary`) → khoá ô (`colors.primary`). `''` (gốc) → `undefined`. */
export function pointerToFieldKey(pointer: string): ThemeFieldKey | undefined {
  const parts = pointer.split('/').filter((p) => p !== '');
  if (parts.length === 0) return undefined;
  return parts.map((p) => p.replace(/~1/g, '/').replace(/~0/g, '~')).join('.');
}

export interface MappedThemeErrors {
  /** Lỗi gắn được vào một ô (khoá = `colors.primary`…). */
  byField: ReadonlyMap<ThemeFieldKey, string>;
  /** Lỗi không gắn được (path rỗng/không phải ô nào của panel) — hiện ở banner. */
  general: string[];
}

/** 422 `THEME_INVALID` → tô ô theo `errors[].path`. Path không trỏ ô nào của panel → banner (không mất thông tin). */
export function mapThemeErrors(errors: readonly TreeValidationError[], knownFields: ReadonlySet<ThemeFieldKey>): MappedThemeErrors {
  const byField = new Map<ThemeFieldKey, string>();
  const general: string[] = [];
  for (const error of errors) {
    const key = pointerToFieldKey(error.path);
    if (key && knownFields.has(key)) {
      if (!byField.has(key)) byField.set(key, error.message);
    } else {
      general.push(error.message);
    }
  }
  return { byField, general };
}
