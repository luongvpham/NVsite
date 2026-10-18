import { readFileSync } from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';

const REPO_ROOT = path.resolve(import.meta.dirname, '..', '..', '..');

function readJson(relativePath: string): unknown {
  return JSON.parse(readFileSync(path.join(REPO_ROOT, relativePath), 'utf8'));
}

type ImagePreset = { width: number; height: number; fit: string; usage: string };
type ImagePresetsFile = { presets: Record<string, ImagePreset>; surfaces: Record<string, string[]> };

const EXPECTED_PRESET_KEYS = [
  '1600x900,cover',
  '1600x600,cover',
  '1200x630,cover',
  '1200x1200,inside',
  '800x800,cover',
  '800x600,cover',
  '320x96,inside',
  '160x160,cover',
  '96x96,cover',
];

describe('config whitelist files (§2.1)', () => {
  it('image-presets.json has shape { presets, surfaces } with exactly the 9 expected presets', () => {
    const file = readJson('config/image-presets.json') as ImagePresetsFile;
    expect(Object.keys(file.presets).sort()).toEqual([...EXPECTED_PRESET_KEYS].sort());
  });

  it('image-presets.json presets all use fit ∈ {cover, inside} and a numeric height (no null)', () => {
    const file = readJson('config/image-presets.json') as ImagePresetsFile;
    for (const [key, preset] of Object.entries(file.presets)) {
      expect(['cover', 'inside'], `${key}.fit`).toContain(preset.fit);
      expect(typeof preset.height, `${key}.height`).toBe('number');
    }
  });

  it('image-presets.json no longer has the "600xR,cover" preset', () => {
    const file = readJson('config/image-presets.json') as ImagePresetsFile;
    expect(file.presets['600xR,cover']).toBeUndefined();
  });

  it('image-presets.json surfaces reference only presets that exist in presets', () => {
    const file = readJson('config/image-presets.json') as ImagePresetsFile;
    for (const [surface, presetKeys] of Object.entries(file.surfaces)) {
      for (const presetKey of presetKeys) {
        expect(file.presets[presetKey], `surfaces.${surface} -> ${presetKey}`).toBeDefined();
      }
    }
  });

  it('image-presets.json surfaces keys are all valid binding sources', () => {
    const file = readJson('config/image-presets.json') as ImagePresetsFile;
    const sources = readJson('config/binding-sources.json') as string[];
    for (const surface of Object.keys(file.surfaces)) {
      expect(sources, `surfaces key '${surface}'`).toContain(surface);
    }
  });

  it('binding-sources.json is a non-empty array and never contains "Review"', () => {
    const sources = readJson('config/binding-sources.json') as string[];
    expect(Array.isArray(sources)).toBe(true);
    expect(sources.length).toBeGreaterThan(0);
    expect(sources).not.toContain('Review');
  });

  it('binding-sources.json contains "Shop" (#73)', () => {
    const sources = readJson('config/binding-sources.json') as string[];
    expect(sources).toContain('Shop');
  });

  it('sanitize-profiles.json has inline and basic profiles', () => {
    const profiles = readJson('config/sanitize-profiles.json') as Record<string, { tags: string[] }>;
    expect(profiles.inline).toBeDefined();
    expect(profiles.basic).toBeDefined();
    expect(profiles.inline?.tags).toContain('a');
    expect(profiles.basic?.tags).toContain('blockquote');
  });

  // `media` là route phục vụ ảnh trên mọi domain (08 §5) — không shop nào được lấy slug này (#24).
  it('reserved-routes.json reservedPaths contains media (#24)', () => {
    const config = readJson('config/reserved-routes.json') as { reservedPaths: string[] };
    expect(config.reservedPaths).toContain('media');
  });
});

// ---------------------------------------------------------------------------
// WEBSITE-001 S0-T1 — config dùng chung cho Website (#97, #98)
// ---------------------------------------------------------------------------

