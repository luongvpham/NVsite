#!/usr/bin/env -S tsx
/**
 * `pnpm gen:registry` — sinh artifact từ registry/*.manifest.ts (07-component-manifest-schema.md §6).
 *
 * Sinh 7 artifact: component-types.ts, props-schemas.ts (Zod), props-schemas.json (JSON Schema, #60),
 * property-panel.ts, op-rules.ts, ai-tool-schema.json, registry-map.ts.
 * check-additive vs registry.lock.json được wire vào ở 2.11 (chưa có lock file thì bỏ qua, tạo mới).
 */
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import type { ComponentManifest } from '../meta/manifest-schema';
import type { PropDef } from '../meta/prop-kinds';
import { fail, loadManifests } from './lib/load-manifests';
import { buildLockSnapshot } from './lib/lock-snapshot';
import { runCheckAdditive } from './check-additive';

const PKG_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const REPO_ROOT = path.resolve(PKG_ROOT, '..', '..');
const GENERATED_DIR = path.join(PKG_ROOT, 'generated');
const LOCK_FILE = path.join(PKG_ROOT, 'registry.lock.json');

const imagePresets = JSON.parse(readFileSync(path.join(REPO_ROOT, 'config', 'image-presets.json'), 'utf8')) as Record<string, unknown>;
const bindingSources = JSON.parse(readFileSync(path.join(REPO_ROOT, 'config', 'binding-sources.json'), 'utf8')) as string[];
const sanitizeProfiles = JSON.parse(readFileSync(path.join(REPO_ROOT, 'config', 'sanitize-profiles.json'), 'utf8')) as Record<string, unknown>;

function toKebabCase(pascalCase: string): string {
  return pascalCase.replace(/([a-z0-9])([A-Z])/g, '$1-$2').toLowerCase();
}

/** Invariant chéo giữa nhiều manifest (§6.1 — #1, #2, #7, #10, #12). Intra-manifest đã kiểm ở Zod. */
function checkCrossManifestInvariants(manifests: ComponentManifest[]): void {
  const seenTypes = new Set<string>();

  for (const manifest of manifests) {
    // #10 — type trùng giữa hai manifest
    if (seenTypes.has(manifest.type)) {
      fail(`type '${manifest.type}' khai báo trùng ở nhiều manifest`);
    }
    seenTypes.add(manifest.type);

    for (const [propName, propDef] of Object.entries(manifest.props)) {
      checkPropInvariants(manifest.type, propName, propDef);
    }

    // #7 — registry-map thiếu file component cho variant đã khai
    const folder = toKebabCase(manifest.type);
    for (const variant of manifest.variants) {
      const componentFile = path.join(PKG_ROOT, 'src', folder, `${variant.key}.tsx`);
      if (!existsSync(componentFile)) {
        fail(`variant '${variant.key}' của type '${manifest.type}' khai báo nhưng không có component tại src/${folder}/${variant.key}.tsx`);
      }
    }
  }
}

function checkPropInvariants(typeName: string, propName: string, prop: PropDef): void {
  const where = `${typeName}.${propName}`;

  if (prop.kind === 'binding') {
    // #1 — "Review" hard-fail, cảnh báo sẽ bị bỏ qua nên phải fail (#65)
    if (prop.sources.includes('Review')) {
      fail(`${where}: binding.sources chứa "Review" — ranh giới cứng 01 §7, không được phép`);
    }
    for (const source of prop.sources) {
      if (!bindingSources.includes(source)) {
        fail(`${where}: binding source '${source}' không có trong config/binding-sources.json`);
      }
    }
  }

  if (prop.kind === 'image' && !(prop.preset in imagePresets)) {
    fail(`${where}: image.preset '${prop.preset}' không có trong config/image-presets.json`);
  }

  if (prop.kind === 'richText' && !(prop.profile in sanitizeProfiles)) {
    fail(`${where}: richText.profile '${prop.profile}' không có trong config/sanitize-profiles.json`);
  }

  if (prop.kind === 'group') {
    for (const [childName, childProp] of Object.entries(prop.props)) {
      checkPropInvariants(typeName, `${propName}.${childName}`, childProp);
    }
  }

  if (prop.kind === 'list') {
    for (const [childName, childProp] of Object.entries(prop.itemProps)) {
      checkPropInvariants(typeName, `${propName}[].${childName}`, childProp);
    }
  }
}

