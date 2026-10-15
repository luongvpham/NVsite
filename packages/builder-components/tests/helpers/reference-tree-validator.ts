import Ajv2020 from 'ajv/dist/2020';
import bindingSources from '../../../../config/binding-sources.json';
import propsSchemasJson from '../../generated/props-schemas.json';
import treeEnvelopeJsonSchema from '../../generated/tree-envelope.schema.json';
import treeRulesJson from '../../generated/tree-rules.json';
import { treeEnvelopeSchema } from '../../meta/envelope-schema';
import { sanitizeRichText } from '../../src/sanitize-html';
import { classifyDangerous } from './richtext-danger';

/**
 * Hiện thực THAM CHIẾU (TypeScript, CHỈ TRONG TEST) của luật validate cây — mục đích duy nhất là chứng minh
 * các fixture `tests/fixtures/tree/*.json` tự nhất quán: nhãn `expect`/`errorCodes` của từng fixture là điều
 * thật sự xảy ra khi áp `tree-rules.json` + `props-schemas.json` + `tree-envelope.schema.json` + sanitize.
 * BE .NET (T4/T5b) hiện thực lại độc lập và chạy CÙNG fixture; file này không được BE hay FE production import.
 */

export interface TreeRuleProp {
  kind: string;
  profile?: string;
  sources?: string[];
  preset?: string;
  itemProps?: Record<string, TreeRuleProp>;
}
export interface TreeTypeRule {
  acceptsChildren: boolean;
  allowedChildTypes: string[] | null;
  allowedInPageKinds: string[];
  maxPerPage: number | null;
  editableInSystemPageProps: string[];
  variants: string[];
  props: Record<string, TreeRuleProp>;
}
export type TreeRules = { schemaVersion: number; types: Record<string, TreeTypeRule> };

export interface MediaContextEntry {
  kind: 'Clone' | 'Direct' | 'Library';
  shop?: 'own' | 'other';
  deleted?: boolean;
}
export interface FixtureContext {
  media?: Record<string, MediaContextEntry>;
}
export interface ValidateInput {
  tree: unknown;
  pageKind: 'Composable' | 'System';
  context?: FixtureContext;
  rulesOverride?: { types: Record<string, TreeTypeRule> };
}
export interface ValidateResult {
  codes: Set<string>;
  /** Cây sau khi thay richText bằng bản sạch (chỉ có khi không lỗi). */
  tree?: unknown;
  sanitized: boolean;
}

export const CURRENT_SCHEMA_VERSION = 1;
const rules = treeRulesJson as unknown as TreeRules;
const ajv = new Ajv2020({ strict: false });
const validateEnvelope = ajv.compile(treeEnvelopeJsonSchema);
const propsValidators = new Map<string, ReturnType<typeof ajv.compile>>();
const bindingWhitelist: string[] = bindingSources;

function propsValidatorFor(type: string) {
  let v = propsValidators.get(type);
  if (!v) {
    v = ajv.compile((propsSchemasJson as Record<string, object>)[type] as object);
    propsValidators.set(type, v);
  }
  return v;
}

interface Node {
  id: string;
  type: string;
  variant?: string;
  props?: Record<string, unknown>;
  children?: Node[];
  i18n?: unknown;
}

const isRecord = (v: unknown): v is Record<string, unknown> => typeof v === 'object' && v !== null && !Array.isArray(v);

/** Quy ước `imageId` hợp lệ: có trong context.media, kind Clone|Direct, cùng shop, chưa xoá. Thiếu entry = không hợp lệ. */
function imageIdValid(imageId: string, context: FixtureContext): boolean {
  const entry = context.media?.[imageId];
  if (!entry) return false;
  return (entry.kind === 'Clone' || entry.kind === 'Direct') && (entry.shop ?? 'own') === 'own' && !entry.deleted;
}