type ThemeTokensConfig = {
  colorKeys: string[];
  colorPattern: string;
  fonts: string[];
  scale: string[];
  radius: string[];
  shadow: string[];
  sectionGap: string[];
  defaults: {
    schemaVersion: number;
    colors: Record<string, string>;
    typography: { headingFont: string; bodyFont: string; scale: string };
    shape: { radius: string; shadow: string };
    spacing: { sectionGap: string };
  };
};
type SystemPagesConfig = Record<string, { routes: string[]; phase: number; seedOnCreate: boolean }>;

// Mọi SystemType của 05 §4 — kể cả loại chưa seed (slug shop hôm nay không được chiếm route tương lai).
const ALL_SYSTEM_TYPES = ['Contact', 'NotFound', 'ProductListing', 'ProductDetail', 'ProductSearch', 'Booking'];

describe('config/locales.json (#97, #98)', () => {
  it('là mảng phẳng ["vi","en"]', () => {
    expect(readJson('config/locales.json')).toEqual(['vi', 'en']);
  });

  it('mã locale là chữ thường, không trùng nhau (so slug không phân biệt hoa/thường)', () => {
    const locales = readJson('config/locales.json') as string[];
    for (const code of locales) expect(code).toMatch(/^[a-z]{2,3}$/);
    expect(new Set(locales).size).toBe(locales.length);
  });
});

describe('config/theme-tokens.json (#97)', () => {
  const cfg = () => readJson('config/theme-tokens.json') as ThemeTokensConfig;

  it('có đúng 7 colorKeys + các tập giá trị đã chốt (plan §4)', () => {
    const c = cfg();
    expect(c.colorKeys).toEqual(['primary', 'secondary', 'background', 'surface', 'text', 'textMuted', 'border']);
    expect(c.scale).toEqual(['compact', 'comfortable', 'spacious']);
    expect(c.radius).toEqual(['none', 'sm', 'md', 'lg', 'full']);
    expect(c.shadow).toEqual(['none', 'soft', 'medium', 'strong']);
    expect(c.sectionGap).toEqual(['sm', 'md', 'lg', 'xl']);
    expect(c.fonts).toContain('Be Vietnam Pro');
    expect(c.fonts).toContain('Inter');
  });

  it('colorPattern biên dịch được thành RegExp và chấp nhận #rrggbb', () => {
    const re = new RegExp(cfg().colorPattern);
    expect(re.test('#7c3aed')).toBe(true);
    expect(re.test('#7C3AED')).toBe(true);
    expect(re.test('7c3aed')).toBe(false);
    expect(re.test('#fff')).toBe(false);
    expect(re.test('red')).toBe(false);
  });

  it('các tập giá trị không rỗng, không trùng lặp', () => {
    const c = cfg();
    for (const key of ['colorKeys', 'fonts', 'scale', 'radius', 'shadow', 'sectionGap'] as const) {
      expect(c[key].length, key).toBeGreaterThan(0);
      expect(new Set(c[key]).size, key).toBe(c[key].length);
    }
  });

  it('khoá defaults theo shape 05 §3: schemaVersion + colors/typography/shape/spacing, mọi giá trị nằm trong whitelist (G2)', () => {
    const c = cfg();
    const d = c.defaults;
    const re = new RegExp(c.colorPattern);
    expect(d.schemaVersion).toBe(1);
    expect(Object.keys(d.colors).sort()).toEqual([...c.colorKeys].sort());
    for (const [key, value] of Object.entries(d.colors)) expect(re.test(value), `colors.${key}`).toBe(true);
    expect(c.fonts).toContain(d.typography.headingFont);
    expect(c.fonts).toContain(d.typography.bodyFont);
    expect(c.scale).toContain(d.typography.scale);
    expect(c.radius).toContain(d.shape.radius);
    expect(c.shadow).toContain(d.shape.shadow);
    expect(c.sectionGap).toContain(d.spacing.sectionGap);
  });
});

