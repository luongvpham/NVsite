import { describe, expect, it } from 'vitest';
import type { TreeEnvelope } from '@vsite/builder-core';
import { siblingMove } from './sibling-move';

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

describe('siblingMove — đổi chỗ với anh em liền kề', () => {
  it('khối giữa: lên → index-1, xuống → index+1, cùng cha', () => {
    expect(siblingMove(tree, 'c_sect1', 'up')).toEqual({ toParentId: 'root', toIndex: 0 });
    expect(siblingMove(tree, 'c_sect1', 'down')).toEqual({ toParentId: 'root', toIndex: 2 });
  });

  it('đầu danh sách không lên được, cuối không xuống được', () => {
    expect(siblingMove(tree, 'c_hero1', 'up')).toBeNull();
    expect(siblingMove(tree, 'c_gall1', 'down')).toBeNull();
  });

  it('con trong Section di chuyển trong phạm vi Section', () => {
    expect(siblingMove(tree, 'c_rich1', 'down')).toEqual({ toParentId: 'c_sect1', toIndex: 1 });
    expect(siblingMove(tree, 'c_rich2', 'up')).toEqual({ toParentId: 'c_sect1', toIndex: 0 });
    expect(siblingMove(tree, 'c_rich1', 'up')).toBeNull();
    expect(siblingMove(tree, 'c_rich2', 'down')).toBeNull();
  });

  it('id không có trong cây → null', () => {
    expect(siblingMove(tree, 'c_gone', 'up')).toBeNull();
  });
});
