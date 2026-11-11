import { describe, expect, it } from 'vitest';
import { PageKind } from '../tree/types';
import type { TreeEnvelope } from '../tree/types';
import { envelope, hero, richText, section } from '../test-support/trees';
import { applyOperation, executeOperation } from './apply';

/** Đóng băng sâu để chứng minh apply không mutate cây đầu vào. */
function deepFreeze<T>(value: T): T {
  if (typeof value === 'object' && value !== null) {
    Object.values(value).forEach(deepFreeze);
    Object.freeze(value);
  }
  return value;
}

const ids = (tree: TreeEnvelope, parent?: string) =>
  (parent ? (tree.children.find((n) => n.id === parent)?.children ?? []) : tree.children).map((n) => n.id);

describe('applyOperation — add', () => {
  it('chèn vào gốc đúng vị trí, không mutate cây đầu vào', () => {
    const before = deepFreeze(envelope([hero('c_hero1'), richText('c_rt001')]));
    const after = applyOperation(before, {
      type: 'add',
      parentId: 'root',
      index: 1,
      node: { id: 'c_new01', type: 'Section', variant: 'Section01' },
    });
    expect(ids(after)).toEqual(['c_hero1', 'c_new01', 'c_rt001']);
    expect(ids(before)).toEqual(['c_hero1', 'c_rt001']);
  });

  it('chèn vào Section chưa có children (tạo mảng) và giữ props của node mới', () => {
    const before = deepFreeze(envelope([{ id: 'c_sec01', type: 'Section', variant: 'Section01' }]));
    const after = applyOperation(before, {
      type: 'add',
      parentId: 'c_sec01',
      index: 0,
      node: { id: 'c_rt001', type: 'RichText', variant: 'RichText01', props: { content: '<p>a</p>' } },
    });
    expect(after.children[0]?.children).toEqual([
      { id: 'c_rt001', type: 'RichText', variant: 'RichText01', props: { content: '<p>a</p>' } },
    ]);
  });

  it('structural sharing: nhánh không đụng tới giữ nguyên tham chiếu (zundo/React khỏi render lại)', () => {
    const before = deepFreeze(envelope([section('c_sec01', [richText('c_rt001')]), hero('c_hero1')]));
    const after = applyOperation(before, {
      type: 'add',
      parentId: 'root',
      index: 2,
      node: { id: 'c_new01', type: 'RichText', variant: 'RichText01' },
    });
    expect(after.children[0]).toBe(before.children[0]);
    expect(after.children[1]).toBe(before.children[1]);
  });
});

describe('applyOperation — remove', () => {
  it('gỡ cả nhánh con cháu', () => {
    const before = deepFreeze(envelope([section('c_sec01', [richText('c_rt001')]), hero('c_hero1')]));
    const after = applyOperation(before, { type: 'remove', nodeId: 'c_sec01' });
    expect(ids(after)).toEqual(['c_hero1']);
  });

  it('gỡ node lồng trong Section', () => {
    const before = deepFreeze(envelope([section('c_sec01', [richText('c_rt001'), richText('c_rt002')])]));
    const after = applyOperation(before, { type: 'remove', nodeId: 'c_rt001' });
    expect(ids(after, 'c_sec01')).toEqual(['c_rt002']);
  });
});

describe('applyOperation — move (toIndex tính SAU khi gỡ)', () => {
  const base = () => deepFreeze(envelope([richText('c_aaaaa'), richText('c_bbbbb'), richText('c_ccccc'), section('c_sec01')]));

  it('cùng cha: a → cuối (index 3 sau khi gỡ = sau Section)', () => {
    expect(ids(applyOperation(base(), { type: 'move', nodeId: 'c_aaaaa', toParentId: 'root', toIndex: 3 }))).toEqual([
      'c_bbbbb',
      'c_ccccc',
      'c_sec01',
      'c_aaaaa',
    ]);
  });

  it('cùng cha: c → đầu', () => {
    expect(ids(applyOperation(base(), { type: 'move', nodeId: 'c_ccccc', toParentId: 'root', toIndex: 0 }))).toEqual([
      'c_ccccc',
      'c_aaaaa',
      'c_bbbbb',
      'c_sec01',
    ]);
  });

  it('sang Section; node giữ nguyên props/id', () => {
    const after = applyOperation(base(), { type: 'move', nodeId: 'c_bbbbb', toParentId: 'c_sec01', toIndex: 0 });
    expect(ids(after)).toEqual(['c_aaaaa', 'c_ccccc', 'c_sec01']);
    expect(after.children[2]?.children?.[0]).toEqual(richText('c_bbbbb'));
  });

  it('từ Section ra gốc', () => {
    const before = deepFreeze(envelope([section('c_sec01', [richText('c_rt001')]), hero('c_hero1')]));
    const after = applyOperation(before, { type: 'move', nodeId: 'c_rt001', toParentId: 'root', toIndex: 0 });
    expect(ids(after)).toEqual(['c_rt001', 'c_sec01', 'c_hero1']);
    expect(ids(after, 'c_sec01')).toEqual([]);
  });
});