// ---------------------------------------------------------------------------
// component-types.ts
// ---------------------------------------------------------------------------

function propToTsType(prop: PropDef): string {
  switch (prop.kind) {
    case 'text':
    case 'color':
    case 'icon':
      return 'string';
    case 'richText':
      return 'string';
    case 'number':
      return 'number';
    case 'boolean':
      return 'boolean';
    case 'select':
      return prop.options.map((o) => JSON.stringify(o.value)).join(' | ') || 'string';
    case 'image':
      return '{ imageId: string; alt?: string }';
    case 'link': {
      const kindBranches: Record<string, string> = {
        page: '{ kind: "page"; pageId: string }',
        systemPage: '{ kind: "systemPage"; systemType: string }',
        productCategory: '{ kind: "productCategory"; categoryId: number }',
        external: '{ kind: "external"; url: string }',
        anchor: '{ kind: "anchor"; nodeId: string }',
      };
      return prop.allowKinds.map((k) => kindBranches[k]).join(' | ');
    }
    case 'group':
      return propsToTsInterfaceBody(prop.props);
    case 'list':
      return `Array<${propsToTsInterfaceBody(prop.itemProps)}>`;
    case 'binding':
      return '{ filters?: Record<string, unknown> }';
  }
}

function propsToTsInterfaceBody(props: Record<string, PropDef>): string {
  const fields = Object.entries(props)
    .map(([name, prop]) => `  ${name}?: ${propToTsType(prop)};`)
    .join('\n');
  return `{\n${fields}\n}`;
}

function generateComponentTypes(manifests: ComponentManifest[]): string {
  const lines: string[] = ['// GENERATED — DO NOT EDIT (pnpm gen:registry)', ''];

  lines.push(`export type ComponentType = ${manifests.map((m) => JSON.stringify(m.type)).join(' | ') || 'never'};`, '');

  for (const manifest of manifests) {
    lines.push(`export interface ${manifest.type}Props ${propsToTsInterfaceBody(manifest.props)}`, '');
  }

  const nodeUnion = manifests
    .map((m) => `  | { type: ${JSON.stringify(m.type)}; variant: ${m.variants.map((v) => JSON.stringify(v.key)).join(' | ')}; id: string; props: ${m.type}Props; children?: ComponentNode[] }`)
    .join('\n');

  lines.push(`export type ComponentNode =\n${nodeUnion || '  never'};`, '');

  return lines.join('\n');
}

// ---------------------------------------------------------------------------
// props-schemas.ts (Zod) — validate SHAPE của từng prop. required-theo-variant
// (requiresProps) KHÔNG enforce ở đây — đó là input cho Operations Engine (Bước 5),
// vì cùng type Hero có thể là variant Hero01 (cần image) hay Hero02 (không cần).
// Mọi prop ở tầng type-schema vì vậy là optional.
// ---------------------------------------------------------------------------

