import { z } from 'zod';
import type { PropDef } from './prop-kinds';

/**
 * Meta-schema (07-component-manifest-schema.md §4) — validate shape của MỘT manifest.
 * Các invariant CHÉO giữa nhiều manifest (§6.1) không nằm ở đây — xem scripts/gen-registry.ts.
 */

const propDefBaseShape = {
  label: z.string().min(1),
  help: z.string().optional(),
  group: z.string().min(1),
  order: z.number().int(),
  editableInSystemPage: z.boolean(),
  since: z.string().min(1),
  localizable: z.boolean().optional(),
};

const textPropDefSchema = z.object({
  kind: z.literal('text'),
  maxLength: z.number().int().positive().optional(),
  multiline: z.boolean().optional(),
  default: z.string().optional(),
  ...propDefBaseShape,
});

const richTextPropDefSchema = z.object({
  kind: z.literal('richText'),
  profile: z.string().min(1),
  maxLength: z.number().int().positive().optional(),
  ...propDefBaseShape,
});

const numberPropDefSchema = z.object({
  kind: z.literal('number'),
  min: z.number().optional(),
  max: z.number().optional(),
  step: z.number().optional(),
  unit: z.string().optional(),
  default: z.number().optional(),
  ...propDefBaseShape,
});

const booleanPropDefSchema = z.object({
  kind: z.literal('boolean'),
  default: z.boolean().optional(),
  ...propDefBaseShape,
});

const selectPropDefSchema = z.object({
  kind: z.literal('select'),
  options: z.array(z.object({ value: z.string(), label: z.string() })).min(1),
  default: z.string().optional(),
  ...propDefBaseShape,
});

const colorPropDefSchema = z.object({
  kind: z.literal('color'),
  allowCustom: z.boolean(),
  default: z.string().optional(),
  ...propDefBaseShape,
});

const imagePropDefSchema = z.object({
  kind: z.literal('image'),
  preset: z.string().min(1),
  allowFocalPoint: z.boolean().optional(),
  ...propDefBaseShape,
});

const iconPropDefSchema = z.object({
  kind: z.literal('icon'),
  set: z.literal('lucide'),
  ...propDefBaseShape,
});

const linkKindSchema = z.enum(['page', 'systemPage', 'productCategory', 'external', 'anchor']);

const linkPropDefSchema = z.object({
  kind: z.literal('link'),
  allowKinds: z.array(linkKindSchema).min(1),
  ...propDefBaseShape,
});

const bindingPropDefSchema = z.object({
  kind: z.literal('binding'),
  sources: z.array(z.string()).min(1),
  allowFilters: z.array(z.string()).optional(),
  ...propDefBaseShape,
});

// z.discriminatedUnion cần MỖI thành viên là một ZodObject thật, có thể đọc `.shape.kind` ngay lúc
// xây dựng schema — không được là z.lazy(). Đệ quy vì vậy phải nằm ở FIELD (props/itemProps), dùng
// một "forward ref" (gán sau khi propDefSchema đã tồn tại) để phá vòng lặp khởi tạo tuần tự của JS.
// phải là `let`: gán một lần NHƯNG tách khỏi khai báo, vì giá trị (propDefSchema) chưa tồn tại
// tại điểm khai báo này (forward reference).
// eslint-disable-next-line prefer-const
let propDefSchemaRef: z.ZodType<unknown>;

const groupPropDefSchema = z.object({
  kind: z.literal('group'),
  props: z.record(z.string(), z.lazy(() => propDefSchemaRef)),
  ...propDefBaseShape,
});

// list KHÔNG được lồng list (§4.3) — itemProps chỉ nhận union không có 'list'. Tại đây group đã là
// ZodObject thật (khởi tạo xong ở trên) nên discriminatedUnion đọc được `.shape.kind` bình thường.
const nonListPropDefSchema = z.discriminatedUnion('kind', [
  textPropDefSchema,
  richTextPropDefSchema,
  numberPropDefSchema,
  booleanPropDefSchema,
  selectPropDefSchema,
  colorPropDefSchema,
  imagePropDefSchema,
  iconPropDefSchema,
  linkPropDefSchema,
  groupPropDefSchema,
  bindingPropDefSchema,
]);

const listPropDefSchema = z.object({
  kind: z.literal('list'),
  itemProps: z.record(z.string(), nonListPropDefSchema),
  itemLabel: z.string().optional(),
  minItems: z.number().int().min(0).optional(),
  maxItems: z.number().int().positive().optional(),
  ...propDefBaseShape,
});

export const propDefSchema = z.discriminatedUnion('kind', [
  textPropDefSchema,
  richTextPropDefSchema,
  numberPropDefSchema,
  booleanPropDefSchema,
  selectPropDefSchema,
  colorPropDefSchema,
  imagePropDefSchema,
  iconPropDefSchema,
  linkPropDefSchema,
  listPropDefSchema,
  groupPropDefSchema,
  bindingPropDefSchema,
]);

