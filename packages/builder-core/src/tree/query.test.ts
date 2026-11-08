import { describe, expect, it } from 'vitest';
import { envelope, sampleTree, section } from '../test-support/trees';
import {
  collectIds,
  countNodes,
  findNode,
  isSelfOrDescendant,
  maxDepth,
  pointerOf,
  resolvePointer,
} from './query';

describe('findNode', () => {
  it('trả node + cha + vị trí + độ sâu (con trực tiếp của gốc = 1)', () => {
    const tree = sampleTree();
    expect(findNode(tree, 'c_rt001')).toMatchObject({ parentId: 'c_sec01', index: 0, depth: 2 });
    expect(findNode(tree, 'c_hero1')).toMatchObject({ parentId: 'root', index: 1, depth: 1 });
    expect(findNode(tree, 'c_sec01')?.node.children).toHaveLength(1);
  });

  it('không có node → undefined; gốc "root" không phải node', () => {
    const tree = sampleTree();
    expect(findNode(tree, 'c_nope0')).toBeUndefined();
    expect(findNode(tree, 'root')).toBeUndefined();
  });
});

describe('isSelfOrDescendant', () => {
  it('chính nó và hậu duệ là true, tổ tiên/anh em là false', () => {
    const tree = sampleTree();
    expect(isSelfOrDescendant(tree, 'c_sec01', 'c_sec01')).toBe(true);
    expect(isSelfOrDescendant(tree, 'c_sec01', 'c_rt001')).toBe(true);
    expect(isSelfOrDescendant(tree, 'c_rt001', 'c_sec01')).toBe(false);
    expect(isSelfOrDescendant(tree, 'c_hero1', 'c_rt001')).toBe(false);
  });
});

describe('đo cây', () => {
  it('countNodes không tính gốc; maxDepth gốc rỗng = 0', () => {
    expect(countNodes(sampleTree())).toBe(3);
    expect(countNodes(envelope())).toBe(0);
    expect(maxDepth(sampleTree())).toBe(2);
    expect(maxDepth(envelope())).toBe(0);
  });

  it('maxDepth theo chuỗi Section lồng nhau', () => {
    const deep = envelope([section('c_a0001', [section('c_b0001', [section('c_c0001')])])]);
    expect(maxDepth(deep)).toBe(3);
  });

  it('collectIds gom id mọi node (không có "root")', () => {
    expect([...collectIds(sampleTree())].sort()).toEqual(['c_hero1', 'c_rt001', 'c_sec01']);
  });
});

describe('JSON Pointer ↔ node/prop (tô đỏ lỗi 422 theo path)', () => {
  it('pointerOf: node, prop, prop lồng + chỉ số mảng, và thoát ~ /', () => {
    const tree = sampleTree();
    expect(pointerOf(tree, 'c_rt001')).toBe('/children/0/children/0');
    expect(pointerOf(tree, 'c_rt001', ['content'])).toBe('/children/0/children/0/props/content');
    expect(pointerOf(tree, 'c_hero1', ['cta', 'target', 'url'])).toBe('/children/1/props/cta/target/url');
    expect(pointerOf(tree, 'c_hero1', ['items', 2, 'image'])).toBe('/children/1/props/items/2/image');
    expect(pointerOf(tree, 'c_hero1', ['a/b', 'c~d'])).toBe('/children/1/props/a~1b/c~0d');
  });

  it('pointerOf: node không tồn tại → undefined', () => {
    expect(pointerOf(sampleTree(), 'c_nope0')).toBeUndefined();
  });

  it('resolvePointer: ngược lại pointerOf', () => {
    const tree = sampleTree();
    expect(resolvePointer(tree, '/children/0/children/0/props/content')).toEqual({
      nodeId: 'c_rt001',
      propPath: ['content'],
    });
    expect(resolvePointer(tree, '/children/1')).toEqual({ nodeId: 'c_hero1', propPath: [] });
    expect(resolvePointer(tree, '/children/1/props/cta/target/url')).toEqual({
      nodeId: 'c_hero1',
      propPath: ['cta', 'target', 'url'],
    });
    expect(resolvePointer(tree, '/children/1/props/items/2/image')).toEqual({
      nodeId: 'c_hero1',
      propPath: ['items', 2, 'image'],
    });
    expect(resolvePointer(tree, '/children/1/props/a~1b/c~0d')).toEqual({
      nodeId: 'c_hero1',
      propPath: ['a/b', 'c~d'],
    });
  });

  it('resolvePointer: lỗi ở trường của chính node (id/type/variant) → nodeId, không có propPath', () => {
    expect(resolvePointer(sampleTree(), '/children/0/variant')).toEqual({ nodeId: 'c_sec01', propPath: [] });
  });

  it('resolvePointer: gốc/vỏ → nodeId null (banner chung)', () => {
    const tree = sampleTree();
    expect(resolvePointer(tree, '')).toEqual({ nodeId: null, propPath: [] });
    expect(resolvePointer(tree, '/schemaVersion')).toEqual({ nodeId: null, propPath: ['schemaVersion'] });
  });

  it('resolvePointer: node không còn trong cây (path cũ, R5) hoặc pointer hỏng → null', () => {
    const tree = sampleTree();
    expect(resolvePointer(tree, '/children/9')).toBeNull();
    expect(resolvePointer(tree, '/children/0/children/5/props/content')).toBeNull();
    expect(resolvePointer(tree, '/children/x')).toBeNull();
    expect(resolvePointer(tree, 'children/0')).toBeNull();
  });
});