function propToZodExpr(prop: PropDef): string {
  switch (prop.kind) {
    case 'text': {
      let expr = 'z.string()';
      if (prop.maxLength) expr += `.max(${prop.maxLength})`;
      return expr;
    }
    case 'richText': {
      let expr = 'z.string()';
      if (prop.maxLength) expr += `.max(${prop.maxLength})`;
      return expr;
    }
    case 'number': {
      let expr = 'z.number()';
      if (prop.min !== undefined) expr += `.min(${prop.min})`;
      if (prop.max !== undefined) expr += `.max(${prop.max})`;
      return expr;
    }
    case 'boolean':
      return 'z.boolean()';
    case 'select':
      return `z.enum([${prop.options.map((o) => JSON.stringify(o.value)).join(', ')}])`;
    case 'color':
      return 'z.string()';
    case 'icon':
      return 'z.string()';
    case 'image':
      return 'z.object({ imageId: z.string(), alt: z.string().optional() })';
    case 'link': {
      const branches: Record<string, string> = {
        page: 'z.object({ kind: z.literal("page"), pageId: z.string() })',
        systemPage: 'z.object({ kind: z.literal("systemPage"), systemType: z.string() })',
        productCategory: 'z.object({ kind: z.literal("productCategory"), categoryId: z.number() })',
        external: 'z.object({ kind: z.literal("external"), url: z.string() })',
        anchor: 'z.object({ kind: z.literal("anchor"), nodeId: z.string() })',
      };
      const options = prop.allowKinds.map((k) => branches[k]);
      // options.join('') với đúng 1 phần tử trả về chính phần tử đó — tránh index access (options[0]).
      return options.length === 1 ? options.join('') : `z.discriminatedUnion("kind", [${options.join(', ')}])`;
    }
    case 'group':
      return propsToZodObjectExpr(prop.props);
    case 'list': {
      let expr = `z.array(${propsToZodObjectExpr(prop.itemProps)})`;
      if (prop.minItems !== undefined) expr += `.min(${prop.minItems})`;
      if (prop.maxItems !== undefined) expr += `.max(${prop.maxItems})`;
      return expr;
    }
    case 'binding':
      return 'z.object({ filters: z.record(z.string(), z.unknown()).optional() })';
  }
}

function propsToZodObjectExpr(props: Record<string, PropDef>): string {
  const fields = Object.entries(props)
    .map(([name, prop]) => `  ${JSON.stringify(name)}: ${propToZodExpr(prop)}.optional(),`)
    .join('\n');
  return `z.object({\n${fields}\n})`;
}

function generatePropsSchemas(manifests: ComponentManifest[]): string {
  const lines: string[] = ['// GENERATED — DO NOT EDIT (pnpm gen:registry)', "import { z } from 'zod';", ''];

  for (const manifest of manifests) {
    const varName = `${manifest.type.charAt(0).toLowerCase()}${manifest.type.slice(1)}PropsSchema`;
    lines.push(`export const ${varName} = ${propsToZodObjectExpr(manifest.props)};`, '');
  }

  return lines.join('\n');
}

// ---------------------------------------------------------------------------
// props-schemas.json (JSON Schema draft 2020-12) — #60: KHÔNG convert từ Zod, sinh trực tiếp
// từ manifest (cùng nguồn, hai runtime — không có bản dịch nào tồn tại để lệch).
// ---------------------------------------------------------------------------

type JsonSchemaObject = Record<string, unknown>;

function propToJsonSchema(prop: PropDef): JsonSchemaObject {
  switch (prop.kind) {
    case 'text':
    case 'richText': {
      const schema: JsonSchemaObject = { type: 'string' };
      if (prop.maxLength !== undefined) schema.maxLength = prop.maxLength;
      return schema;
    }
    case 'number': {
      const schema: JsonSchemaObject = { type: 'number' };
      if (prop.min !== undefined) schema.minimum = prop.min;
      if (prop.max !== undefined) schema.maximum = prop.max;
      return schema;
    }
    case 'boolean':
      return { type: 'boolean' };
    case 'select':
      return { type: 'string', enum: prop.options.map((o) => o.value) };
    case 'color':
    case 'icon':
      return { type: 'string' };
    case 'image':
      return {
        type: 'object',
        properties: { imageId: { type: 'string' }, alt: { type: 'string' } },
        required: ['imageId'],
      };
    case 'link': {
      const branches: Record<string, JsonSchemaObject> = {
        page: { type: 'object', properties: { kind: { const: 'page' }, pageId: { type: 'string' } }, required: ['kind', 'pageId'] },
        systemPage: {
          type: 'object',
          properties: { kind: { const: 'systemPage' }, systemType: { type: 'string' } },
          required: ['kind', 'systemType'],
        },
        productCategory: {
          type: 'object',
          properties: { kind: { const: 'productCategory' }, categoryId: { type: 'number' } },
          required: ['kind', 'categoryId'],
        },
        external: { type: 'object', properties: { kind: { const: 'external' }, url: { type: 'string' } }, required: ['kind', 'url'] },
        anchor: { type: 'object', properties: { kind: { const: 'anchor' }, nodeId: { type: 'string' } }, required: ['kind', 'nodeId'] },
      };
      const options = prop.allowKinds.map((k) => branches[k]);
      const [first] = options;
      return options.length === 1 && first ? first : { oneOf: options };
    }
    case 'group':
      return propsToJsonSchemaObject(prop.props);
    case 'list': {
      const schema: JsonSchemaObject = { type: 'array', items: propsToJsonSchemaObject(prop.itemProps) };
      if (prop.minItems !== undefined) schema.minItems = prop.minItems;
      if (prop.maxItems !== undefined) schema.maxItems = prop.maxItems;
      return schema;
    }
    case 'binding':
      return { type: 'object', properties: { filters: { type: 'object' } } };
  }
}

