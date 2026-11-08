import { describe, expect, it } from 'vitest';
import themeTokensConfig from '../../../config/theme-tokens.json';
import {
  bodyFontVar,
  colorVar,
  defaultThemeVars,
  headingFontVar,
  radiusVar,
  sectionGapVar,
  shadowVar,
  themeColorKeys,
  themeStyle,
  tokensToCssVars,
} from './css-vars';

/**
 * Theme per-shop đi qua CSS variables (TOOLING-001 #1, #97): `config/theme-tokens.json` là MỘT nguồn tập giá trị cho phép
 * (FE + BE), file này ánh xạ chúng sang giá trị CSS. Hàm thuần, isomorphic (#23) — test chạy ở môi trường Node.
 */
const config = themeTokensConfig;
const defaults = config.defaults;

describe('môi trường', () => {
  it('chạy ở Node, không có window/document (isomorphic, #23)', () => {
    expect(typeof globalThis.window).toBe('undefined');
    expect(typeof globalThis.document).toBe('undefined');
  });
});

describe('tokensToCssVars — defaults của config', () => {
  const vars = tokensToCssVars(defaults);

  it('đủ biến: mỗi key màu của config, font heading/body, scale, radius, shadow, section gap', () => {
    const expected = [
      ...config.colorKeys.map((key) => `--vs-color-${key.replace(/[A-Z]/g, (c) => `-${c.toLowerCase()}`)}`),
      '--vs-font-heading',
      '--vs-font-body',
      '--vs-font-scale',
      '--vs-radius',
      '--vs-shadow',
      '--vs-section-gap',
    ];
    expect(Object.keys(vars).sort()).toEqual(expected.sort());
  });

  it('màu giữ nguyên giá trị hex; camelCase → kebab-case (textMuted → --vs-color-text-muted)', () => {
    expect(vars['--vs-color-primary']).toBe(defaults.colors.primary);
    expect(vars['--vs-color-text-muted']).toBe(defaults.colors.textMuted);
    expect(vars['--vs-color-background']).toBe(defaults.colors.background);
  });

  it('font là danh sách font-family có tên đã trích dẫn + họ chữ dự phòng', () => {
    expect(vars['--vs-font-heading']).toBe("'Be Vietnam Pro', system-ui, sans-serif");
    expect(vars['--vs-font-body']).toBe("'Inter', system-ui, sans-serif");
  });

  it('defaultThemeVars chính là kết quả của defaults', () => {
    expect(defaultThemeVars).toEqual(vars);
  });
});

describe('ánh xạ phủ HẾT tập giá trị của config (thêm giá trị mới vào config mà quên ánh xạ → test đỏ)', () => {
  const only = (patch: Record<string, unknown>, section: 'typography' | 'shape' | 'spacing') => ({
    ...defaults,
    [section]: { ...defaults[section], ...patch },
  });

  it.each(config.radius)('radius %s', (radius) => {
    const value = tokensToCssVars(only({ radius }, 'shape'))['--vs-radius'];
    expect(value).toMatch(/^(0|\d+(\.\d+)?rem|9999px)$/);
  });

  it('mỗi radius cho một giá trị khác nhau', () => {
    const values = config.radius.map((radius) => tokensToCssVars(only({ radius }, 'shape'))['--vs-radius']);
    expect(new Set(values).size).toBe(config.radius.length);
  });

  it.each(config.shadow)('shadow %s', (shadow) => {
    const value = tokensToCssVars(only({ shadow }, 'shape'))['--vs-shadow'];
    expect(value === 'none' || value.startsWith('0 ')).toBe(true);
  });

  it('mỗi shadow cho một giá trị khác nhau', () => {
    const values = config.shadow.map((shadow) => tokensToCssVars(only({ shadow }, 'shape'))['--vs-shadow']);
    expect(new Set(values).size).toBe(config.shadow.length);
  });

  it.each(config.sectionGap)('sectionGap %s', (sectionGap) => {
    expect(tokensToCssVars(only({ sectionGap }, 'spacing'))['--vs-section-gap']).toMatch(/^\d+(\.\d+)?rem$/);
  });

  it('sectionGap tăng dần theo thứ tự sm < md < lg < xl', () => {
    const rem = (sectionGap: string) => parseFloat(tokensToCssVars(only({ sectionGap }, 'spacing'))['--vs-section-gap']);
    expect(rem('sm')).toBeLessThan(rem('md'));
    expect(rem('md')).toBeLessThan(rem('lg'));
    expect(rem('lg')).toBeLessThan(rem('xl'));
  });

  it.each(config.scale)('scale %s', (scale) => {
    expect(Number(tokensToCssVars(only({ scale }, 'typography'))['--vs-font-scale'])).toBeGreaterThan(0.5);
  });

  it('scale tăng dần: compact < comfortable < spacious', () => {
    const num = (scale: string) => Number(tokensToCssVars(only({ scale }, 'typography'))['--vs-font-scale']);
    expect(num('compact')).toBeLessThan(num('comfortable'));
    expect(num('comfortable')).toBeLessThan(num('spacious'));
  });

  it.each(config.fonts)('font %s', (headingFont) => {
    const value = tokensToCssVars(only({ headingFont }, 'typography'))['--vs-font-heading'];
    expect(value.startsWith(`'${headingFont}', `)).toBe(true);
  });

  it('font có chân (Playfair Display, Lora) dùng họ serif dự phòng, còn lại sans-serif', () => {
    const stack = (headingFont: string) => tokensToCssVars(only({ headingFont }, 'typography'))['--vs-font-heading'];
    expect(stack('Playfair Display')).toMatch(/serif$/);
    expect(stack('Lora')).toMatch(/serif$/);
    expect(stack('Roboto')).toMatch(/sans-serif$/);
  });
});

