import { z } from 'zod';
import localesJson from '../../../config/locales.json';
import themeTokensJson from '../../../config/theme-tokens.json';

/**
 * Shape VỎ của ba document JSONB (WEBSITE-001 S0-T2, WEBSITE-004 S0-T2, #94, #97):
 *   - Tree:     `{ schemaVersion, id:"root", type:"Page", children[] }`, node `{ id, type, variant?, props?, children?, i18n? }`
 *   - Tokens:   `{ schemaVersion, colors, typography, shape, spacing }` (05 §3)
 *   - Snapshot: document bất biến publish sinh ra (plan §4 WEBSITE-004) — tái dùng vỏ Tree + Tokens,
 *               thêm `version`, `locales`, `navigation`, `seo`, `tracking`, `media`, `pages[]`.
 *
 * Vỏ chỉ kiểm CẤU TRÚC. `type`/`variant`/`props` của node KHÔNG kiểm ở đây (đó là việc của
 * `generated/tree-rules.json` + `props-schemas.json`, #60); `schemaVersion` chỉ kiểm là số nguyên >= 1
 * (so với phiên bản hiện hành là việc của upcaster BE, #94).
 *
 * Tập giá trị cho phép (mã locale, key màu, font, thang scale/radius/shadow/sectionGap) lấy từ `config/` —
 * một nguồn, không danh sách thứ hai (#17, #97).
 *
 * Zod (FE) và JSON Schema (BE) dựng ĐỘC LẬP từ cùng config (#60 — không convert từ cái này sang cái kia);
 * `tests/envelope-schema.test.ts` khẳng định hai bên đồng ý.
 */

export interface ThemeTokensConfig {
  colorKeys: readonly string[];
  colorPattern: string;
  fonts: readonly string[];
  scale: readonly string[];
  radius: readonly string[];
  shadow: readonly string[];
  sectionGap: readonly string[];
}

/** Id node do server sinh: `c_` + nanoid 5 ký tự (05 §6 quy tắc 1). Id gốc là chuỗi cố định "root". */
export const NODE_ID_PATTERN = '^c_[A-Za-z0-9_-]{5,}$';

type JsonSchemaObject = Record<string, unknown>;

const SCHEMA_HEADER_KEYS: ReadonlySet<string> = new Set(['$schema', '$id', 'title']);

/** Bỏ `$schema`/`$id`/`title` khi nhúng một schema độc lập vào `$defs` của schema gốc. */
function withoutSchemaHeader(schema: JsonSchemaObject): JsonSchemaObject {
  return Object.fromEntries(Object.entries(schema).filter(([key]) => !SCHEMA_HEADER_KEYS.has(key)));
}

function enumOf(values: readonly string[], what: string): [string, ...string[]] {
  const [first, ...rest] = values;
  if (first === undefined) throw new Error(`envelope-schema: tập giá trị '${what}' rỗng`);
  return [first, ...rest];
}

// ---------------------------------------------------------------------------
// Tree envelope
// ---------------------------------------------------------------------------

export function buildTreeEnvelopeZod(locales: readonly string[]) {
  const propsRecord = z.record(z.string(), z.unknown());
  const i18nShape = Object.fromEntries(locales.map((l) => [l, propsRecord.optional()]));

  interface TreeNode {
    id: string;
    type: string;
    variant?: string;
    props?: Record<string, unknown>;
    children?: TreeNode[];
    i18n?: Record<string, Record<string, unknown> | undefined>;
  }

  const nodeSchema: z.ZodType<TreeNode> = z.lazy(() =>
    z
      .object({
        id: z.string().regex(new RegExp(NODE_ID_PATTERN)),
        type: z.string().min(1),
        variant: z.string().min(1).optional(),
        props: propsRecord.optional(),
        children: z.array(nodeSchema).optional(),
        i18n: z.object(i18nShape).strict().optional(),
      })
      .strict(),
  );

  return z
    .object({
      schemaVersion: z.number().int().min(1),
      id: z.literal('root'),
      type: z.literal('Page'),
      children: z.array(nodeSchema),
    })
    .strict();
}

