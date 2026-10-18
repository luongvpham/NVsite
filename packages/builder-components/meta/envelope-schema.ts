import { z } from 'zod';
import localesJson from '../../../config/locales.json';
import themeTokensJson from '../../../config/theme-tokens.json';

/**
 * Shape VỎ của hai document JSONB (WEBSITE-001 S0-T2, #94, #97):
 *   - Tree:   `{ schemaVersion, id:"root", type:"Page", children[] }`, node `{ id, type, variant?, props?, children?, i18n? }`
 *   - Tokens: `{ schemaVersion, colors, typography, shape, spacing }` (05 §3)
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
// Instance dựng từ config thật của repo
// ---------------------------------------------------------------------------

export const treeEnvelopeSchema = buildTreeEnvelopeZod(localesJson);
export const themeTokensSchema = buildThemeTokensZod(themeTokensJson);
