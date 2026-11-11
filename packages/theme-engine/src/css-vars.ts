import themeTokensConfig from '../../../config/theme-tokens.json';

/**
 * Theme per-shop → CSS variables (TOOLING-001 #1, #97, `05` §3). `config/theme-tokens.json` là MỘT nguồn tập giá trị
 * cho phép (FE + BE đọc cùng file); file này ánh xạ chúng sang giá trị CSS.
 *
 * Hàm thuần, isomorphic (#23): không `window`/`document`, dùng được ở canvas editor (portal) lẫn SSR (`apps/web`).
 * **Chỉ whitelist**: mọi giá trị đi vào CSS phải nằm trong tập của config (màu qua `colorPattern`) — chuỗi lạ không
 * bao giờ xuất hiện trong kết quả, nên không thể chèn CSS qua `tokens`.
 */
export interface ThemeTokens {
  schemaVersion: number;
  colors: Record<string, string>;
  typography: { headingFont: string; bodyFont: string; scale: string };
  shape: { radius: string; shadow: string };
  spacing: { sectionGap: string };
}

/** Biến màu sinh theo `colorKeys` của config (mở); các biến còn lại cố định nên kiểu là `string` chứ không `string | undefined`. */
export interface ThemeVars {
  [name: `--vs-color-${string}`]: string;
  '--vs-font-heading': string;
  '--vs-font-body': string;
  '--vs-font-scale': string;
  '--vs-radius': string;
  '--vs-shadow': string;
  '--vs-section-gap': string;
}
type FixedVarName = Exclude<keyof ThemeVars, `--vs-color-${string}`>;

interface ThemeConfig {
  colorKeys: readonly string[];
  colorPattern: string;
  fonts: readonly string[];
  scale: readonly string[];
  radius: readonly string[];
  shadow: readonly string[];
  sectionGap: readonly string[];
  defaults: ThemeTokens;
}

const config = themeTokensConfig as unknown as ThemeConfig;
const defaults = config.defaults;
const COLOR_PATTERN = new RegExp(config.colorPattern);

/** Các key màu của theme (`primary`, `secondary`, … `textMuted`) — Inspector liệt kê, Section.background tham chiếu. */
export const themeColorKeys: readonly string[] = config.colorKeys;

/**
 * Tập giá trị cho phép của từng token (cùng nguồn `config/theme-tokens.json` với BE) — Theme panel dựng control từ đây,
 * không viết tay danh sách thứ hai (#24-tinh thần: một nguồn).
 */
export const themeOptions = {
  colorKeys: config.colorKeys,
  colorPattern: config.colorPattern,
  fonts: config.fonts,
  scale: config.scale,
  radius: config.radius,
  shadow: config.shadow,
  sectionGap: config.sectionGap,
} as const;

const kebab = (key: string): string => key.replace(/[A-Z]/g, (c) => `-${c.toLowerCase()}`);

// ── Bảng ánh xạ giá trị của config → giá trị CSS. Thiếu một giá trị là test `css-vars.test.ts` đỏ. ──
const RADIUS: Record<string, string> = { none: '0', sm: '0.25rem', md: '0.5rem', lg: '1rem', full: '9999px' };
const SHADOW: Record<string, string> = {
  none: 'none',
  soft: '0 1px 3px rgb(0 0 0 / 0.08)',
  medium: '0 4px 12px rgb(0 0 0 / 0.12)',
  strong: '0 10px 30px rgb(0 0 0 / 0.2)',
};
const SECTION_GAP: Record<string, string> = { sm: '1.5rem', md: '3rem', lg: '4rem', xl: '6rem' };
const FONT_SCALE: Record<string, string> = { compact: '0.9375', comfortable: '1', spacious: '1.0625' };
const SERIF_FONTS = new Set(['Playfair Display', 'Lora']);

const fontStack = (name: string): string => `'${name}', ${SERIF_FONTS.has(name) ? 'Georgia, serif' : 'system-ui, sans-serif'}`;

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);

/** Đọc khoá CỦA CHÍNH object (không đi lên prototype — `__proto__`/`constructor` không lọt vào). */
const own = (value: unknown, key: string): unknown => (isRecord(value) && Object.hasOwn(value, key) ? value[key] : undefined);

