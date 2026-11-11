import type { ZodTypeAny } from 'zod';
import * as generated from '../generated/props-schemas';

const schemas = generated as unknown as Record<string, ZodTypeAny | undefined>;

/**
 * Zod props của một component type (`Hero` → `heroPropsSchema`), tra theo tên để thêm type mới trong registry
 * không phải sửa file này — test `public-exports` chốt "mọi type của opRules đều có schema".
 *
 * Schema chỉ kiểm SHAPE (mọi prop optional, object không `.strict()`): dùng `safeParse` để kiểm, **đừng** dùng
 * `.data` thay props gốc — Zod strip key lạ và `binding` không có khoá `source` trong schema (`requiresProps` và
 * binding source được Engine/BE kiểm riêng).
 */
export function getPropsSchema(type: string): ZodTypeAny | undefined {
  if (!/^[A-Z][A-Za-z0-9]*$/.test(type)) return undefined;
  const key = `${type.charAt(0).toLowerCase()}${type.slice(1)}PropsSchema`;
  return Object.hasOwn(schemas, key) ? schemas[key] : undefined;
}
