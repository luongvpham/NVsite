import { describe, expect, it } from 'vitest';
import { opRules } from '@vsite/builder-components';
import { PageKind, type TreeEnvelope, type TreeNode } from '../tree/types';
import { clone, loadFixtures, replayAsAdds, type TreeFixture } from '../test-support/fixtures';
import { executeOperation } from './apply';

/**
 * Bộ test HỢP ĐỒNG với BE: cùng fixture `packages/builder-components/tests/fixtures/tree/*.json` mà BE duyệt
 * (`PageTreeValidatorTests`, `SystemPageGuardTests`). Engine là bản "từ chối sớm" của cùng luật — không thay BE
 * (BE vẫn là nguồn sự thật) nên chỉ kiểm những mã Engine có thể thấy theo từng op.
 */
const fixtures = loadFixtures();
const isPair = (f: TreeFixture) => f.stored !== undefined || f.incoming !== undefined;

/** Mã mà Engine kiểm được theo từng op. */
const ENGINE_CODES = new Set([
  'NODE_TYPE_UNKNOWN',
  'NODE_VARIANT_UNKNOWN',
  'NODE_ID_DUPLICATE',
  'NODE_NOT_ALLOWED_IN_PAGE_KIND',
  'NODE_CHILDREN_NOT_ACCEPTED',
  'NODE_CHILD_TYPE_NOT_ALLOWED',
  'NODE_MAX_PER_PAGE_EXCEEDED',
  'PROP_UNKNOWN',
  'PROP_INVALID',
  'I18N_NOT_ENABLED',
  'BINDING_SOURCE_NOT_ALLOWED',
  'LINK_URL_UNSAFE',
]);

/** Mã Engine CỐ Ý không kiểm — lý do ghi ở changelog WEBSITE-002 (F4). */
const OUT_OF_SCOPE = new Set([
  'TREE_ENVELOPE_INVALID', // vỏ do Zod envelope kiểm lúc nạp draft (F6), không phải việc của op
  'SCHEMA_VERSION_UNSUPPORTED', // idem
  'MEDIA_INVALID_IMAGE_REFERENCE', // cần DB (id Library/Clone/shop khác) — chỉ BE
  'RICHTEXT_UNSAFE', // BE từ chối; FE làm sạch ở control Lexical (F10), bộ phân loại hiện chỉ có trong test
]);

describe('bộ fixture được nạp đủ', () => {
  it('có đủ nhóm valid/invalid và cặp stored/incoming', () => {
    expect(fixtures.length).toBeGreaterThanOrEqual(40);
    expect(fixtures.some((f) => f.expect === 'valid')).toBe(true);
    expect(fixtures.some(isPair)).toBe(true);
  });
});

const composableTrees = fixtures.filter((f) => f.tree !== undefined && f.pageKind === 'Composable');

/** `children: []` và thiếu `children` cùng nghĩa (Section rỗng) — replay bằng `add` không tạo mảng rỗng. */
function withoutEmptyChildren(nodes: TreeNode[]): TreeNode[] {
  return nodes.map(({ children, ...rest }) =>
    children && children.length > 0 ? { ...rest, children: withoutEmptyChildren(children) } : rest,
  );
}

describe('cây Composable HỢP LỆ: Engine dựng lại được bằng chuỗi op add', () => {
  const valid = composableTrees.filter((f) => f.expect === 'valid');
  it.each(valid.map((f) => [f.file, f] as const))('%s', (_file, fixture) => {
    const source = clone(fixture.tree as TreeEnvelope);
    const result = replayAsAdds(source);
    expect(result.ok, result.ok ? '' : `bị từ chối ở ${result.nodeId}: ${result.code}`).toBe(true);
    if (result.ok) expect(withoutEmptyChildren(result.tree.children)).toEqual(withoutEmptyChildren(source.children));
  });
});