describe('config/system-pages.json (G3)', () => {
  const cfg = () => readJson('config/system-pages.json') as SystemPagesConfig;

  it('khai đủ MỌI SystemType của 05 §4 (kể cả loại chưa seed)', () => {
    expect(Object.keys(cfg()).sort()).toEqual([...ALL_SYSTEM_TYPES].sort());
  });

  it('mỗi mục có routes[] (slug hợp lệ), phase là số nguyên dương, seedOnCreate là boolean', () => {
    for (const [type, entry] of Object.entries(cfg())) {
      expect(Array.isArray(entry.routes), `${type}.routes`).toBe(true);
      for (const route of entry.routes) expect(route, `${type}.routes`).toMatch(/^[a-z0-9]+(-[a-z0-9]+)*$/);
      expect(Number.isInteger(entry.phase) && entry.phase > 0, `${type}.phase`).toBe(true);
      expect(typeof entry.seedOnCreate, `${type}.seedOnCreate`).toBe('boolean');
    }
  });

  it('seed khi tạo website: đúng Contact (lien-he) + NotFound (không route); các loại khác chưa seed', () => {
    const c = cfg();
    expect(
      Object.entries(c)
        .filter(([, e]) => e.seedOnCreate)
        .map(([t]) => t)
        .sort(),
    ).toEqual(['Contact', 'NotFound']);
    expect(c.Contact?.routes).toEqual(['lien-he']);
    expect(c.NotFound?.routes).toEqual([]);
  });

  it('route chính (routes[0]) của các loại có route khớp 05 §4', () => {
    const c = cfg();
    expect(c.ProductListing?.routes).toEqual(['san-pham', 'danh-muc']);
    expect(c.ProductDetail?.routes[0]).toBe('san-pham');
    expect(c.ProductSearch?.routes[0]).toBe('tim-kiem');
    expect(c.Booking?.routes[0]).toBe('dat-lich');
  });

  it('mã locale không trùng route System page', () => {
    const locales = (readJson('config/locales.json') as string[]).map((l) => l.toLowerCase());
    const routes = Object.values(cfg()).flatMap((e) => e.routes);
    for (const code of locales) expect(routes, `locale '${code}' trùng route System`).not.toContain(code);
  });

  // plan §4 / S0-T1: key `reservedShopSitePaths` (người thêm vào config/reserved-routes.json — hook chặn agent ghi).
  it('mã locale và route System page không trùng reservedShopSitePaths; có media và api', () => {
    const reserved = (readJson('config/reserved-routes.json') as { reservedShopSitePaths?: string[] })
      .reservedShopSitePaths;
    expect(reserved, 'thiếu key reservedShopSitePaths').toBeDefined();
    expect(reserved).toEqual(expect.arrayContaining(['media', 'api']));
    const locales = (readJson('config/locales.json') as string[]).map((l) => l.toLowerCase());
    const routes = Object.values(cfg()).flatMap((e) => e.routes);
    for (const p of reserved ?? []) {
      expect(locales, `locale trùng reserved '${p}'`).not.toContain(p.toLowerCase());
      expect(routes, `route System trùng reserved '${p}'`).not.toContain(p);
    }
  });
});

describe('config/sanitize-profiles.json — khoá dangerous (R4, plan §3 Flow 2g, §10 T4)', () => {
  type Dangerous = { tags: string[]; attrPrefixes: string[]; schemes: string[] };
  const dangerous = () => (readJson('config/sanitize-profiles.json') as { dangerous: Dangerous }).dangerous;

  it('liệt kê thẻ nguy hiểm script/iframe/object/embed/style', () => {
    expect([...dangerous().tags].sort()).toEqual(['embed', 'iframe', 'object', 'script', 'style']);
  });

  it('tiền tố thuộc tính nguy hiểm là "on"', () => {
    expect(dangerous().attrPrefixes).toEqual(['on']);
  });

  it('scheme nguy hiểm: javascript/data/vbscript (chữ thường, không có dấu hai chấm)', () => {
    expect([...dangerous().schemes].sort()).toEqual(['data', 'javascript', 'vbscript']);
  });

  it('giữ nguyên profile inline/basic hiện có', () => {
    const profiles = readJson('config/sanitize-profiles.json') as Record<string, { tags: string[] }>;
    expect(profiles.inline?.tags).toEqual(['strong', 'em', 'u', 's', 'br', 'a']);
    expect(profiles.basic?.tags).toEqual(['p', 'strong', 'em', 'u', 's', 'br', 'a', 'ul', 'ol', 'li', 'h2', 'h3', 'h4', 'blockquote']);
  });
});
