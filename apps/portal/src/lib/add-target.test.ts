import { describe, expect, it } from 'vitest';
import type { TreeEnvelope } from '@vsite/builder-core';
import { resolveAddTarget } from './add-target';

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
      children: [{ id: 'c_rich1', type: 'RichText', variant: 'RichText01' }],
    },
    { id: 'c_gall1', type: 'Gallery', variant: 'Gallery01' },
  ],
};

describe('resolveAddTarget — thêm khối mới vào đâu', () => {
  it('chưa chọn gì → cuối trang', () => {
    expect(resolveAddTarget(tree, null)).toEqual({ parentId: 'root', index: 3 });
  });

  it('chọn khối chứa được con (Section) → cuối danh sách con của nó', () => {
    expect(resolveAddTarget(tree, 'c_sect1')).toEqual({ parentId: 'c_sect1', index: 1 });
  });

  it('chọn khối không chứa con (Hero) → ngay sau nó, cùng cha', () => {
    expect(resolveAddTarget(tree, 'c_hero1')).toEqual({ parentId: 'root', index: 1 });
  });

  it('chọn khối lá nằm trong Section → ngay sau nó trong Section', () => {
    expect(resolveAddTarget(tree, 'c_rich1')).toEqual({ parentId: 'c_sect1', index: 1 });
  });

  it('id chọn không còn trong cây → cuối trang', () => {
    expect(resolveAddTarget(tree, 'c_gone')).toEqual({ parentId: 'root', index: 3 });
  });
});