describe('cây Composable KHÔNG hợp lệ: Engine từ chối đúng mã của fixture', () => {
  const invalid = composableTrees.filter((f) => f.expect === 'invalid');

  const inScope = invalid.filter((f) => f.rulesOverride === undefined && (f.errorCodes ?? []).every((c) => ENGINE_CODES.has(c)));
  it.each(inScope.map((f) => [f.file, f] as const))('%s', (_file, fixture) => {
    const result = replayAsAdds(clone(fixture.tree as TreeEnvelope));
    expect(result.ok, 'Engine phải từ chối cây này').toBe(false);
    if (!result.ok) expect(fixture.errorCodes).toContain(result.code);
  });

  it('phần còn lại bị bỏ qua có chủ đích — không bỏ sót im lặng', () => {
    const skipped = invalid.filter((f) => !inScope.includes(f));
    for (const f of skipped) {
      const reason = f.rulesOverride !== undefined ? 'rulesOverride' : (f.errorCodes ?? []).every((c) => OUT_OF_SCOPE.has(c)) ? 'out-of-scope' : 'UNEXPLAINED';
      expect(reason, f.file).not.toBe('UNEXPLAINED');
    }
    // Chốt số lượng để thêm fixture mới buộc người viết xem lại danh sách trên.
    expect(skipped.map((f) => f.file)).toEqual([
      'invalid-binding-review-in-list.json', // dùng type giả `Carousel` qua rulesOverride
      'invalid-deleted-image-id.json',
      'invalid-envelope-extra-node-field.json',
      'invalid-envelope-missing-schema-version.json',
      'invalid-envelope-schema-version-string.json',
      'invalid-library-image-id.json',
      'invalid-other-shop-image-id.json',
      'invalid-richtext-unsafe.json',
      'invalid-schema-version-999.json',
    ]);
  });
});