function propsToJsonSchemaObject(props: Record<string, PropDef>): JsonSchemaObject {
  const properties: Record<string, JsonSchemaObject> = {};
  for (const [name, prop] of Object.entries(props)) {
    properties[name] = propToJsonSchema(prop);
  }
  return { type: 'object', properties };
}

function generatePropsSchemasJson(manifests: ComponentManifest[]): string {
  const schemas: Record<string, JsonSchemaObject> = {};
  for (const manifest of manifests) {
    schemas[manifest.type] = {
      $schema: 'https://json-schema.org/draft/2020-12/schema',
      ...propsToJsonSchemaObject(manifest.props),
    };
  }
  return JSON.stringify(schemas, null, 2) + '\n';
}

// ---------------------------------------------------------------------------
// property-panel.ts — config cho Property Inspector (§1, §4.3 cột "Control Inspector").
// ---------------------------------------------------------------------------

const CONTROL_BY_KIND: Record<PropDef['kind'], string> = {
  text: 'text',
  richText: 'lexical',
  number: 'number',
  boolean: 'switch',
  select: 'select',
  color: 'color-token',
  image: 'media-picker',
  icon: 'icon-picker',
  link: 'link-picker',
  list: 'repeater',
  group: 'fieldset',
  binding: 'binding-builder',
};

function propToPanelFieldExpr(name: string, prop: PropDef): string {
  const common = `name: ${JSON.stringify(name)}, kind: ${JSON.stringify(prop.kind)}, control: ${JSON.stringify(CONTROL_BY_KIND[prop.kind])}, label: ${JSON.stringify(prop.label)}, group: ${JSON.stringify(prop.group)}, order: ${prop.order}, editableInSystemPage: ${String(prop.editableInSystemPage)}${prop.help ? `, help: ${JSON.stringify(prop.help)}` : ''}`;

  switch (prop.kind) {
    case 'text':
      return `{ ${common}${prop.maxLength ? `, maxLength: ${prop.maxLength}` : ''}${prop.multiline ? ', multiline: true' : ''} }`;
    case 'richText':
      return `{ ${common}, profile: ${JSON.stringify(prop.profile)}${prop.maxLength ? `, maxLength: ${prop.maxLength}` : ''} }`;
    case 'number':
      return `{ ${common}${prop.min !== undefined ? `, min: ${prop.min}` : ''}${prop.max !== undefined ? `, max: ${prop.max}` : ''}${prop.step !== undefined ? `, step: ${prop.step}` : ''} }`;
    case 'select':
      return `{ ${common}, options: ${JSON.stringify(prop.options)} }`;
    case 'image':
      return `{ ${common}, preset: ${JSON.stringify(prop.preset)} }`;
    case 'link':
      return `{ ${common}, allowKinds: ${JSON.stringify(prop.allowKinds)} }`;
    case 'binding':
      return `{ ${common}, sources: ${JSON.stringify(prop.sources)} }`;
    case 'list':
      return `{ ${common}, itemFields: ${propsToPanelFieldsArrayExpr(prop.itemProps)} }`;
    case 'group':
      return `{ ${common}, fields: ${propsToPanelFieldsArrayExpr(prop.props)} }`;
    case 'boolean':
    case 'color':
    case 'icon':
      return `{ ${common} }`;
  }
}

