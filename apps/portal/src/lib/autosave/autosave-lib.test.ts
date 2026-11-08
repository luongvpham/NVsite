import { describe, expect, it } from 'vitest';
import { pointerOf, type TreeEnvelope } from '@vsite/builder-core';
import { treeEnvelopeSchema } from '@vsite/builder-components';
import { classifySaveError } from './classify-save-error';
import { mapTreeErrors } from './map-tree-errors';
import wrongTypeFixture from '../../../../../packages/builder-components/tests/fixtures/tree/invalid-prop-wrong-type.json';
import unknownPropFixture from '../../../../../packages/builder-components/tests/fixtures/tree/invalid-unknown-prop.json';
import validHomeFixture from '../../../../../packages/builder-components/tests/fixtures/tree/valid-home.json';

const parse = (tree: unknown): TreeEnvelope => treeEnvelopeSchema.parse(tree);

describe('mapTreeErrors', () => {
  it('pointer của prop lồng → node + propPath (fixture invalid-prop-wrong-type)', () => {
    const tree = parse(wrongTypeFixture.tree);
    const path = pointerOf(tree, 'c_hero1', ['overlayOpacity']);
    expect(path).toBe('/children/0/props/overlayOpacity');

    const mapped = mapTreeErrors(tree, {
      errors: [{ path: path ?? '', code: wrongTypeFixture.errorCodes[0] ?? '', message: 'Sai kiểu' }],
      truncated: false,
    });

    expect(mapped.byNode.get('c_hero1')?.propPaths).toEqual([['overlayOpacity']]);
    expect(mapped.banner).toEqual([]);
    expect(mapped.truncated).toBe(false);
  });

  it('lỗi ở chính node (không có /props) → tô node, không có propPath', () => {
    const tree = parse(unknownPropFixture.tree);
    const mapped = mapTreeErrors(tree, {
      errors: [{ path: '/children/0', code: 'NODE_VARIANT_UNKNOWN', message: 'x' }],
      truncated: false,
    });
    expect(mapped.byNode.get('c_hero1')).toMatchObject({ propPaths: [] });
    expect(mapped.byNode.get('c_hero1')?.errors).toHaveLength(1);
  });

  it('path gốc/vỏ → banner, không tô node nào', () => {
    const tree = parse(validHomeFixture.tree);
    const mapped = mapTreeErrors(tree, {
      errors: [
        { path: '', code: 'TREE_TOO_MANY_NODES', message: 'nhiều quá' },
        { path: '/schemaVersion', code: 'SCHEMA_VERSION_UNSUPPORTED', message: 'v' },
      ],
      truncated: true,
    });
    expect(mapped.byNode.size).toBe(0);
    expect(mapped.banner.map((e) => e.code)).toEqual(['TREE_TOO_MANY_NODES', 'SCHEMA_VERSION_UNSUPPORTED']);
    expect(mapped.truncated).toBe(true);
  });

  it('R5: path trỏ node không còn trong cây → không ném, không tô đỏ, lỗi giữ ở banner', () => {
    const tree = parse(wrongTypeFixture.tree);
    const mapped = mapTreeErrors(tree, {
      errors: [
        { path: '/children/7/props/title', code: 'PROP_INVALID', message: 'cũ' },
        { path: '/children/0/children/3', code: 'NODE_TYPE_UNKNOWN', message: 'cũ' },
        { path: 'khong-phai-pointer', code: 'X', message: 'hỏng' },
      ],
      truncated: false,
    });
    expect(mapped.byNode.size).toBe(0);
    expect(mapped.banner).toHaveLength(3);
    expect(mapped.total).toBe(3);
  });

  it('nhiều lỗi cùng node gộp lại; escape `~1` trong key prop được giải', () => {
    const tree = parse(wrongTypeFixture.tree);
    const mapped = mapTreeErrors(tree, {
      errors: [
        { path: '/children/0/props/a~1b', code: 'PROP_INVALID', message: '1' },
        { path: '/children/0/props/items/2/label', code: 'PROP_INVALID', message: '2' },
      ],
      truncated: false,
    });
    expect(mapped.byNode.get('c_hero1')?.propPaths).toEqual([['a/b'], ['items', 2, 'label']]);
  });
});

describe('classifySaveError', () => {
  it('409 DRAFT_CONFLICT kèm params → conflict', () => {
    const params = { currentVersion: 5, updatedAt: '2026-11-08T00:00:00Z', updatedByUserId: null };
    expect(classifySaveError({ status: 409, error_code: 'DRAFT_CONFLICT', params })).toEqual({ kind: 'conflict', params });
  });

  it('409 thiếu/sai params vẫn là conflict (dialog vẫn hiện)', () => {
    expect(classifySaveError({ status: 409, error_code: 'DRAFT_CONFLICT', params: { x: 1 } })).toEqual({
      kind: 'conflict',
      params: undefined,
    });
  });

  it('422 → invalid + params; mã lạ vẫn là invalid', () => {
    const params = { errors: [{ path: '', code: 'X', message: 'm' }], truncated: false };
    expect(classifySaveError({ status: 422, error_code: 'TREE_INVALID', params })).toEqual({
      kind: 'invalid',
      code: 'TREE_INVALID',
      params,
    });
    expect(classifySaveError({ status: 422, error_code: 'RICHTEXT_UNSAFE' })).toMatchObject({
      kind: 'invalid',
      params: undefined,
    });
  });

  it('413 → too-large (theo status hoặc mã)', () => {
    expect(classifySaveError({ status: 413, error_code: 'TREE_BODY_TOO_LARGE' })).toEqual({ kind: 'too-large' });
    expect(classifySaveError({ error_code: 'TREE_BODY_TOO_LARGE' })).toEqual({ kind: 'too-large' });
  });

  it('5xx, lỗi mạng, giá trị lạ → transient (giữ dirty, thử lại được)', () => {
    expect(classifySaveError({ status: 500, error_code: 'INTERNAL_ERROR' })).toEqual({ kind: 'transient' });
    expect(classifySaveError(new Error('Network Error'))).toEqual({ kind: 'transient' });
    expect(classifySaveError(undefined)).toEqual({ kind: 'transient' });
    expect(classifySaveError('<html>502</html>')).toEqual({ kind: 'transient' });
  });
});
