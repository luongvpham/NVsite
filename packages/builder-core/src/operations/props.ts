import { getPropsSchema, isSafeLinkUrl, treeRules } from '@vsite/builder-components';
import bindingSources from '../../../../config/binding-sources.json';
import type { PropPathSegment } from '../tree/types';
import { OperationErrorCode } from './result';

/** Một mục của `generated/tree-rules.json` (bản đồ prop theo type). */
export interface PropRule {
  kind: string;
  profile?: string;
  sources?: string[];
  itemProps?: Record<string, PropRule>;
}

export interface TypeRule {
  variants: string[];
  props: Record<string, PropRule>;
}

const types = (treeRules as unknown as { types: Record<string, TypeRule> }).types;
const globalBindingSources: readonly string[] = bindingSources;

export function typeRuleOf(type: string): TypeRule | undefined {
  return Object.hasOwn(types, type) ? types[type] : undefined;
}

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);

const ownRule = (rules: Record<string, PropRule> | undefined, name: string): PropRule | undefined =>
  rules && Object.hasOwn(rules, name) ? rules[name] : undefined;

/** Kiểu giá trị là object tự do bên trong (ảnh/liên kết/nguồn dữ liệu): cho phép đi sâu thêm segment, nội dung do kiểm sau. */
const OPAQUE_OBJECT_KINDS = new Set(['image', 'link', 'binding']);

/**
 * Đường dẫn `path` có nằm trong bản đồ prop của `type` không (kể cả trong `group`/`list`) — mã `PROP_UNKNOWN`.
 * Zod props không `.strict()` (strip khoá lạ) nên không tự bắt được.
 */
export function isKnownPropPath(type: string, path: readonly PropPathSegment[]): boolean {
  const rule = typeRuleOf(type);
  if (!rule) return false;
  const [first, ...rest] = path;
  if (typeof first !== 'string') return false;
  let current = ownRule(rule.props, first);
  if (!current) return false;

  let i = 0;
  while (i < rest.length) {
    const segment = rest[i];
    if (segment === undefined) return false;
    if (OPAQUE_OBJECT_KINDS.has(current.kind)) return true;
    if (current.kind === 'group') {
      if (typeof segment !== 'string') return false;
      const next = ownRule(current.itemProps, segment);
      if (!next) return false;
      current = next;
      i += 1;
    } else if (current.kind === 'list') {
      if (typeof segment !== 'number') return false;
      i += 1;
      const field = rest[i];
      if (field === undefined) return true; // chính phần tử mảng
      if (typeof field !== 'string') return false;
      const next = ownRule(current.itemProps, field);
      if (!next) return false;
      current = next;
      i += 1;
    } else {
      return false; // prop vô hướng không có con
    }
  }
  return true;
}

/**
 * Kiểm một giá trị prop theo `rule` ở mọi độ sâu: khoá lạ, nguồn binding (cấm `Review`), url link độc.
 * Trả mã lỗi đầu tiên gặp hoặc `null`. Phản chiếu walker của hiện thực tham chiếu
 * (`tests/helpers/reference-tree-validator.ts`) — cùng fixture chứng minh hai bên đồng ý.
 *
 * `image` KHÔNG kiểm ở đây (cần DB biết id Library/Clone/shop khác — chỉ BE). `richText` cũng không: BE từ chối nội dung
 * nguy hiểm, FE làm sạch ở control Lexical (F10); bộ phân loại hiện chỉ tồn tại trong test.
 */
export function checkPropValue(rule: PropRule, value: unknown): string | null {
  if (value === undefined || value === null) return null;
  switch (rule.kind) {
    case 'binding': {
      const source = isRecord(value) ? value.source : undefined;
      const declared = rule.sources ?? [];
      if (typeof source !== 'string' || !globalBindingSources.includes(source) || !declared.includes(source)) {
        return OperationErrorCode.BindingSourceNotAllowed;
      }
      return null;
    }
    case 'link':
      // Kiểm url bất cứ khi nào khoá `url` có mặt (không phụ thuộc kind); riêng `external` bắt buộc có url hợp lệ.
      if (isRecord(value) && (value.kind === 'external' || 'url' in value) && !isSafeLinkUrl(value.url)) {
        return OperationErrorCode.LinkUrlUnsafe;
      }
      return null;
    case 'group':
      return isRecord(value) && rule.itemProps ? checkFields(rule.itemProps, value) : null;
    case 'list': {
      if (!Array.isArray(value) || !rule.itemProps) return null;
      for (const item of value as unknown[]) {
        const error = isRecord(item) ? checkFields(rule.itemProps, item) : null;
        if (error) return error;
      }
      return null;
    }
    default:
      return null;
  }
}

function checkFields(rules: Record<string, PropRule>, value: Record<string, unknown>): string | null {
  for (const [name, fieldValue] of Object.entries(value)) {
    const fieldRule = ownRule(rules, name);
    if (!fieldRule) return OperationErrorCode.PropUnknown;
    const error = checkPropValue(fieldRule, fieldValue);
    if (error) return error;
  }
  return null;
}

/**
 * Kiểm `props` của một node: khoá lạ → shape Zod (`PROP_INVALID`) → luật theo kind. `only` giới hạn vào MỘT prop cấp 1
 * (dùng cho `update`: prop khác đã sai sẵn không được chặn việc sửa prop đang làm).
 */
export function checkProps(type: string, props: Record<string, unknown>, only?: string): string | null {
  const rule = typeRuleOf(type);
  if (!rule) return OperationErrorCode.NodeTypeUnknown;

  const names = only === undefined ? Object.keys(props) : [only];
  for (const name of names) {
    if (!ownRule(rule.props, name)) return OperationErrorCode.PropUnknown;
  }
  for (const name of names) {
    const propRule = ownRule(rule.props, name);
    const error = propRule ? checkPropValue(propRule, props[name]) : null;
    if (error) return error;
  }

  const schema = getPropsSchema(type);
  if (schema) {
    const parsed = schema.safeParse(props);
    if (!parsed.success && parsed.error.issues.some((issue) => only === undefined || issue.path[0] === only)) {
      return OperationErrorCode.PropInvalid;
    }
  }
  return null;
}
