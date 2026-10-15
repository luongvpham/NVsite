import Ajv2020 from 'ajv/dist/2020';
import fg from 'fast-glob';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';
import themeTokensConfig from '../../../config/theme-tokens.json';
import treeEnvelopeJsonSchema from '../generated/tree-envelope.schema.json';
import themeTokensJsonSchema from '../generated/theme-tokens.schema.json';
import { buildThemeTokensZod, buildTreeEnvelopeZod, NODE_ID_PATTERN, themeTokensSchema, treeEnvelopeSchema } from '../meta/envelope-schema';

/**
 * WEBSITE-001 S0-T2 (#97, #60) — Zod (FE) và JSON Schema (BE) của vỏ Tree/Tokens phải cho CÙNG kết quả.
 * Khuôn schema-equivalence.test.ts: nếu hai runtime lệch, BE và FE chấp nhận hai tập dữ liệu khác nhau.
 */
const ajv = new Ajv2020({ strict: false });
const validateTree = ajv.compile(treeEnvelopeJsonSchema);
const validateTokens = ajv.compile(themeTokensJsonSchema);

const FIXTURE_DIR = path.resolve(import.meta.dirname, 'fixtures', 'tree');

function agree(zod: { safeParse: (v: unknown) => { success: boolean } }, validate: (v: unknown) => boolean, name: string, value: unknown): boolean {
  const zodOk = zod.safeParse(value).success;
  const jsonOk = validate(value);
  expect(jsonOk, `case '${name}': JSON Schema (${jsonOk}) phải khớp Zod (${zodOk})`).toBe(zodOk);
  return zodOk;
}

const node = (extra: Record<string, unknown> = {}) => ({ id: 'c_abc12', type: 'Hero', ...extra });
const root = (extra: Record<string, unknown> = {}) => ({ schemaVersion: 1, id: 'root', type: 'Page', children: [], ...extra });

describe('tree envelope — Zod ↔ JSON Schema equivalence (#97)', () => {
  const cases: Array<{ name: string; value: unknown; valid: boolean }> = [
    { name: 'gốc rỗng', value: root(), valid: true },
    { name: 'một node đủ trường', value: root({ children: [node({ variant: 'Hero01', props: { title: 'x' }, children: [], i18n: { en: { title: 'y' } } })] }), valid: true },
    { name: 'lồng nhiều cấp', value: root({ children: [node({ type: 'Section', children: [node({ id: 'c_def34', type: 'Section', children: [node({ id: 'c_ghi56' })] })] })] }), valid: true },
    { name: 'thiếu schemaVersion', value: { id: 'root', type: 'Page', children: [] }, valid: false },
    { name: 'schemaVersion là chuỗi "1" (R12)', value: root({ schemaVersion: '1' }), valid: false },
    { name: 'schemaVersion 0', value: root({ schemaVersion: 0 }), valid: false },
    { name: 'schemaVersion không nguyên', value: root({ schemaVersion: 1.5 }), valid: false },
    { name: 'schemaVersion 999 (vỏ không giới hạn trên — BE trả SCHEMA_VERSION_UNSUPPORTED)', value: root({ schemaVersion: 999 }), valid: true },
    { name: 'gốc id khác "root"', value: root({ id: 'c_abc12' }), valid: false },
    { name: 'gốc type khác "Page"', value: root({ type: 'Hero' }), valid: false },
    { name: 'gốc thiếu children', value: { schemaVersion: 1, id: 'root', type: 'Page' }, valid: false },
    { name: 'gốc có trường lạ', value: root({ props: {} }), valid: false },
    { name: 'gốc là mảng', value: [], valid: false },
    { name: 'gốc là null', value: null, valid: false },
    { name: 'node thiếu id', value: root({ children: [{ type: 'Hero' }] }), valid: false },
    { name: 'node id sai khuôn c_', value: root({ children: [node({ id: 'abc12' })] }), valid: false },
    { name: 'node id quá ngắn', value: root({ children: [node({ id: 'c_ab' })] }), valid: false },
    { name: 'node thiếu type', value: root({ children: [{ id: 'c_abc12' }] }), valid: false },
    { name: 'node type rỗng', value: root({ children: [node({ type: '' })] }), valid: false },
    { name: 'node có trường lạ (additionalProperties:false)', value: root({ children: [node({ foo: 1 })] }), valid: false },
    { name: 'node lồng sâu có trường lạ', value: root({ children: [node({ type: 'Section', children: [node({ id: 'c_def34', bar: 1 })] })] }), valid: false },
    { name: 'node props là mảng', value: root({ children: [node({ props: [] })] }), valid: false },
    { name: 'node props là null', value: root({ children: [node({ props: null })] }), valid: false },
    { name: 'node children không phải mảng', value: root({ children: [node({ children: {} })] }), valid: false },
    { name: 'node variant rỗng', value: root({ children: [node({ variant: '' })] }), valid: false },
    { name: 'i18n khoá locale lạ (fr)', value: root({ children: [node({ i18n: { fr: {} } })] }), valid: false },
    { name: 'i18n giá trị không phải object', value: root({ children: [node({ i18n: { en: 'x' } })] }), valid: false },
  ];

  it.each(cases)('$name', ({ name, value, valid }) => {
    expect(agree(treeEnvelopeSchema, validateTree, name, value)).toBe(valid);
  });

  it('NODE_ID_PATTERN khớp id server sinh "c_" + 5 ký tự nanoid', () => {
    const re = new RegExp(NODE_ID_PATTERN);
    expect(re.test('c_a3f21')).toBe(true);
    expect(re.test('c_Ab-_9')).toBe(true);
    expect(re.test('root')).toBe(false);
  });

  it('dựng từ locales.json: thêm locale mới thì i18n nhận locale đó (một nguồn, không danh sách thứ hai)', () => {
    const extended = buildTreeEnvelopeZod(['vi', 'en', 'fr']);
    expect(extended.safeParse(root({ children: [node({ i18n: { fr: {} } })] })).success).toBe(true);
    expect(treeEnvelopeSchema.safeParse(root({ children: [node({ i18n: { fr: {} } })] })).success).toBe(false);
  });
});

