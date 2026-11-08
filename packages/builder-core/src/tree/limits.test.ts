import { describe, expect, it } from 'vitest';
import { envelope, hero, richText, section } from '../test-support/trees';
import {
  MAX_DEPTH,
  MAX_NODES,
  MAX_TREE_BYTES,
  THEME_MAX_BYTES,
  checkThemeLimit,
  checkTreeLimits,
  measureTree,
} from './limits';
import type { TreeNode } from './types';

/** Giới hạn khớp BE (`PageTreeLimits`): 1 MB (byte UTF-8), sâu 20 (con gốc = 1), 2000 node; theme 64 KB. */
function chain(depth: number): TreeNode {
  let node: TreeNode = richText('c_leaf0');
  for (let i = depth - 1; i >= 1; i--) node = section(`c_s${String(i).padStart(4, '0')}`, [node]);
  return node;
}

describe('hằng số giới hạn', () => {
  it('đúng giá trị của BE', () => {
    expect(MAX_TREE_BYTES).toBe(1_048_576);
    expect(MAX_DEPTH).toBe(20);
    expect(MAX_NODES).toBe(2000);
    expect(THEME_MAX_BYTES).toBe(65_536);
  });
});

describe('measureTree', () => {
  it('đo byte UTF-8 của JSON gọn (ký tự tiếng Việt > 1 byte), độ sâu và số node', () => {
    const tree = envelope([richText('c_rt001', '<p>Việt</p>')]);
    const json = JSON.stringify(tree);
    const m = measureTree(tree);
    expect(m.bytes).toBe(new TextEncoder().encode(json).length);
    expect(m.bytes).toBeGreaterThan(json.length);
    expect(m.depth).toBe(1);
    expect(m.nodes).toBe(1);
  });
});

describe('checkTreeLimits', () => {
  it('cây nhỏ → ok', () => {
    expect(checkTreeLimits(envelope([hero()]))).toEqual({ ok: true });
  });

  it('sâu đúng 20 → ok, 21 → TREE_TOO_DEEP', () => {
    expect(checkTreeLimits(envelope([chain(20)])).ok).toBe(true);
    expect(checkTreeLimits(envelope([chain(21)]))).toMatchObject({ ok: false, code: 'TREE_TOO_DEEP' });
  });

  it('đúng 2000 node → ok, 2001 → TREE_TOO_MANY_NODES', () => {
    const make = (n: number) =>
      envelope(Array.from({ length: n }, (_, i) => hero(`c_n${String(i).padStart(5, '0')}`)));
    expect(checkTreeLimits(make(2000)).ok).toBe(true);
    expect(checkTreeLimits(make(2001))).toMatchObject({ ok: false, code: 'TREE_TOO_MANY_NODES' });
  });

  it('vượt 1 MB byte (đo bằng byte, không phải ký tự) → TREE_BODY_TOO_LARGE', () => {
    // "ệ" = 3 byte UTF-8: 400k ký tự ≈ 1.2 MB trong khi chuỗi JSON chỉ ~400k "ký tự".
    const big = envelope([richText('c_big01', 'ệ'.repeat(400_000))]);
    expect(JSON.stringify(big).length).toBeLessThan(MAX_TREE_BYTES);
    expect(checkTreeLimits(big)).toMatchObject({ ok: false, code: 'TREE_BODY_TOO_LARGE' });
  });
});

describe('checkThemeLimit', () => {
  it('64 KB', () => {
    expect(checkThemeLimit({ schemaVersion: 1 }).ok).toBe(true);
    expect(checkThemeLimit({ x: 'a'.repeat(THEME_MAX_BYTES) })).toMatchObject({
      ok: false,
      code: 'THEME_BODY_TOO_LARGE',
    });
  });
});
