import Ajv2020 from 'ajv/dist/2020';
import { describe, expect, expectTypeOf, it } from 'vitest';
import themeTokensConfig from '../../../config/theme-tokens.json';
import siteSnapshotJsonSchema from '../generated/site-snapshot.schema.json';
import { buildSiteSnapshotZod, siteSnapshotSchema } from '../meta/envelope-schema';
import type { SiteNavigationItem, SiteSnapshot } from '../src/index';

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

const P1 = 'a1111111-2222-4333-8444-5555555beef5';
const P2 = '21111111-2222-4333-8444-555555555555';
const P3 = '31111111-2222-4333-8444-555555555555';
const P4 = '41111111-2222-4333-8444-555555555555';
const navPage = (id: string, label: string | null = null) => ({ key: `page:${id}`, label });
const navCustom = (label: string, url: string) => ({ key: 'custom', label, url });

/** Ví dụ 3 cấp đúng `data-needs` (cấp 3 không có `children`). */
const threeLevels = () => ({
  ...navPage(P1),
  children: [
    { ...navPage(P2, 'Dịch vụ'), children: [navPage(P3), navCustom('Bảng giá', 'https://example.vn/gia')] },
    navCustom('Zalo', 'https://zalo.me/0901'),
  ],
});

const fourLevels = () => ({
  ...navPage(P1),
  children: [{ ...navPage(P2), children: [{ ...navPage(P3), children: [navPage(P4)] }] }],
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
    // ---- navigation.items (WEBSITE-007, #113): tối đa 3 cấp, strict mọi cấp, không có `hidden` ----
    { name: 'nav: mục cấp 1 trang + custom', value: snapshot({ navigation: { items: [navPage(P1), navCustom('Gọi', 'tel:0901')] } }), valid: true },
    { name: 'nav: nhãn riêng cho mục trang', value: snapshot({ navigation: { items: [navPage(P1, 'Trang chủ')] } }), valid: true },
    { name: 'nav: 3 cấp hợp lệ', value: snapshot({ navigation: { items: [threeLevels()] } }), valid: true },
    { name: 'nav: children rỗng hợp lệ', value: snapshot({ navigation: { items: [{ ...navPage(P1), children: [] }] } }), valid: true },
    { name: 'nav: cấp 4 bị từ chối', value: snapshot({ navigation: { items: [fourLevels()] } }), valid: false },
    { name: 'nav: cấp 3 có children rỗng vẫn bị từ chối', value: snapshot({ navigation: { items: [{ ...navPage(P1), children: [{ ...navPage(P2), children: [{ ...navPage(P3), children: [] }] }] }] } }), valid: false },
    { name: 'nav: có hidden bị từ chối', value: snapshot({ navigation: { items: [{ ...navPage(P1), hidden: false }] } }), valid: false },
    { name: 'nav: khoá lạ ở cấp 2', value: snapshot({ navigation: { items: [{ ...navPage(P1), children: [{ ...navPage(P2), foo: 1 }] }] } }), valid: false },
    { name: 'nav: khoá lạ ở cấp 3', value: snapshot({ navigation: { items: [{ ...navPage(P1), children: [{ ...navPage(P2), children: [{ ...navPage(P3), foo: 1 }] }] }] } }), valid: false },
    { name: 'nav: thiếu label', value: snapshot({ navigation: { items: [{ key: `page:${P1}` }] } }), valid: false },
    { name: 'nav: thiếu key', value: snapshot({ navigation: { items: [{ label: 'x' }] } }), valid: false },
    { name: 'nav: label là số', value: snapshot({ navigation: { items: [{ key: 'custom', label: 1, url: 'https://a.vn' }] } }), valid: false },
    { name: 'nav: url null', value: snapshot({ navigation: { items: [{ key: 'custom', label: 'x', url: null }] } }), valid: false },
    { name: 'nav: key lạ', value: snapshot({ navigation: { items: [{ key: 'link', label: 'x' }] } }), valid: false },
    { name: 'nav: key page: không phải GUID', value: snapshot({ navigation: { items: [{ key: 'page:abc', label: null }] } }), valid: false },
    { name: 'nav: key page: GUID chữ hoa', value: snapshot({ navigation: { items: [{ key: `page:${P1.toUpperCase()}`, label: null }] } }), valid: false },
    { name: 'nav: key Custom sai hoa thường', value: snapshot({ navigation: { items: [{ key: 'Custom', label: 'x', url: 'https://a.vn' }] } }), valid: false },
    { name: 'nav: children không phải mảng', value: snapshot({ navigation: { items: [{ ...navPage(P1), children: {} }] } }), valid: false },
    { name: 'nav: mục là chuỗi', value: snapshot({ navigation: { items: ['page:x'] } }), valid: false },
    { name: 'nav: khoá lạ cạnh items', value: snapshot({ navigation: { items: [], foo: 1 } }), valid: false },

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

  it('kiểu SiteNavigationItem khớp snapshot.navigation.items; key là chuỗi template, không enum (#19)', () => {
    expectTypeOf<SiteSnapshot['navigation']['items'][number]>().toEqualTypeOf<SiteNavigationItem>();
    expectTypeOf<SiteNavigationItem['key']>().toEqualTypeOf<`page:${string}` | 'custom'>();
    const parsed = siteSnapshotSchema.parse(snapshot({ navigation: { items: [threeLevels()] } }));
    expect(parsed.navigation.items).toEqual([threeLevels()]);
  });
});