describe('applyOperation — update (path trong props)', () => {
  it('đặt prop cấp 1, tạo props khi node chưa có', () => {
    const before = deepFreeze(envelope([hero('c_hero1')]));
    const after = applyOperation(before, { type: 'update', nodeId: 'c_hero1', path: ['title'], value: 'Chào' });
    expect(after.children[0]?.props).toEqual({ title: 'Chào' });
    expect(before.children[0]).not.toHaveProperty('props');
  });

  it('đặt prop lồng nhau, tạo object trung gian (cta.label chưa có cta)', () => {
    const before = deepFreeze(envelope([hero('c_hero1', { title: 'x' })]));
    const after = applyOperation(before, { type: 'update', nodeId: 'c_hero1', path: ['cta', 'label'], value: 'Đặt lịch' });
    expect(after.children[0]?.props).toEqual({ title: 'x', cta: { label: 'Đặt lịch' } });
  });

  it('đặt phần tử mảng (list item) và tạo mảng trung gian khi segment kế là số', () => {
    const before = deepFreeze(
      envelope([{ id: 'c_gal01', type: 'Gallery', variant: 'Gallery01', props: { items: [{ caption: 'a' }, { caption: 'b' }] } }]),
    );
    const edit = applyOperation(before, { type: 'update', nodeId: 'c_gal01', path: ['items', 1, 'caption'], value: 'B' });
    expect(edit.children[0]?.props).toEqual({ items: [{ caption: 'a' }, { caption: 'B' }] });

    const fresh = applyOperation(deepFreeze(envelope([{ id: 'c_gal01', type: 'Gallery', variant: 'Gallery01' }])), {
      type: 'update',
      nodeId: 'c_gal01',
      path: ['items', 0, 'caption'],
      value: 'x',
    });
    expect(fresh.children[0]?.props).toEqual({ items: [{ caption: 'x' }] });
  });

  it('value undefined xoá khoá; ở mảng thì gỡ phần tử (splice)', () => {
    const before = deepFreeze(
      envelope([{ id: 'c_gal01', type: 'Gallery', variant: 'Gallery01', props: { heading: 'h', items: [{ caption: 'a' }, { caption: 'b' }] } }]),
    );
    const noHeading = applyOperation(before, { type: 'update', nodeId: 'c_gal01', path: ['heading'], value: undefined });
    expect(noHeading.children[0]?.props).toEqual({ items: [{ caption: 'a' }, { caption: 'b' }] });
    const noFirst = applyOperation(before, { type: 'update', nodeId: 'c_gal01', path: ['items', 0], value: undefined });
    expect(noFirst.children[0]?.props).toEqual({ heading: 'h', items: [{ caption: 'b' }] });
  });

  it('chỉ node được sửa đổi tham chiếu; node khác giữ nguyên', () => {
    const before = deepFreeze(envelope([hero('c_hero1'), richText('c_rt001')]));
    const after = applyOperation(before, { type: 'update', nodeId: 'c_hero1', path: ['title'], value: 'x' });
    expect(after.children[1]).toBe(before.children[1]);
    expect(after.children[0]).not.toBe(before.children[0]);
  });
});

describe('applyOperation — op không đổi gì trả CHÍNH cây cũ (không tạo bước undo rỗng)', () => {
  it('update cùng giá trị, xoá prop không có, move về đúng chỗ cũ', () => {
    const before = deepFreeze(envelope([hero('c_hero1', { title: 'x' }), richText('c_rt001')]));
    expect(applyOperation(before, { type: 'update', nodeId: 'c_hero1', path: ['title'], value: 'x' })).toBe(before);
    expect(applyOperation(before, { type: 'update', nodeId: 'c_hero1', path: ['subtitle'], value: undefined })).toBe(before);
    expect(applyOperation(before, { type: 'move', nodeId: 'c_rt001', toParentId: 'root', toIndex: 1 })).toBe(before);
  });
});

describe('applyOperation — changeTheme', () => {
  it('không đụng cây (theme nằm ngoài tree)', () => {
    const before = deepFreeze(envelope([hero()]));
    expect(applyOperation(before, { type: 'changeTheme', tokens: {} })).toBe(before);
  });
});

describe('executeOperation = parse → validate → apply', () => {
  const ctx = (tree: TreeEnvelope, pageKind: PageKind = PageKind.Composable) => ({ tree, pageKind });

  it('op hợp lệ → ok + cây mới + op đã chuẩn hoá', () => {
    const tree = deepFreeze(envelope([hero('c_hero1')]));
    const result = executeOperation({ type: 'remove', nodeId: 'c_hero1' }, ctx(tree));
    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.tree.children).toEqual([]);
      expect(result.op.type).toBe('remove');
    }
  });

  it('op sai hình dạng / vi phạm luật → ok:false với mã, KHÔNG ném lỗi, cây không đổi', () => {
    const tree = deepFreeze(envelope([hero('c_hero1')]));
    expect(executeOperation({ nonsense: true }, ctx(tree))).toMatchObject({ ok: false, code: 'INVALID_OPERATION' });
    expect(executeOperation({ type: 'remove', nodeId: 'c_nope0' }, ctx(tree))).toMatchObject({ ok: false, code: 'TARGET_NOT_FOUND' });
    expect(
      executeOperation({ type: 'add', parentId: 'root', index: 0, node: { id: 'c_new01', type: 'Hero', variant: 'Hero01' } }, ctx(tree)),
    ).toMatchObject({ ok: false, code: 'NODE_MAX_PER_PAGE_EXCEEDED' });
  });

  it('thông báo lỗi là tiếng Việt, có nội dung', () => {
    const result = executeOperation({ type: 'remove', nodeId: 'c_nope0' }, ctx(envelope()));
    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.message.length).toBeGreaterThan(5);
  });
});