function propsToPanelFieldsArrayExpr(props: Record<string, PropDef>): string {
  const entries = Object.entries(props)
    .sort(([, a], [, b]) => a.order - b.order)
    .map(([name, prop]) => propToPanelFieldExpr(name, prop));
  return `[\n${entries.map((e) => `    ${e},`).join('\n')}\n  ]`;
}

function generatePropertyPanel(manifests: ComponentManifest[]): string {
  const lines: string[] = ['// GENERATED — DO NOT EDIT (pnpm gen:registry)', ''];

  for (const manifest of manifests) {
    const varName = `${manifest.type.charAt(0).toLowerCase()}${manifest.type.slice(1)}PropertyPanel`;
    lines.push(`export const ${varName} = ${propsToPanelFieldsArrayExpr(manifest.props)};`, '');
  }

  const entries = manifests
    .map((m) => `  ${JSON.stringify(m.type)}: ${m.type.charAt(0).toLowerCase()}${m.type.slice(1)}PropertyPanel,`)
    .join('\n');
  lines.push(`export const propertyPanel = {\n${entries}\n};`, '');

  return lines.join('\n');
}

// ---------------------------------------------------------------------------
// op-rules.ts — input cho Operations Engine THẬT (Bước 5). Ở đây chỉ sinh DATA,
// không enforce gì (§2 — "Operations Engine thật (chỉ sinh op-rules.ts để bước 5 dùng)").
// ---------------------------------------------------------------------------

function collectEditableInSystemPagePaths(props: Record<string, PropDef>, prefix = ''): string[] {
  const paths: string[] = [];
  for (const [name, prop] of Object.entries(props)) {
    const path = prefix ? `${prefix}.${name}` : name;
    if (prop.editableInSystemPage) paths.push(path);
    if (prop.kind === 'group') paths.push(...collectEditableInSystemPagePaths(prop.props, path));
    if (prop.kind === 'list') paths.push(...collectEditableInSystemPagePaths(prop.itemProps, `${path}[]`));
  }
  return paths;
}

function generateOpRules(manifests: ComponentManifest[]): string {
  const lines: string[] = [
    '// GENERATED — DO NOT EDIT (pnpm gen:registry)',
    '// Dữ liệu cho Operations Engine (Bước 5) — KHÔNG enforce gì ở Bước 2.',
    '',
  ];

  const entries = manifests
    .map(
      (m) => `  ${JSON.stringify(m.type)}: {
    acceptsChildren: ${String(m.acceptsChildren)},
    allowedChildTypes: ${JSON.stringify(m.allowedChildTypes)},
    allowedInPageKinds: ${JSON.stringify(m.allowedInPageKinds)},
    maxPerPage: ${JSON.stringify(m.maxPerPage)},
    editableInSystemPageProps: ${JSON.stringify(collectEditableInSystemPagePaths(m.props))},
  },`,
    )
    .join('\n');

  lines.push(`export const opRules = {\n${entries}\n};`, '');
  return lines.join('\n');
}

// ---------------------------------------------------------------------------
// ai-tool-schema.json (§12, #14/#16) — enum type/variant, KHÔNG thể bịa (đúng #17).
// ---------------------------------------------------------------------------