export function buildTreeEnvelopeJsonSchema(locales: readonly string[]): JsonSchemaObject {
  const i18nProperties = Object.fromEntries(locales.map((l) => [l, { type: 'object' }]));
  return {
    $schema: 'https://json-schema.org/draft/2020-12/schema',
    $id: 'https://vsite.vn/schemas/tree-envelope.schema.json',
    title: 'Component Tree envelope (#94, #97)',
    type: 'object',
    properties: {
      schemaVersion: { type: 'integer', minimum: 1 },
      id: { const: 'root' },
      type: { const: 'Page' },
      children: { type: 'array', items: { $ref: '#/$defs/node' } },
    },
    required: ['schemaVersion', 'id', 'type', 'children'],
    additionalProperties: false,
    $defs: {
      node: {
        type: 'object',
        properties: {
          id: { type: 'string', pattern: NODE_ID_PATTERN },
          type: { type: 'string', minLength: 1 },
          variant: { type: 'string', minLength: 1 },
          props: { type: 'object' },
          children: { type: 'array', items: { $ref: '#/$defs/node' } },
          i18n: { type: 'object', properties: i18nProperties, additionalProperties: false },
        },
        required: ['id', 'type'],
        additionalProperties: false,
      },
    },
  };
}

// ---------------------------------------------------------------------------
// Theme tokens
// ---------------------------------------------------------------------------

export function buildThemeTokensZod(config: ThemeTokensConfig) {
  const color = z.string().regex(new RegExp(config.colorPattern));
  const colors = z
    .object(Object.fromEntries(config.colorKeys.map((k) => [k, color])))
    .strict();
  const font = z.enum(enumOf(config.fonts, 'fonts'));

  return z
    .object({
      schemaVersion: z.number().int().min(1),
      colors,
      typography: z
        .object({ headingFont: font, bodyFont: font, scale: z.enum(enumOf(config.scale, 'scale')) })
        .strict(),
      shape: z
        .object({ radius: z.enum(enumOf(config.radius, 'radius')), shadow: z.enum(enumOf(config.shadow, 'shadow')) })
        .strict(),
      spacing: z.object({ sectionGap: z.enum(enumOf(config.sectionGap, 'sectionGap')) }).strict(),
    })
    .strict();
}

export function buildThemeTokensJsonSchema(config: ThemeTokensConfig): JsonSchemaObject {
  const color = { type: 'string', pattern: config.colorPattern };
  const font = { type: 'string', enum: [...config.fonts] };
  const strictObject = (properties: Record<string, unknown>): JsonSchemaObject => ({
    type: 'object',
    properties,
    required: Object.keys(properties),
    additionalProperties: false,
  });

  return {
    $schema: 'https://json-schema.org/draft/2020-12/schema',
    $id: 'https://vsite.vn/schemas/theme-tokens.schema.json',
    title: 'Theme.Tokens envelope (#94, #97)',
    ...strictObject({
      schemaVersion: { type: 'integer', minimum: 1 },
      colors: strictObject(Object.fromEntries(config.colorKeys.map((k) => [k, color]))),
      typography: strictObject({ headingFont: font, bodyFont: font, scale: { type: 'string', enum: [...config.scale] } }),
      shape: strictObject({ radius: { type: 'string', enum: [...config.radius] }, shadow: { type: 'string', enum: [...config.shadow] } }),
      spacing: strictObject({ sectionGap: { type: 'string', enum: [...config.sectionGap] } }),
    }),
  };
}

// ---------------------------------------------------------------------------
// Site Snapshot v1 (WEBSITE-004 S0-T2, #94, #97)
// ---------------------------------------------------------------------------
//
// Vỏ của document JSONB bất biến mà publish sinh ra (plan §4). Chỉ kiểm CẤU TRÚC:
//   - `tree` của từng trang tham chiếu vỏ Tree sẵn có; `theme.tokens` tham chiếu vỏ Tokens sẵn có.
//   - `media` là map `imageId → { storageKey, preset }`; `preset = null` cho bản Library gốc.
//   - `navigation.items` để rỗng ở 8b (NavigationConfig thật là Bước 7) nên items chỉ kiểm là mảng.
//   - `tracking` là map mở (như `props` của node) — không ép khoá.
// `additionalProperties: false` ở mọi cấp vỏ; `schemaVersion` chỉ kiểm là số nguyên >= 1 (so phiên bản
// hiện hành là việc của upcaster BE, #94). Zod (FE) và JSON Schema (BE) dựng ĐỘC LẬP từ cùng config (#60).