describe('trang System', () => {
  const editable = (type: string): string[] => (opRules as Record<string, { editableInSystemPageProps: string[] }>)[type]?.editableInSystemPageProps ?? [];
  const flatten = (nodes: TreeNode[]): TreeNode[] => nodes.flatMap((n) => [n, ...flatten(n.children ?? [])]);
  const canonical = (v: unknown): string => JSON.stringify(v, (_k, val: unknown) =>
    val && typeof val === 'object' && !Array.isArray(val)
      ? Object.fromEntries(Object.entries(val).sort(([a], [b]) => a.localeCompare(b)))
      : val);

  /** Op `update` tương ứng với mỗi prop top-level khác nhau giữa stored và incoming (cùng cấu trúc). */
  function diffAsUpdates(stored: TreeEnvelope, incoming: TreeEnvelope) {
    const inc = new Map(flatten(incoming.children).map((n) => [n.id, n]));
    return flatten(stored.children).flatMap((node) => {
      const other = inc.get(node.id);
      const keys = new Set([...Object.keys(node.props ?? {}), ...Object.keys(other?.props ?? {})]);
      return [...keys]
        .filter((key) => canonical(node.props?.[key]) !== canonical(other?.props?.[key]))
        .map((key) => ({ type: 'update' as const, nodeId: node.id, path: [key], value: other?.props?.[key], nodeType: node.type }));
    });
  }

  const pairs = fixtures.filter(isPair);

  it('cặp VALID (sửa content): mọi update tương đương đều được Engine chấp nhận và cho ra đúng incoming', () => {
    const fixture = pairs.find((f) => f.expect === 'valid');
    expect(fixture?.file).toBe('valid-system-edit-content.json');
    let tree = clone(fixture?.stored as TreeEnvelope);
    const updates = diffAsUpdates(tree, fixture?.incoming as TreeEnvelope);
    expect(updates.length).toBeGreaterThan(0);
    for (const { nodeType, ...op } of updates) {
      expect(editable(nodeType)).toContain(op.path[0]);
      const result = executeOperation(op, { tree, pageKind: PageKind.System });
      expect(result.ok, JSON.stringify(op)).toBe(true);
      if (result.ok) tree = result.tree;
    }
    expect(tree.children).toEqual((fixture?.incoming as TreeEnvelope).children);
  });

  it('cặp SYSTEM_PAGE_PROP_NOT_EDITABLE: update tương đương bị từ chối đúng mã', () => {
    const fixture = pairs.find((f) => f.errorCodes?.includes('SYSTEM_PAGE_PROP_NOT_EDITABLE'));
    expect(fixture).toBeDefined();
    const stored = clone(fixture?.stored as TreeEnvelope);
    const updates = diffAsUpdates(stored, fixture?.incoming as TreeEnvelope);
    expect(updates.length).toBeGreaterThan(0);
    const rejected = updates.map(({ nodeType: _t, ...op }) => executeOperation(op, { tree: stored, pageKind: PageKind.System }));
    expect(rejected.some((r) => !r.ok && r.code === 'SYSTEM_PAGE_PROP_NOT_EDITABLE')).toBe(true);
  });

  it('cặp SYSTEM_PAGE_STRUCTURE_CHANGED: mọi op cấu trúc (add/remove/move) bị từ chối đúng mã', () => {
    const structural = pairs.filter((f) => f.errorCodes?.includes('SYSTEM_PAGE_STRUCTURE_CHANGED'));
    expect(structural.length).toBeGreaterThanOrEqual(5);
    for (const fixture of structural) {
      const stored = clone(fixture.stored as TreeEnvelope);
      const first = flatten(stored.children)[0] as TreeNode;
      const ctx = { tree: stored, pageKind: PageKind.System };
      const ops = [
        { type: 'remove', nodeId: first.id },
        { type: 'move', nodeId: first.id, toParentId: 'root', toIndex: 0 },
        { type: 'add', parentId: 'root', index: 0, node: { id: 'c_new01', type: 'RichText', variant: 'RichText01' } },
      ];
      for (const op of ops) {
        expect(executeOperation(op, ctx), `${fixture.file}: ${op.type}`).toMatchObject({ ok: false, code: 'SYSTEM_PAGE_STRUCTURE_CHANGED' });
      }
    }
  });

  it('cây System đơn lẻ: không dựng được bằng add (mọi add bị từ chối trước cả luật node)', () => {
    const systemTrees = fixtures.filter((f) => f.tree !== undefined && f.pageKind === 'System');
    expect(systemTrees.length).toBeGreaterThanOrEqual(2);
    for (const fixture of systemTrees) {
      const result = replayAsAdds(clone(fixture.tree as TreeEnvelope), PageKind.System);
      expect(result, fixture.file).toMatchObject({ ok: false, code: 'SYSTEM_PAGE_STRUCTURE_CHANGED' });
    }
  });

  it('cây System hợp lệ nạp thẳng: update prop editable được, prop khác bị cấm', () => {
    const fixture = fixtures.find((f) => f.file === 'valid-system-contact.json');
    const tree = clone(fixture?.tree as TreeEnvelope);
    const nodes = flatten(tree.children);
    let allowed = 0;
    let denied = 0;
    for (const node of nodes) {
      const props = Object.keys(node.props ?? {});
      for (const key of props) {
        const result = executeOperation(
          { type: 'update', nodeId: node.id, path: [key], value: node.props?.[key] },
          { tree, pageKind: PageKind.System },
        );
        if (editable(node.type).includes(key)) {
          expect(result.ok, `${node.id}.${key}`).toBe(true);
          allowed += 1;
        } else {
          expect(result, `${node.id}.${key}`).toMatchObject({ ok: false, code: 'SYSTEM_PAGE_PROP_NOT_EDITABLE' });
          denied += 1;
        }
      }
    }
    expect(allowed).toBeGreaterThan(0);
    expect(denied + allowed).toBeGreaterThan(0);
  });
});
