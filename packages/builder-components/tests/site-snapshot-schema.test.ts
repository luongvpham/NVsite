import Ajv2020 from 'ajv/dist/2020';
import { describe, expect, it } from 'vitest';
import themeTokensConfig from '../../../config/theme-tokens.json';
import siteSnapshotJsonSchema from '../generated/site-snapshot.schema.json';
import { buildSiteSnapshotZod, siteSnapshotSchema } from '../meta/envelope-schema';

/**
 * WEBSITE-004 S0-T2 (#94, #97, #60) — vỏ SiteSnapshot v1.
 * Zod (FE) và JSON Schema (BE) dựng ĐỘC LẬP từ cùng config nên phải cho CÙNG kết quả trên mọi ca —
 * nếu lệch, publish (BE) và SSR (FE) chấp nhận hai tập snapshot khác nhau. Khuôn envelope-schema.test.ts.
 */
const ajv = new Ajv2020({ strict: false });
const validateSnapshot = ajv.compile(siteSnapshotJsonSchema);

const tokens = themeTokensConfig.defaults;
const emptyTree = { schemaVersion: 1, id: 'root', type: 'Page', children: [] };

const page = (extra: Record<string, unknown> = {}) => ({
  id: 'p_0001',
  slug: '',
  kind: 'Composable',
  systemType: null,
  title: 'Trang chủ',
  seo: { title: null, description: null, ogImageId: null, noIndex: false },
  tree: emptyTree,
  ...extra,
});

/** Snapshot đầy đủ đúng shape plan §4. */
const snapshot = (extra: Record<string, unknown> = {}) => ({
  schemaVersion: 1,
  version: 7,
  defaultLocale: 'vi',
  locales: ['vi'],
  theme: { presetKey: null, tokens },
  navigation: { items: [] },
  seo: { defaultTitle: null, defaultDescription: null, ogImageId: null, faviconAssetId: null },
  tracking: {},
  media: { '5b0c1111-2222-3333-4444-555566667777': { storageKey: 'shops/abc/x.webp', preset: '1600x900,cover' } },
  pages: [page()],
  ...extra,
});

function agree(name: string, value: unknown): boolean {
  const zodOk = siteSnapshotSchema.safeParse(value).success;
  const jsonOk = validateSnapshot(value);
  expect(jsonOk, `case '${name}': JSON Schema (${jsonOk}) phải khớp Zod (${zodOk})`).toBe(zodOk);
  return zodOk;
}

describe('site snapshot — Zod ↔ JSON Schema equivalence (#97, WEBSITE-004)', () => {
  const cases: Array<{ name: string; value: unknown; valid: boolean }> = [
    { name: 'ví dụ §4 đầy đủ', value: snapshot(), valid: true },
    { name: 'navigation.items rỗng hợp lệ', value: snapshot({ navigation: { items: [] } }), valid: true },
    { name: 'media rỗng hợp lệ', value: snapshot({ media: {} }), valid: true },
    { name: 'tracking có khoá tuỳ ý', value: snapshot({ tracking: { gtag: 'G-XYZ' } }), valid: true },
    { name: 'preset = null (bản Library gốc)', value: snapshot({ media: { a: { storageKey: 'k', preset: null } } }), valid: true },
    { name: 'slug: null (chỉ trang NotFound)', value: snapshot({ pages: [page({ slug: null })] }), valid: true },
    { name: 'systemType có giá trị', value: snapshot({ pages: [page({ systemType: 'NotFound' })] }), valid: true },
    { name: 'nhiều trang', value: snapshot({ pages: [page(), page({ id: 'p_0002', slug: 'lien-he', title: 'Liên hệ' })] }), valid: true },
    { name: 'không trang nào', value: snapshot({ pages: [] }), valid: true },

    { name: 'thiếu pages[].id', value: snapshot({ pages: [{ slug: '', kind: 'Composable', systemType: null, title: 'x', seo: { title: null, description: null, ogImageId: null, noIndex: false }, tree: emptyTree }] }), valid: false },
    { name: 'pages[].id rỗng', value: snapshot({ pages: [page({ id: '' })] }), valid: false },
    { name: 'khoá lạ ở gốc', value: snapshot({ extra: 1 }), valid: false },
    { name: 'khoá lạ trong page', value: snapshot({ pages: [page({ foo: 1 })] }), valid: false },
    { name: 'khoá lạ trong theme', value: snapshot({ theme: { presetKey: null, tokens, bar: 1 } }), valid: false },
    { name: 'khoá lạ trong seo', value: snapshot({ seo: { defaultTitle: null, defaultDescription: null, ogImageId: null, faviconAssetId: null, baz: 1 } }), valid: false },
    { name: 'thiếu schemaVersion', value: snapshot({ schemaVersion: undefined }), valid: false },
    { name: 'schemaVersion là chuỗi', value: snapshot({ schemaVersion: '1' }), valid: false },
    { name: 'version 0', value: snapshot({ version: 0 }), valid: false },
    { name: 'version không nguyên', value: snapshot({ version: 1.5 }), valid: false },
    { name: 'defaultLocale ngoài config (fr)', value: snapshot({ defaultLocale: 'fr' }), valid: false },
    { name: 'locales chứa mã lạ', value: snapshot({ locales: ['vi', 'fr'] }), valid: false },
    { name: 'thiếu theme', value: snapshot({ theme: undefined }), valid: false },
    { name: 'theme.tokens sai (màu không #)', value: snapshot({ theme: { presetKey: null, tokens: { ...tokens, colors: { ...tokens.colors, primary: 'red' } } } }), valid: false },
    { name: 'navigation.items không phải mảng', value: snapshot({ navigation: { items: {} } }), valid: false },
    { name: 'media entry thiếu storageKey', value: snapshot({ media: { a: { preset: null } } }), valid: false },
    { name: 'media entry khoá lạ', value: snapshot({ media: { a: { storageKey: 'k', preset: null, foo: 1 } } }), valid: false },
    { name: 'page.tree sai vỏ (gốc id khác root)', value: snapshot({ pages: [page({ tree: { schemaVersion: 1, id: 'c_abc12', type: 'Page', children: [] } })] }), valid: false },
    { name: 'page.seo.noIndex không phải boolean', value: snapshot({ pages: [page({ seo: { title: null, description: null, ogImageId: null, noIndex: 'no' } })] }), valid: false },
    { name: 'gốc là mảng', value: [], valid: false },
    { name: 'gốc là null', value: null, valid: false },
  ];

  it.each(cases)('$name', ({ name, value, valid }) => {
    expect(agree(name, value)).toBe(valid);
  });

  it('dựng từ locales.json: thêm locale mới thì defaultLocale/locales nhận mã đó (một nguồn, #17)', () => {
    const extended = buildSiteSnapshotZod(['vi', 'en', 'fr'], themeTokensConfig);
    expect(extended.safeParse(snapshot({ defaultLocale: 'fr', locales: ['fr'] })).success).toBe(true);
    expect(siteSnapshotSchema.safeParse(snapshot({ defaultLocale: 'fr' })).success).toBe(false);
  });
});