const oneOf = (allowed: readonly string[], value: unknown, fallback: string): string =>
  typeof value === 'string' && allowed.includes(value) ? value : fallback;

const lookup = (table: Record<string, string>, key: string, fallbackKey: string): string =>
  table[key] ?? table[fallbackKey] ?? '';

/**
 * Bộ biến CSS đầy đủ của một theme. Đầu vào sai/thiếu (chưa nạp, đang gõ dở, hỏng) **không ném lỗi**: từng phần sai rơi về
 * giá trị mặc định của chính nó, nên kết quả luôn đủ bộ và deterministic.
 */
export function tokensToCssVars(tokens?: unknown): ThemeVars {
  const colors = own(tokens, 'colors');
  const typography = own(tokens, 'typography');
  const shape = own(tokens, 'shape');
  const spacing = own(tokens, 'spacing');

  const colorVars: Record<`--vs-color-${string}`, string> = {};
  for (const key of config.colorKeys) {
    const value = own(colors, key);
    colorVars[`--vs-color-${kebab(key)}`] =
      typeof value === 'string' && COLOR_PATTERN.test(value) ? value : (defaults.colors[key] ?? '');
  }

  return {
    ...colorVars,
    '--vs-font-heading': fontStack(oneOf(config.fonts, own(typography, 'headingFont'), defaults.typography.headingFont)),
    '--vs-font-body': fontStack(oneOf(config.fonts, own(typography, 'bodyFont'), defaults.typography.bodyFont)),
    '--vs-font-scale': lookup(FONT_SCALE, oneOf(config.scale, own(typography, 'scale'), defaults.typography.scale), defaults.typography.scale),
    '--vs-radius': lookup(RADIUS, oneOf(config.radius, own(shape, 'radius'), defaults.shape.radius), defaults.shape.radius),
    '--vs-shadow': lookup(SHADOW, oneOf(config.shadow, own(shape, 'shadow'), defaults.shape.shadow), defaults.shape.shadow),
    '--vs-section-gap': lookup(
      SECTION_GAP,
      oneOf(config.sectionGap, own(spacing, 'sectionGap'), defaults.spacing.sectionGap),
      defaults.spacing.sectionGap,
    ),
  };
}

/** Bộ biến của theme mặc định (`defaults` trong config). */
export const defaultThemeVars: ThemeVars = tokensToCssVars(defaults);

/** `var(--vs-color-<key>, <màu mặc định>)`. Key lạ/thiếu (kể cả key cũ của theme tĩnh: `muted`, `foreground`) → `background`. */
export function colorVar(key?: string, fallbackKey = 'background'): string {
  const resolved = typeof key === 'string' && config.colorKeys.includes(key) ? key : fallbackKey;
  return `var(--vs-color-${kebab(resolved)}, ${defaults.colors[resolved] ?? ''})`;
}

// Hằng var cho component: giá trị dự phòng = mặc định nên component vẫn đúng khi chưa ai đặt biến (SSR, test, Storybook).
const withFallback = (name: FixedVarName): string => `var(${name}, ${defaultThemeVars[name]})`;
export const radiusVar = withFallback('--vs-radius');
export const shadowVar = withFallback('--vs-shadow');
export const sectionGapVar = withFallback('--vs-section-gap');
export const headingFontVar = withFallback('--vs-font-heading');
export const bodyFontVar = withFallback('--vs-font-body');

/**
 * Style gốc của khung chứa trang (canvas ở portal; site ở `apps/web`): mọi biến của theme + nền/chữ/font/cỡ chữ dựa trên
 * biến. Trả `Record<string, string>` (không phụ thuộc React) — nơi dùng ép sang `CSSProperties`.
 */
export function themeStyle(tokens?: unknown): Record<string, string> {
  return {
    ...tokensToCssVars(tokens),
    color: 'var(--vs-color-text)',
    backgroundColor: 'var(--vs-color-background)',
    fontFamily: 'var(--vs-font-body)',
    fontSize: 'calc(1rem * var(--vs-font-scale))',
  };
}
