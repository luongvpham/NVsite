import { describe, expect, it } from 'vitest';
import { componentManifestSchema } from './manifest-schema';

function baseManifest(overrides: Record<string, unknown> = {}) {
  return {
    type: 'Test',
    category: 'Content',
    label: 'Test',
    description: 'Test component',
    icon: 'Box',
    acceptsChildren: false,
    allowedChildTypes: null,
    allowedInPageKinds: ['Composable'],
    maxPerPage: null,
    aiSummary: 'Test',
    since: '1.0.0',
    variants: [
      { key: 'Test01', label: 'Test01', preview: 'x.webp', usesProps: ['title'], requiresProps: ['title'], since: '1.0.0' },
    ],
    props: {
      title: { kind: 'text', label: 'Title', group: 'Nội dung', order: 1, editableInSystemPage: false, since: '1.0.0' },
    },
    ...overrides,
  };
}

describe('componentManifestSchema — shape hợp lệ', () => {
  it('accepts a minimal valid manifest', () => {
    expect(componentManifestSchema.safeParse(baseManifest()).success).toBe(true);
  });

  it.each([
    ['text', { kind: 'text', label: 'L', group: 'g', order: 1, editableInSystemPage: false, since: '1.0.0' }],
    ['richText', { kind: 'richText', profile: 'inline', label: 'L', group: 'g', order: 1, editableInSystemPage: false, since: '1.0.0' }],
    ['number', { kind: 'number', min: 0, max: 10, label: 'L', group: 'g', order: 1, editableInSystemPage: false, since: '1.0.0' }],
    ['boolean', { kind: 'boolean', default: true, label: 'L', group: 'g', order: 1, editableInSystemPage: false, since: '1.0.0' }],
    ['select', { kind: 'select', options: [{ value: 'a', label: 'A' }], label: 'L', group: 'g', order: 1, editableInSystemPage: false, since: '1.0.0' }],
    ['color', { kind: 'color', allowCustom: false, label: 'L', group: 'g', order: 1, editableInSystemPage: false, since: '1.0.0' }],
    ['image', { kind: 'image', preset: '1600x900,cover', label: 'L', group: 'g', order: 1, editableInSystemPage: false, since: '1.0.0' }],
    ['icon', { kind: 'icon', set: 'lucide', label: 'L', group: 'g', order: 1, editableInSystemPage: false, since: '1.0.0' }],
    ['link', { kind: 'link', allowKinds: ['external'], label: 'L', group: 'g', order: 1, editableInSystemPage: false, since: '1.0.0' }],
    ['binding', { kind: 'binding', sources: ['Service'], label: 'L', group: 'g', order: 1, editableInSystemPage: false, since: '1.0.0' }],
    ['group', { kind: 'group', props: { inner: { kind: 'text', label: 'L', group: 'g', order: 1, editableInSystemPage: false, since: '1.0.0' } }, label: 'L', group: 'g', order: 1, editableInSystemPage: false, since: '1.0.0' }],
    ['list', { kind: 'list', itemProps: { inner: { kind: 'text', label: 'L', group: 'g', order: 1, editableInSystemPage: false, since: '1.0.0' } }, label: 'L', group: 'g', order: 1, editableInSystemPage: false, since: '1.0.0' }],
  ])('accepts a valid "%s" prop', (_kind, propDef) => {
    const manifest = baseManifest({
      variants: [{ key: 'Test01', label: 'Test01', preview: 'x.webp', usesProps: ['p'], requiresProps: [], since: '1.0.0' }],
      props: { p: propDef },
    });
    const result = componentManifestSchema.safeParse(manifest);
    expect(result.success, result.success ? undefined : JSON.stringify(result.error.issues)).toBe(true);
  });

  it('rejects "image" prop missing preset', () => {
    const manifest = baseManifest({
      variants: [{ key: 'Test01', label: 'Test01', preview: 'x.webp', usesProps: ['p'], requiresProps: [], since: '1.0.0' }],
      props: { p: { kind: 'image', label: 'L', group: 'g', order: 1, editableInSystemPage: false, since: '1.0.0' } },
    });
    expect(componentManifestSchema.safeParse(manifest).success).toBe(false);
  });

  it('rejects "list" whose itemProps contains a nested "list" (§4.3 — no nesting)', () => {
    const manifest = baseManifest({
      variants: [{ key: 'Test01', label: 'Test01', preview: 'x.webp', usesProps: ['p'], requiresProps: [], since: '1.0.0' }],
      props: {
        p: {
          kind: 'list',
          label: 'L',
          group: 'g',
          order: 1,
          editableInSystemPage: false,
          since: '1.0.0',
          itemProps: {
            nested: { kind: 'list', label: 'L2', group: 'g', order: 1, editableInSystemPage: false, since: '1.0.0', itemProps: {} },
          },
        },
      },
    });
    expect(componentManifestSchema.safeParse(manifest).success).toBe(false);
  });
});

describe('componentManifestSchema — invariant nội bộ (§6.1 phần intra-manifest)', () => {
  it('rejects requiresProps not present in usesProps', () => {
    const manifest = baseManifest({
      variants: [{ key: 'Test01', label: 'Test01', preview: 'x.webp', usesProps: [], requiresProps: ['title'], since: '1.0.0' }],
    });
    expect(componentManifestSchema.safeParse(manifest).success).toBe(false);
  });

  it('rejects usesProps referencing an undeclared prop', () => {
    const manifest = baseManifest({
      variants: [{ key: 'Test01', label: 'Test01', preview: 'x.webp', usesProps: ['nope'], requiresProps: [], since: '1.0.0' }],
    });
    expect(componentManifestSchema.safeParse(manifest).success).toBe(false);
  });

  it('rejects acceptsChildren:false with non-null allowedChildTypes', () => {
    const manifest = baseManifest({ acceptsChildren: false, allowedChildTypes: ['Hero'] });
    expect(componentManifestSchema.safeParse(manifest).success).toBe(false);
  });

  it('rejects allowedInPageKinds containing System with no editableInSystemPage:true prop', () => {
    const manifest = baseManifest({ allowedInPageKinds: ['Composable', 'System'] });
    expect(componentManifestSchema.safeParse(manifest).success).toBe(false);
  });

  it('rejects two variants with the same key', () => {
    const manifest = baseManifest({
      variants: [
        { key: 'Test01', label: 'A', preview: 'x.webp', usesProps: ['title'], requiresProps: [], since: '1.0.0' },
        { key: 'Test01', label: 'B', preview: 'y.webp', usesProps: ['title'], requiresProps: [], since: '1.0.0' },
      ],
    });
    expect(componentManifestSchema.safeParse(manifest).success).toBe(false);
  });
});