export function buildSiteSnapshotZod(locales: readonly string[], themeConfig: ThemeTokensConfig) {
  const tree = buildTreeEnvelopeZod(locales);
  const tokens = buildThemeTokensZod(themeConfig);
  const localeEnum = z.enum(enumOf(locales, 'locales'));
  const nullableString = z.string().nullable();

  const mediaEntry = z.object({ storageKey: z.string(), preset: z.string().nullable() }).strict();
  const pageSeo = z
    .object({ title: nullableString, description: nullableString, ogImageId: nullableString, noIndex: z.boolean() })
    .strict();
  const page = z
    .object({
      id: z.string().min(1),
      slug: z.string().nullable(),
      kind: z.string().min(1),
      systemType: z.string().nullable(),
      title: z.string(),
      seo: pageSeo,
      tree,
    })
    .strict();

  return z
    .object({
      schemaVersion: z.number().int().min(1),
      version: z.number().int().min(1),
      defaultLocale: localeEnum,
      locales: z.array(localeEnum),
      theme: z.object({ presetKey: nullableString, tokens }).strict(),
      navigation: z.object({ items: z.array(z.unknown()) }).strict(),
      seo: z
        .object({
          defaultTitle: nullableString,
          defaultDescription: nullableString,
          ogImageId: nullableString,
          faviconAssetId: nullableString,
        })
        .strict(),
      tracking: z.record(z.string(), z.unknown()),
      media: z.record(z.string(), mediaEntry),
      pages: z.array(page),
    })
    .strict();
}

export function buildSiteSnapshotJsonSchema(
  locales: readonly string[],
  themeConfig: ThemeTokensConfig,
): JsonSchemaObject {
  const treeSchema = buildTreeEnvelopeJsonSchema(locales);
  const tokensSchema = buildThemeTokensJsonSchema(themeConfig);
  // Nâng `node` của vỏ Tree lên `$defs` gốc; `treeEnvelope` giữ nguyên thân (children vẫn `$ref #/$defs/node`).
  const { $defs: treeDefs = {}, ...treeEnvelopeBody } = withoutSchemaHeader(treeSchema);
  const themeTokensBody = withoutSchemaHeader(tokensSchema);
  const node = (treeDefs as JsonSchemaObject).node;

  const nullableString = { type: ['string', 'null'] };
  const strictObject = (properties: Record<string, unknown>): JsonSchemaObject => ({
    type: 'object',
    properties,
    required: Object.keys(properties),
    additionalProperties: false,
  });

  const localeEnum = { type: 'string', enum: [...locales] };

  return {
    $schema: 'https://json-schema.org/draft/2020-12/schema',
    $id: 'https://vsite.vn/schemas/site-snapshot.schema.json',
    title: 'Site Snapshot v1 envelope (#94, #97)',
    ...strictObject({
      schemaVersion: { type: 'integer', minimum: 1 },
      version: { type: 'integer', minimum: 1 },
      defaultLocale: localeEnum,
      locales: { type: 'array', items: localeEnum },
      theme: strictObject({ presetKey: nullableString, tokens: { $ref: '#/$defs/themeTokens' } }),
      navigation: strictObject({ items: { type: 'array' } }),
      seo: strictObject({
        defaultTitle: nullableString,
        defaultDescription: nullableString,
        ogImageId: nullableString,
        faviconAssetId: nullableString,
      }),
      tracking: { type: 'object' },
      media: {
        type: 'object',
        additionalProperties: strictObject({ storageKey: { type: 'string' }, preset: nullableString }),
      },
      pages: { type: 'array', items: { $ref: '#/$defs/page' } },
    }),
    $defs: {
      node,
      treeEnvelope: treeEnvelopeBody,
      themeTokens: themeTokensBody,
      page: strictObject({
        id: { type: 'string', minLength: 1 },
        slug: nullableString,
        kind: { type: 'string', minLength: 1 },
        systemType: nullableString,
        title: { type: 'string' },
        seo: strictObject({
          title: nullableString,
          description: nullableString,
          ogImageId: nullableString,
          noIndex: { type: 'boolean' },
        }),
        tree: { $ref: '#/$defs/treeEnvelope' },
      }),
    },
  };
}

// ---------------------------------------------------------------------------
// Instance dựng từ config thật của repo
// ---------------------------------------------------------------------------

export const treeEnvelopeSchema = buildTreeEnvelopeZod(localesJson);
export const themeTokensSchema = buildThemeTokensZod(themeTokensJson);
export const siteSnapshotSchema = buildSiteSnapshotZod(localesJson, themeTokensJson);

/** Shape của document Snapshot bất biến (plan §4, WEBSITE-004). FE dùng để parse response `GET /api/site`. */
export type SiteSnapshot = z.infer<typeof siteSnapshotSchema>;
