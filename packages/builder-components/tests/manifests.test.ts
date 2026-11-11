import { describe, expect, it } from 'vitest';
import treeRules from '../generated/tree-rules.json';
import { componentManifests } from '../src/manifests';

describe('componentManifests (nguồn nhãn cho palette)', () => {
  const rules = treeRules.types as Record<string, { variants: string[] }>;

  it('phủ đúng mọi type trong tree-rules — thêm component mà quên danh sách này là fail', () => {
    expect(componentManifests.map((m) => m.type).sort()).toEqual(Object.keys(rules).sort());
  });

  it('variant của manifest khớp variant trong tree-rules, và đều có nhãn', () => {
    for (const manifest of componentManifests) {
      expect(manifest.variants.map((v) => v.key).sort(), manifest.type).toEqual([...(rules[manifest.type]?.variants ?? [])].sort());
      expect(manifest.label.length, manifest.type).toBeGreaterThan(0);
      for (const variant of manifest.variants) expect(variant.label.length, variant.key).toBeGreaterThan(0);
    }
  });
});
