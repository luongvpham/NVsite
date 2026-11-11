import { describe, expect, it } from 'vitest';
import { PageKind, type TreeEnvelope } from '@vsite/builder-core';
import { evaluateDrop, PROBE_NODE_ID } from './drop-operation';

const tree: TreeEnvelope = {
  schemaVersion: 1,
  id: 'root',
  type: 'Page',
  children: [
    { id: 'c_hero1', type: 'Hero', variant: 'Hero01' },
    {
      id: 'c_sect1',
      type: 'Section',
      variant: 'Section01',
      children: [
        { id: 'c_rich1', type: 'RichText', variant: 'RichText01' },
        { id: 'c_rich2', type: 'RichText', variant: 'RichText01' },
      ],
    },
    { id: 'c_gall1', type: 'Gallery', variant: 'Gallery01' },
  ],
};
const ctx = { tree, pageKind: PageKind.Composable };

describe('evaluateDrop — kéo từ palette (add)', () => {
  it('thả vào slot → op add đúng cha/vị trí, node mang id được cấp', () => {
    const r = evaluateDrop(
      { kind: 'palette', type: 'RichText', variant: 'RichText01' },
      { parentId: 'c_sect1', index: 1 },
      ctx,
      'c_newnode1',
    );
    expect(r).toEqual({
      status: 'ok',
      op: {
        type: 'add',
        parentId: 'c_sect1',
        index: 1,
        node: { id: 'c_newnode1', type: 'RichText', variant: 'RichText01' },
      },
    });
  });

  it('không cấp id → dùng id giả (chỉ để hỏi Engine)', () => {
    const r = evaluateDrop({ kind: 'palette', type: 'Gallery', variant: 'Gallery01' }, { parentId: 'root', index: 0 }, ctx);
    expect(r.status === 'ok' && r.op.type === 'add' && r.op.node.id).toBe(PROBE_NODE_ID);
  });

  it('Hero thứ hai → invalid kèm lý do maxPerPage', () => {
    const r = evaluateDrop({ kind: 'palette', type: 'Hero', variant: 'Hero02' }, { parentId: 'root', index: 0 }, ctx);
    expect(r.status).toBe('invalid');
    expect(r.status === 'invalid' && r.message).toMatch(/tối đa một lần/);
  });

  it('thả Section vào khối không chứa con (Hero) → invalid', () => {
    const r = evaluateDrop({ kind: 'palette', type: 'RichText', variant: 'RichText01' }, { parentId: 'c_hero1', index: 0 }, ctx);
    expect(r.status).toBe('invalid');
  });

  it('thả vào cha không tồn tại → invalid', () => {
    const r = evaluateDrop({ kind: 'palette', type: 'Gallery', variant: 'Gallery01' }, { parentId: 'c_gone', index: 0 }, ctx);
    expect(r.status).toBe('invalid');
  });

  it('trang System → invalid (cấu trúc cố định)', () => {
    const r = evaluateDrop(
      { kind: 'palette', type: 'RichText', variant: 'RichText01' },
      { parentId: 'root', index: 0 },
      { tree, pageKind: PageKind.System },
    );
    expect(r.status).toBe('invalid');
  });
});

describe('evaluateDrop — kéo node (move)', () => {
  const node = (nodeId: string) => ({ kind: 'node', nodeId }) as const;

  it('cùng cha, thả phía sau: toIndex tính SAU khi gỡ (slot-1)', () => {
    // root: [hero(0), sect(1), gall(2)] — kéo hero tới slot 3 (cuối) → toIndex 2
    expect(evaluateDrop(node('c_hero1'), { parentId: 'root', index: 3 }, ctx)).toEqual({
      status: 'ok',
      op: { type: 'move', nodeId: 'c_hero1', toParentId: 'root', toIndex: 2 },
    });
    // slot 2 (giữa sect và gall) → toIndex 1
    expect(evaluateDrop(node('c_hero1'), { parentId: 'root', index: 2 }, ctx)).toEqual({
      status: 'ok',
      op: { type: 'move', nodeId: 'c_hero1', toParentId: 'root', toIndex: 1 },
    });
  });

  it('cùng cha, thả phía trước: toIndex = slot', () => {
    expect(evaluateDrop(node('c_gall1'), { parentId: 'root', index: 0 }, ctx)).toEqual({
      status: 'ok',
      op: { type: 'move', nodeId: 'c_gall1', toParentId: 'root', toIndex: 0 },
    });
  });

  it('thả ngay trước hoặc ngay sau chính nó → noop', () => {
    expect(evaluateDrop(node('c_sect1'), { parentId: 'root', index: 1 }, ctx).status).toBe('noop');
    expect(evaluateDrop(node('c_sect1'), { parentId: 'root', index: 2 }, ctx).status).toBe('noop');
  });

  it('vào Section khác cha: toIndex = slot (không trừ)', () => {
    expect(evaluateDrop(node('c_gall1'), { parentId: 'c_sect1', index: 1 }, ctx)).toEqual({
      status: 'ok',
      op: { type: 'move', nodeId: 'c_gall1', toParentId: 'c_sect1', toIndex: 1 },
    });
  });

  it('ra ngoài Section lên gốc', () => {
    expect(evaluateDrop(node('c_rich1'), { parentId: 'root', index: 0 }, ctx)).toEqual({
      status: 'ok',
      op: { type: 'move', nodeId: 'c_rich1', toParentId: 'root', toIndex: 0 },
    });
  });

  it('thả Section vào chính nó hoặc con của nó → invalid', () => {
    const intoSelf = evaluateDrop(node('c_sect1'), { parentId: 'c_sect1', index: 0 }, ctx);
    expect(intoSelf.status).toBe('invalid');
    expect(intoSelf.status === 'invalid' && intoSelf.message).toMatch(/chính nó/);
  });

  it('node không tồn tại → invalid', () => {
    expect(evaluateDrop(node('c_gone'), { parentId: 'root', index: 0 }, ctx).status).toBe('invalid');
  });

  it('trang System → invalid', () => {
    expect(
      evaluateDrop(node('c_rich1'), { parentId: 'root', index: 0 }, { tree, pageKind: PageKind.System }).status,
    ).toBe('invalid');
  });
});

describe('isDragSource / isDropSlot', () => {
  it('nhận đúng hình dạng, từ chối phần còn lại', async () => {
    const { isDragSource, isDropSlot } = await import('./drop-operation');
    expect(isDragSource({ kind: 'palette', type: 'Hero', variant: 'Hero01' })).toBe(true);
    expect(isDragSource({ kind: 'node', nodeId: 'c_x' })).toBe(true);
    expect(isDragSource({ kind: 'palette', type: 'Hero' })).toBe(false);
    expect(isDragSource({ kind: 'node' })).toBe(false);
    expect(isDragSource(null)).toBe(false);
    expect(isDragSource('node')).toBe(false);
    expect(isDropSlot({ parentId: 'root', index: 0 })).toBe(true);
    expect(isDropSlot({ parentId: 'root', index: 1.5 })).toBe(false);
    expect(isDropSlot({ parentId: 1, index: 0 })).toBe(false);
    expect(isDropSlot(undefined)).toBe(false);
  });
});