describe('tokensToCssVars — đầu vào sai/thiếu không ném lỗi, rơi về defaults từng phần', () => {
  it.each([[undefined], [null], ['xanh'], [42], [[]], [{}]])('đầu vào %j → đúng bộ mặc định', (input) => {
    expect(tokensToCssVars(input)).toEqual(defaultThemeVars);
  });

  it('màu sai pattern → dùng màu mặc định của key đó, key khác giữ nguyên', () => {
    const vars = tokensToCssVars({ colors: { primary: 'tím', secondary: '#123456' } });
    expect(vars['--vs-color-primary']).toBe(defaults.colors.primary);
    expect(vars['--vs-color-secondary']).toBe('#123456');
  });

  it('giá trị ngoài tập cho phép (radius/shadow/font/scale/gap) → mặc định', () => {
    const vars = tokensToCssVars({
      typography: { headingFont: 'Comic Sans', scale: 'huge' },
      shape: { radius: 'round', shadow: 'glow' },
      spacing: { sectionGap: 'xxl' },
    });
    expect(vars).toEqual(defaultThemeVars);
  });

  it('khoá lạ và phần dư bị bỏ qua', () => {
    expect(tokensToCssVars({ ...defaults, extra: { a: 1 }, colors: { ...defaults.colors, neon: '#ff00ff' } })).toEqual(defaultThemeVars);
  });

  it('KHÔNG cho chèn CSS: giá trị không qua whitelist không bao giờ xuất hiện trong kết quả', () => {
    const evil = '#fff;} body{display:none';
    const vars = tokensToCssVars({
      colors: { primary: evil },
      typography: { headingFont: "x'; background:url(//evil)" },
      shape: { radius: '0;position:fixed' },
    });
    const dump = JSON.stringify(vars);
    expect(dump).not.toContain('evil');
    expect(dump).not.toContain('display:none');
    expect(dump).not.toContain('position:fixed');
  });

  it('khoá nguy hiểm kiểu __proto__/constructor không làm hỏng và không rò rỉ', () => {
    const polluted = JSON.parse('{"colors":{"__proto__":{"primary":"#000000"}},"typography":{"constructor":"x"}}') as unknown;
    expect(tokensToCssVars(polluted)).toEqual(defaultThemeVars);
  });
});

describe('colorVar — Section.background là KEY màu, không phải mã hex', () => {
  it('key hợp lệ → var(--vs-color-<kebab>, <mặc định>)', () => {
    expect(colorVar('primary')).toBe(`var(--vs-color-primary, ${defaults.colors.primary})`);
    expect(colorVar('textMuted')).toBe(`var(--vs-color-text-muted, ${defaults.colors.textMuted})`);
  });

  it('key lạ / key cũ của theme tĩnh (muted, foreground) / thiếu → rơi về background', () => {
    const background = `var(--vs-color-background, ${defaults.colors.background})`;
    for (const key of ['muted', 'foreground', 'x', '', undefined, '__proto__', 'constructor']) {
      expect(colorVar(key), String(key)).toBe(background);
    }
  });

  it('themeColorKeys đúng bằng colorKeys của config', () => {
    expect(themeColorKeys).toEqual(config.colorKeys);
  });
});

describe('hằng var cho component', () => {
  it('có giá trị dự phòng = mặc định, để component vẫn đẹp khi chưa ai đặt biến (SSR, test, Storybook)', () => {
    expect(radiusVar).toBe(`var(--vs-radius, ${defaultThemeVars['--vs-radius']})`);
    expect(shadowVar).toBe(`var(--vs-shadow, ${defaultThemeVars['--vs-shadow']})`);
    expect(sectionGapVar).toBe(`var(--vs-section-gap, ${defaultThemeVars['--vs-section-gap']})`);
    expect(headingFontVar).toBe(`var(--vs-font-heading, ${defaultThemeVars['--vs-font-heading']})`);
    expect(bodyFontVar).toBe(`var(--vs-font-body, ${defaultThemeVars['--vs-font-body']})`);
  });
});

describe('themeStyle — style gốc của khung chứa trang (canvas ở portal, site ở apps/web)', () => {
  it('gồm mọi biến + kiểu nền dựa trên biến (chữ, nền, font body, cỡ chữ theo scale)', () => {
    const style = themeStyle(defaults);
    expect(style['--vs-color-primary']).toBe(defaults.colors.primary);
    expect(style.color).toBe('var(--vs-color-text)');
    expect(style.backgroundColor).toBe('var(--vs-color-background)');
    expect(style.fontFamily).toBe('var(--vs-font-body)');
    expect(style.fontSize).toBe('calc(1rem * var(--vs-font-scale))');
  });

  it('đổi theme → đổi biến', () => {
    const style = themeStyle({ ...defaults, colors: { ...defaults.colors, primary: '#112233' } });
    expect(style['--vs-color-primary']).toBe('#112233');
  });
});