function generateAiToolSchema(manifests: ComponentManifest[]): string {
  const componentTypes = manifests.map((m) => m.type);
  const variantKeys = manifests.flatMap((m) => m.variants.map((v) => v.key));

  const aiSummaryTemplates: Record<string, string> = {};
  for (const m of manifests) aiSummaryTemplates[m.type] = m.aiSummary;

  const schema = {
    add_component: {
      componentType: { enum: componentTypes },
      variant: { enum: variantKeys },
    },
    update_component_props: {
      componentId: { type: 'string', pattern: '^c_[a-zA-Z0-9_-]{5,}$' },
    },
    remove_component: {
      componentId: { type: 'string', pattern: '^c_[a-zA-Z0-9_-]{5,}$' },
    },
    move_component: {
      componentId: { type: 'string', pattern: '^c_[a-zA-Z0-9_-]{5,}$' },
      toIndex: { type: 'integer', minimum: 0 },
    },
    aiSummaryTemplates,
  };

  return JSON.stringify(schema, null, 2) + '\n';
}

// ---------------------------------------------------------------------------
// registry-map.ts
// ---------------------------------------------------------------------------

function generateRegistryMap(manifests: ComponentManifest[]): string {
  const imports: string[] = [];
  const entries: string[] = [];

  for (const manifest of manifests) {
    const folder = toKebabCase(manifest.type);
    for (const variant of manifest.variants) {
      const importName = `${manifest.type}_${variant.key}`;
      imports.push(`import ${importName} from '../src/${folder}/${variant.key}';`);
      entries.push(`  ${JSON.stringify(`${manifest.type}/${variant.key}`)}: ${importName},`);
    }
  }

  return [
    '// GENERATED — DO NOT EDIT (pnpm gen:registry)',
    ...imports,
    '',
    "import type { ComponentType as ReactComponentType } from 'react';",
    '',
    'export const registryMap: Record<string, ReactComponentType<any>> = {',
    ...entries,
    '};',
    '',
  ].join('\n');
}

// ---------------------------------------------------------------------------

async function main() {
  const manifests = await loadManifests(PKG_ROOT);
  checkCrossManifestInvariants(manifests);

  // check-additive (#62) — TRƯỚC khi ghi artifact, để không sinh output cho một manifest
  // đã vi phạm additive-only. Chưa có lock file (lần đầu) → bỏ qua, tạo mới bên dưới.
  if (existsSync(LOCK_FILE)) {
    const { ok, violations, warnings } = await runCheckAdditive();
    for (const warning of warnings) console.warn(`[gen-registry] CẢNH BÁO: ${warning}`);
    if (!ok) {
      fail(`vi phạm additive-only (#43, #62):\n${violations.map((v) => `  - ${v}`).join('\n')}\n\nCần đổi phá vỡ thật? Tạo type/variant MỚI, không sửa cái cũ (§5).`);
    }
  }

  mkdirSync(GENERATED_DIR, { recursive: true });
  writeFileSync(path.join(GENERATED_DIR, 'component-types.ts'), generateComponentTypes(manifests));
  writeFileSync(path.join(GENERATED_DIR, 'props-schemas.ts'), generatePropsSchemas(manifests));
  writeFileSync(path.join(GENERATED_DIR, 'props-schemas.json'), generatePropsSchemasJson(manifests));
  writeFileSync(path.join(GENERATED_DIR, 'property-panel.ts'), generatePropertyPanel(manifests));
  writeFileSync(path.join(GENERATED_DIR, 'op-rules.ts'), generateOpRules(manifests));
  writeFileSync(path.join(GENERATED_DIR, 'ai-tool-schema.json'), generateAiToolSchema(manifests));
  writeFileSync(path.join(GENERATED_DIR, 'registry-map.ts'), generateRegistryMap(manifests));

  if (!existsSync(LOCK_FILE)) {
    writeFileSync(LOCK_FILE, JSON.stringify(buildLockSnapshot(manifests), null, 2) + '\n');
    console.log('[gen-registry] registry.lock.json chưa tồn tại — đã tạo mới (lần đầu).');
  }

  console.log(`[gen-registry] OK — ${manifests.length} manifest, 7 artifact sinh ra tại generated/`);
}

main().catch((error: unknown) => {
  console.error(error);
  process.exit(1);
});