describe('theme tokens — Zod ↔ JSON Schema equivalence (#97)', () => {
  const d = themeTokensConfig.defaults;
  interface Tokens {
    schemaVersion?: unknown;
    colors: Record<string, unknown>;
    typography: Record<string, unknown>;
    shape: Record<string, unknown>;
    spacing: Record<string, unknown>;
    extra?: unknown;
  }
  const clone = () => structuredClone(d) as Tokens;
  const mutate = (fn: (t: Tokens) => void) => {
    const t = clone();
    fn(t);
    return t;
  };

  const cases: Array<{ name: string; value: unknown; valid: boolean }> = [
    { name: 'defaults trong config là hợp lệ (G2)', value: d, valid: true },
    { name: 'màu chữ hoa', value: mutate((t) => (t.colors.primary = '#ABCDEF')), valid: true },
    { name: 'màu thiếu #', value: mutate((t) => (t.colors.primary = '7c3aed')), valid: false },
    { name: 'màu 3 ký tự', value: mutate((t) => (t.colors.primary = '#fff')), valid: false },
    { name: 'màu tên (red)', value: mutate((t) => (t.colors.text = 'red')), valid: false },
    { name: 'màu có url() — chống CSS injection', value: mutate((t) => (t.colors.text = 'url(javascript:alert(1))')), valid: false },
    { name: 'thiếu một key màu', value: mutate((t) => delete t.colors.border), valid: false },
    { name: 'key màu lạ', value: mutate((t) => (t.colors.accent = '#000000')), valid: false },
    { name: 'font ngoài whitelist', value: mutate((t) => (t.typography.headingFont = 'Comic Sans MS')), valid: false },
    { name: 'font body hợp lệ khác', value: mutate((t) => (t.typography.bodyFont = 'Lora')), valid: true },
    { name: 'scale ngoài enum', value: mutate((t) => (t.typography.scale = 'huge')), valid: false },
    { name: 'radius ngoài enum', value: mutate((t) => (t.shape.radius = 'xl')), valid: false },
    { name: 'radius full', value: mutate((t) => (t.shape.radius = 'full')), valid: true },
    { name: 'shadow ngoài enum', value: mutate((t) => (t.shape.shadow = 'huge')), valid: false },
    { name: 'sectionGap ngoài enum', value: mutate((t) => (t.spacing.sectionGap = 'xxl')), valid: false },
    { name: 'thiếu schemaVersion', value: mutate((t) => delete t.schemaVersion), valid: false },
    { name: 'schemaVersion là chuỗi', value: mutate((t) => (t.schemaVersion = '1')), valid: false },
    { name: 'thiếu nhóm typography', value: mutate((t) => delete (t as Partial<Tokens>).typography), valid: false },
    { name: 'trường lạ ở gốc', value: mutate((t) => (t.extra = 1)), valid: false },
    { name: 'trường lạ trong shape', value: mutate((t) => (t.shape.extra = 1)), valid: false },
    { name: 'không phải object', value: 'tokens', valid: false },
  ];

  it.each(cases)('$name', ({ name, value, valid }) => {
    expect(agree(themeTokensSchema, validateTokens, name, value)).toBe(valid);
  });

  it('dựng từ config: đổi whitelist font thì schema đổi theo', () => {
    const narrowed = buildThemeTokensZod({ ...themeTokensConfig, fonts: ['Inter'] });
    expect(narrowed.safeParse(d).success).toBe(false); // headingFont mặc định "Be Vietnam Pro" không còn trong whitelist
  });
});

describe('fixture Tree dùng chung (S0-T4) — Zod và JSON Schema đồng ý trên từng fixture', () => {
  const files = fg.sync('*.json', { cwd: FIXTURE_DIR, absolute: true }).sort();

  it('mọi fixture (nếu có) được hai runtime đánh giá giống nhau', () => {
    for (const file of files) {
      const name = path.basename(file);
      const fixture = JSON.parse(readFileSync(file, 'utf8')) as Record<string, unknown>;
      // xss-richtext.json không phải cây — danh sách vector HTML, test riêng ở sanitize-html.test.ts.
      for (const tree of [fixture.tree, fixture.stored, fixture.incoming]) {
        if (tree !== undefined) agree(treeEnvelopeSchema, validateTree, name, tree);
      }
    }
  });
});
