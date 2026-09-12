/**
 * 12 kind đóng cho PropDef (07-component-manifest-schema.md §4.3, Quyết định #63).
 * KHÔNG thêm kind mới khi viết component thường — đó là việc sửa framework.
 */

interface PropDefBase {
  label: string;
  help?: string;
  /** Nhóm collapse trong Inspector: 'Nội dung' | 'Hiển thị' | 'Dữ liệu' | '_' (lồng trong group/list) */
  group: string;
  order: number;
  /** 05 §11 — prop này có sửa được trên trang System không (#46) */
  editableInSystemPage: boolean;
  since: string;
  /** Đa ngôn ngữ cho website shop (05 §25 #2) — chưa dùng ở Bước 2, thêm sẵn vì additive-safe. */
  localizable?: boolean;
}

export interface TextPropDef extends PropDefBase {
  kind: 'text';
  maxLength?: number;
  multiline?: boolean;
  default?: string;
}

export interface RichTextPropDef extends PropDefBase {
  kind: 'richText';
  /** Key trong config/sanitize-profiles.json (#67) */
  profile: string;
  maxLength?: number;
}

export interface NumberPropDef extends PropDefBase {
  kind: 'number';
  min?: number;
  max?: number;
  step?: number;
  unit?: string;
  default?: number;
}

export interface BooleanPropDef extends PropDefBase {
  kind: 'boolean';
  default?: boolean;
}

export interface SelectPropDef extends PropDefBase {
  kind: 'select';
  options: Array<{ value: string; label: string }>;
  default?: string;
}

export interface ColorPropDef extends PropDefBase {
  kind: 'color';
  allowCustom: boolean;
  default?: string;
}

export interface ImagePropDef extends PropDefBase {
  kind: 'image';
  /** BẮT BUỘC, phải nằm trong config/image-presets.json (#64) */
  preset: string;
  allowFocalPoint?: boolean;
}

export interface IconPropDef extends PropDefBase {
  kind: 'icon';
  set: 'lucide';
}

export type LinkKind = 'page' | 'systemPage' | 'productCategory' | 'external' | 'anchor';

export interface LinkPropDef extends PropDefBase {
  kind: 'link';
  allowKinds: LinkKind[];
}

export interface BindingPropDef extends PropDefBase {
  kind: 'binding';
  /** Phải là tập con của config/binding-sources.json — "Review" không có trong whitelist (#65) */
  sources: string[];
  allowFilters?: string[];
}

/** Props hợp lệ làm item của một `list` — list không được lồng list (§4.3). */
export type NonListPropDef =
  | TextPropDef
  | RichTextPropDef
  | NumberPropDef
  | BooleanPropDef
  | SelectPropDef
  | ColorPropDef
  | ImagePropDef
  | IconPropDef
  | LinkPropDef
  | GroupPropDef
  | BindingPropDef;

export interface ListPropDef extends PropDefBase {
  kind: 'list';
  itemProps: Record<string, NonListPropDef>;
  itemLabel?: string;
  minItems?: number;
  maxItems?: number;
}

export interface GroupPropDef extends PropDefBase {
  kind: 'group';
  props: Record<string, PropDef>;
}

export type PropDef =
  | TextPropDef
  | RichTextPropDef
  | NumberPropDef
  | BooleanPropDef
  | SelectPropDef
  | ColorPropDef
  | ImagePropDef
  | IconPropDef
  | LinkPropDef
  | ListPropDef
  | GroupPropDef
  | BindingPropDef;

export type PropKind = PropDef['kind'];

export const PROP_KINDS: readonly PropKind[] = [
  'text',
  'richText',
  'number',
  'boolean',
  'select',
  'color',
  'image',
  'icon',
  'link',
  'list',
  'group',
  'binding',
];
