import { themeOptions } from '@vsite/theme-engine';
import type { ThemeFieldKey } from './theme-edit';

export type ThemeField =
  | { kind: 'color'; key: ThemeFieldKey; label: string }
  | { kind: 'select'; key: ThemeFieldKey; label: string; options: readonly { value: string; label: string }[] };

export interface ThemeGroup {
  title: string;
  fields: ThemeField[];
}

const COLOR_LABELS: Record<string, string> = {
  primary: 'Màu chính',
  secondary: 'Màu phụ',
  background: 'Nền trang',
  surface: 'Nền khối',
  text: 'Chữ',
  textMuted: 'Chữ phụ',
  border: 'Viền',
};
const SCALE_LABELS: Record<string, string> = { compact: 'Gọn', comfortable: 'Vừa', spacious: 'Rộng' };
const RADIUS_LABELS: Record<string, string> = { none: 'Vuông', sm: 'Nhỏ', md: 'Vừa', lg: 'Lớn', full: 'Tròn hẳn' };
const SHADOW_LABELS: Record<string, string> = { none: 'Không', soft: 'Nhẹ', medium: 'Vừa', strong: 'Đậm' };
const GAP_LABELS: Record<string, string> = { sm: 'Hẹp', md: 'Vừa', lg: 'Rộng', xl: 'Rất rộng' };

const opts = (values: readonly string[], labels: Record<string, string> = {}) =>
  values.map((value) => ({ value, label: labels[value] ?? value }));

/** Tập giá trị lấy từ `config/theme-tokens.json` (qua theme-engine); nhãn tiếng Việt chỉ là chữ hiển thị — thiếu nhãn thì hiện giá trị gốc. */
export const THEME_GROUPS: ThemeGroup[] = [
  {
    title: 'Màu sắc',
    fields: themeOptions.colorKeys.map((key) => ({
      kind: 'color',
      key: `colors.${key}`,
      label: COLOR_LABELS[key] ?? key,
    })),
  },
  {
    title: 'Chữ',
    fields: [
      { kind: 'select', key: 'typography.headingFont', label: 'Font tiêu đề', options: opts(themeOptions.fonts) },
      { kind: 'select', key: 'typography.bodyFont', label: 'Font nội dung', options: opts(themeOptions.fonts) },
      { kind: 'select', key: 'typography.scale', label: 'Cỡ chữ', options: opts(themeOptions.scale, SCALE_LABELS) },
    ],
  },
  {
    title: 'Hình dạng',
    fields: [
      { kind: 'select', key: 'shape.radius', label: 'Bo góc', options: opts(themeOptions.radius, RADIUS_LABELS) },
      { kind: 'select', key: 'shape.shadow', label: 'Đổ bóng', options: opts(themeOptions.shadow, SHADOW_LABELS) },
    ],
  },
  {
    title: 'Khoảng cách',
    fields: [
      { kind: 'select', key: 'spacing.sectionGap', label: 'Giữa các khối', options: opts(themeOptions.sectionGap, GAP_LABELS) },
    ],
  },
];

export const THEME_FIELD_KEYS: ThemeFieldKey[] = THEME_GROUPS.flatMap((g) => g.fields.map((f) => f.key));