// Giải quyết forward ref: group.props giờ trỏ đúng vào propDefSchema đầy đủ (kể cả list/group).
propDefSchemaRef = propDefSchema;

export const variantDefSchema = z.object({
  key: z.string().regex(/^[A-Z][a-zA-Z]*\d+$/, 'variant.key phải PascalCase + số, vd. Hero01'),
  label: z.string().min(1),
  preview: z.string().min(1),
  usesProps: z.array(z.string()),
  requiresProps: z.array(z.string()),
  since: z.string().min(1),
  deprecated: z.boolean().optional(),
});

export const componentManifestSchema = z
  .object({
    type: z.string().regex(/^[A-Z][a-zA-Z0-9]*$/, 'type phải PascalCase'),
    category: z.enum(['Layout', 'Content', 'Media', 'Commerce', 'Service', 'Form', 'Social']),
    label: z.string().min(1),
    description: z.string().min(1),
    icon: z.string().min(1),
    acceptsChildren: z.boolean(),
    allowedChildTypes: z.array(z.string()).nullable(),
    allowedInPageKinds: z.array(z.enum(['Composable', 'System'])).min(1),
    maxPerPage: z.number().int().positive().nullable(),
    variants: z.array(variantDefSchema).min(1),
    props: z.record(z.string(), propDefSchema),
    aiSummary: z.string().min(1),
    since: z.string().min(1),
  })
  .superRefine((manifest, ctx) => {
    // requiresProps phải là tập con của usesProps (invariant #3 ở §6.1 — kiểm ngay per-manifest
    // vì đây là quan hệ NỘI BỘ một manifest, không phải invariant chéo giữa nhiều manifest).
    for (const [i, variant] of manifest.variants.entries()) {
      for (const propName of variant.requiresProps) {
        if (!variant.usesProps.includes(propName)) {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            message: `variants[${i}] (${variant.key}): requiresProps '${propName}' không có trong usesProps`,
            path: ['variants', i, 'requiresProps'],
          });
        }
        if (!(propName in manifest.props)) {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            message: `variants[${i}] (${variant.key}): requiresProps '${propName}' không có trong props`,
            path: ['variants', i, 'requiresProps'],
          });
        }
      }
      for (const propName of variant.usesProps) {
        if (!(propName in manifest.props)) {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            message: `variants[${i}] (${variant.key}): usesProps '${propName}' không có trong props`,
            path: ['variants', i, 'usesProps'],
          });
        }
      }
    }

    if (!manifest.acceptsChildren && manifest.allowedChildTypes !== null) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: `acceptsChildren=false nhưng allowedChildTypes không null — mâu thuẫn nội tại`,
        path: ['allowedChildTypes'],
      });
    }

    if (
      manifest.allowedInPageKinds.includes('System') &&
      !Object.values(manifest.props).some((p) => (p as { editableInSystemPage: boolean }).editableInSystemPage)
    ) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: `allowedInPageKinds chứa 'System' nhưng không prop nào editableInSystemPage:true — component vô dụng trên trang System`,
        path: ['allowedInPageKinds'],
      });
    }

    const variantKeys = new Set<string>();
    for (const variant of manifest.variants) {
      if (variantKeys.has(variant.key)) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: `Hai variant trùng key '${variant.key}' trong cùng type '${manifest.type}'`,
          path: ['variants'],
        });
      }
      variantKeys.add(variant.key);
    }
  });

/**
 * Type "chính thức" dùng bởi manifest files (`satisfies ComponentManifest`) và bởi gen-registry.ts.
 * KHÔNG dùng z.infer ở đây — propDefSchema dùng z.lazy + z.ZodType<unknown> để phá vòng lặp đệ quy
 * (group.props ↔ list.itemProps ↔ propDefSchema), nên z.infer sẽ collapse `props` thành
 * `Record<string, unknown>`, không phải union PropDef thật. componentManifestSchema vẫn là nguồn
 * validate RUNTIME; type TypeScript "tĩnh" lấy từ prop-kinds.ts (PropDef) cho chính xác.
 */
export interface VariantDef {
  key: string;
  label: string;
  preview: string;
  usesProps: string[];
  requiresProps: string[];
  since: string;
  deprecated?: boolean;
}

export interface ComponentManifest {
  type: string;
  category: 'Layout' | 'Content' | 'Media' | 'Commerce' | 'Service' | 'Form' | 'Social';
  label: string;
  description: string;
  icon: string;
  acceptsChildren: boolean;
  allowedChildTypes: string[] | null;
  allowedInPageKinds: Array<'Composable' | 'System'>;
  maxPerPage: number | null;
  variants: VariantDef[];
  props: Record<string, PropDef>;
  aiSummary: string;
  since: string;
}