export function validateTree(input: ValidateInput): ValidateResult {
  const codes = new Set<string>();
  const context = input.context ?? {};
  const typeRules: Record<string, TreeTypeRule> = { ...rules.types, ...(input.rulesOverride?.types ?? {}) };
  const overrideTypes = new Set(Object.keys(input.rulesOverride?.types ?? {}));

  // 1. Phiên bản + vỏ
  const sv = isRecord(input.tree) ? input.tree.schemaVersion : undefined;
  if (typeof sv === 'number' && Number.isInteger(sv) && sv > CURRENT_SCHEMA_VERSION && validateEnvelope(input.tree)) {
    return { codes: new Set(['SCHEMA_VERSION_UNSUPPORTED']), sanitized: false };
  }
  const envelopeZod = treeEnvelopeSchema.safeParse(input.tree).success;
  if (!envelopeZod || !validateEnvelope(input.tree)) {
    return { codes: new Set(['TREE_ENVELOPE_INVALID']), sanitized: false };
  }

  // 2. Duyệt LẶP (stack tường minh) — không đệ quy vô hạn (R8)
  const tree = structuredClone(input.tree) as { children: Node[] };
  const seenIds = new Set<string>(['root']);
  const perType = new Map<string, number>();
  const imageIds = new Set<string>();
  let sanitized = false;

  const visitProps = (props: Record<string, unknown>, ruleProps: Record<string, TreeRuleProp>): void => {
    const stack: Array<{ value: Record<string, unknown>; rule: Record<string, TreeRuleProp> }> = [{ value: props, rule: ruleProps }];
    while (stack.length > 0) {
      const frame = stack.pop();
      if (!frame) break;
      for (const [name, value] of Object.entries(frame.value)) {
        const rule = frame.rule[name];
        if (!rule) {
          codes.add('PROP_UNKNOWN');
          continue;
        }
        if (value === undefined || value === null) continue;
        switch (rule.kind) {
          case 'image':
            if (isRecord(value) && typeof value.imageId === 'string') imageIds.add(value.imageId);
            break;
          case 'binding':
            if (isRecord(value)) {
              const source = value.source;
              if (source !== undefined) {
                if (typeof source !== 'string' || !bindingWhitelist.includes(source) || !(rule.sources ?? []).includes(source)) {
                  codes.add('BINDING_SOURCE_NOT_ALLOWED');
                }
              }
            }
            break;
          case 'richText':
            if (typeof value === 'string' && rule.profile) {
              if (classifyDangerous(value) === 'unsafe') {
                codes.add('RICHTEXT_UNSAFE');
              } else {
                const clean = sanitizeRichText(value, rule.profile);
                if (clean !== value) {
                  frame.value[name] = clean;
                  sanitized = true;
                }
              }
            }
            break;
          case 'group':
            if (isRecord(value) && rule.itemProps) stack.push({ value, rule: rule.itemProps });
            break;
          case 'list':
            if (Array.isArray(value) && rule.itemProps) {
              for (const item of value) if (isRecord(item)) stack.push({ value: item, rule: rule.itemProps });
            }
            break;
          default:
            break;
        }
      }
    }
  };

  const nodeStack: Array<{ node: Node; parent: Node | null }> = [...tree.children].reverse().map((node) => ({ node, parent: null }));
  while (nodeStack.length > 0) {
    const frame = nodeStack.pop();
    if (!frame) break;
    const { node, parent } = frame;

    if (seenIds.has(node.id)) codes.add('NODE_ID_DUPLICATE');
    seenIds.add(node.id);

    const rule = typeRules[node.type];
    if (!rule) {
      codes.add('NODE_TYPE_UNKNOWN');
    } else {
      perType.set(node.type, (perType.get(node.type) ?? 0) + 1);
      if (node.variant !== undefined && !rule.variants.includes(node.variant)) codes.add('NODE_VARIANT_UNKNOWN');
      if (!rule.allowedInPageKinds.includes(input.pageKind)) codes.add('NODE_NOT_ALLOWED_IN_PAGE_KIND');
      if (node.children && node.children.length > 0) {
        if (!rule.acceptsChildren) codes.add('NODE_CHILDREN_NOT_ACCEPTED');
        else if (rule.allowedChildTypes) {
          for (const child of node.children) if (!rule.allowedChildTypes.includes(child.type)) codes.add('NODE_CHILD_TYPE_NOT_ALLOWED');
        }
      }
      if (parent) {
        const parentRule = typeRules[parent.type];
        if (parentRule?.allowedChildTypes && !parentRule.allowedChildTypes.includes(node.type)) codes.add('NODE_CHILD_TYPE_NOT_ALLOWED');
      }
      if (node.i18n !== undefined) codes.add('I18N_NOT_ENABLED');

      if (node.props !== undefined) {
        if (!overrideTypes.has(node.type) && !propsValidatorFor(node.type)(node.props)) codes.add('PROP_INVALID');
        visitProps(node.props, rule.props);
      }
    }
    for (const child of [...(node.children ?? [])].reverse()) nodeStack.push({ node: child, parent: node });
  }

  for (const [type, count] of perType) {
    const max = typeRules[type]?.maxPerPage;
    if (max !== null && max !== undefined && count > max) codes.add('NODE_MAX_PER_PAGE_EXCEEDED');
  }

  // 3. Ảnh (cần DB ở BE; ở đây tra context.media)
  for (const imageId of imageIds) if (!imageIdValid(imageId, context)) codes.add('MEDIA_INVALID_IMAGE_REFERENCE');

  return { codes, tree: codes.size === 0 ? tree : undefined, sanitized };
}

