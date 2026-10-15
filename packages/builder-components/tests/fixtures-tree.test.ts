import fg from 'fast-glob';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';
import { compareSystemPage, validateTree, type FixtureContext, type TreeTypeRule } from './helpers/reference-tree-validator';

/**
 * WEBSITE-001 S0-T4 — bộ fixture Tree dùng chung FE+BE (xem tests/fixtures/tree/README.md).
 * Test này chứng minh từng fixture TỰ NHẤT QUÁN: áp hiện thực tham chiếu (tests/helpers/reference-tree-validator.ts)
 * lên tree-rules.json + props-schemas.json + vỏ + sanitize ra đúng `expect`/`errorCodes` đã ghi.
 * (Fixture ảnh cần tra DB ở BE thì ở đây tra `context.media`.)
 */
const FIXTURE_DIR = path.resolve(import.meta.dirname, 'fixtures', 'tree');

interface TreeFixture {
  description: string;
  pageKind: 'Composable' | 'System';
  systemType?: string;
  tree?: unknown;
  stored?: unknown;
  incoming?: unknown;
  context?: FixtureContext;
  rulesOverride?: { types: Record<string, TreeTypeRule> };
  expect: 'valid' | 'invalid';
  errorCodes?: string[];
  expectedTree?: unknown;
  expectSanitized?: boolean;
}

const REQUIRED_FILES = [
  'valid-home.json',
  'valid-empty-home.json',
  'valid-system-contact.json',
  'valid-richtext-reformatted.json',
  'valid-system-edit-content.json',
  'invalid-library-image-id.json',
  'invalid-other-shop-image-id.json',
  'invalid-deleted-image-id.json',
  'invalid-binding-review.json',
  'invalid-binding-review-nested.json',
  'invalid-binding-review-in-list.json',
  'invalid-binding-source-not-declared.json',
  'invalid-unknown-prop.json',
  'invalid-prop-wrong-type.json',
  'invalid-i18n-key.json',
  'invalid-unknown-type.json',
  'invalid-unknown-variant.json',
  'invalid-duplicate-id.json',
  'invalid-hero-in-system-page.json',
  'invalid-hero-max-per-page.json',
  'invalid-children-not-accepted.json',
  'invalid-richtext-unsafe.json',
  'invalid-envelope-missing-schema-version.json',
  'invalid-envelope-schema-version-string.json',
  'invalid-envelope-extra-node-field.json',
  'invalid-schema-version-999.json',
  'invalid-system-structure-variant-changed.json',
  'invalid-system-structure-node-added.json',
  'invalid-system-structure-node-removed.json',
  'invalid-system-structure-reordered.json',
  'invalid-system-structure-id-changed.json',
  'invalid-system-prop-not-editable.json',
  'xss-richtext.json',
  'README.md',
];

const treeFixtureFiles = fg
  .sync('*.json', { cwd: FIXTURE_DIR })
  .filter((f) => f !== 'xss-richtext.json')
  .sort();
const read = (f: string) => JSON.parse(readFileSync(path.join(FIXTURE_DIR, f), 'utf8')) as TreeFixture;

describe('fixture Tree dùng chung (S0-T4)', () => {
  it('đủ các file plan §9 S0-T4 yêu cầu + README.md quy ước định dạng', () => {
    for (const f of REQUIRED_FILES) expect(existsSync(path.join(FIXTURE_DIR, f)), f).toBe(true);
  });

  it('README.md ghi mọi mã lỗi được fixture dùng', () => {
    const readme = readFileSync(path.join(FIXTURE_DIR, 'README.md'), 'utf8');
    const used = new Set(treeFixtureFiles.flatMap((f) => read(f).errorCodes ?? []));
    for (const code of used) expect(readme, `README thiếu mã ${code}`).toContain(code);
  });

  it.each(treeFixtureFiles.map((f) => [f] as const))('%s — đúng khuôn và đúng kết quả mong đợi', (file) => {
    const fx = read(file);
    expect(typeof fx.description, 'description').toBe('string');
    expect(['valid', 'invalid'], 'expect').toContain(fx.expect);
    expect(['Composable', 'System'], 'pageKind').toContain(fx.pageKind);
    if (fx.expect === 'invalid') {
      expect(Array.isArray(fx.errorCodes) && fx.errorCodes.length > 0, 'errorCodes').toBe(true);
    } else {
      expect(fx.errorCodes, 'valid không có errorCodes').toBeUndefined();
    }

    const guardMode = fx.stored !== undefined || fx.incoming !== undefined;
    if (guardMode) {
      // stored/incoming => chỉ SystemPageGuard (T5b), không chạy toàn pipeline.
      expect(fx.pageKind).toBe('System');
      expect(fx.stored !== undefined && fx.incoming !== undefined, 'phải có đủ stored và incoming').toBe(true);
      const codes = compareSystemPage(fx.stored, fx.incoming);
      expect([...codes].sort()).toEqual([...(fx.errorCodes ?? [])].sort());
      return;
    }

    expect(fx.tree, 'tree').toBeDefined();
    const result = validateTree({ tree: fx.tree, pageKind: fx.pageKind, context: fx.context, rulesOverride: fx.rulesOverride });
    expect([...result.codes].sort(), `mã lỗi thực tế của ${file}`).toEqual([...(fx.errorCodes ?? [])].sort());
    if (fx.expect === 'valid') {
      expect(result.sanitized, 'sanitized').toBe(fx.expectSanitized ?? false);
      if (fx.expectedTree !== undefined) expect(result.tree).toEqual(fx.expectedTree);
    }
  });
});