// ---------------------------------------------------------------------------
// SystemPageGuard (T5b): cùng tập id, type, variant, thứ tự, cha-con; prop khác editableInSystemPageProps phải bằng nhau.
// ---------------------------------------------------------------------------

function canonical(value: unknown): string {
  if (Array.isArray(value)) return `[${value.map(canonical).join(',')}]`;
  if (isRecord(value)) {
    return `{${Object.keys(value)
      .sort()
      .map((k) => `${JSON.stringify(k)}:${canonical(value[k])}`)
      .join(',')}}`;
  }
  return JSON.stringify(value);
}

function flatten(root: { children: Node[] }): Array<{ node: Node; parentId: string; index: number }> {
  const out: Array<{ node: Node; parentId: string; index: number }> = [];
  const stack: Array<{ node: Node; parentId: string; index: number }> = [...root.children].map((node, index) => ({ node, parentId: 'root', index })).reverse();
  while (stack.length > 0) {
    const frame = stack.pop();
    if (!frame) break;
    out.push(frame);
    (frame.node.children ?? []).map((node, index) => ({ node, parentId: frame.node.id, index })).reverse().forEach((f) => stack.push(f));
  }
  return out;
}

export function compareSystemPage(stored: unknown, incoming: unknown): Set<string> {
  const codes = new Set<string>();
  const a = flatten(stored as { children: Node[] });
  const b = flatten(incoming as { children: Node[] });
  const shape = (xs: typeof a) => xs.map((x) => `${x.node.id}|${x.node.type}|${x.node.variant ?? ''}|${x.parentId}|${x.index}`).join(';');
  if (shape(a) !== shape(b)) {
    codes.add('SYSTEM_PAGE_STRUCTURE_CHANGED');
    return codes;
  }
  for (let i = 0; i < a.length; i++) {
    const sa = a[i]?.node;
    const sb = b[i]?.node;
    if (!sa || !sb) continue;
    const editable = new Set((typeRules(sa.type) ?? { editableInSystemPageProps: [] }).editableInSystemPageProps);
    const keys = new Set([...Object.keys(sa.props ?? {}), ...Object.keys(sb.props ?? {})]);
    for (const key of keys) {
      if (editable.has(key)) continue;
      if (canonical((sa.props ?? {})[key]) !== canonical((sb.props ?? {})[key])) codes.add('SYSTEM_PAGE_PROP_NOT_EDITABLE');
    }
  }
  return codes;
}

function typeRules(type: string): TreeTypeRule | undefined {
  return rules.types[type];
}
