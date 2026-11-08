import { describe, expect, it } from 'vitest';
import { NODE_ID_PATTERN } from '@vsite/builder-components';
import { createNode, generateNodeId } from './new-node';

describe('generateNodeId', () => {
  it('khớp NODE_ID_PATTERN của envelope (c_ + ≥5 ký tự)', () => {
    const pattern = new RegExp(NODE_ID_PATTERN);
    for (let i = 0; i < 200; i++) expect(pattern.test(generateNodeId(new Set()))).toBe(true);
  });

  it('không trùng id đã có, 200 id liên tiếp đều khác nhau', () => {
    const seen = new Set<string>();
    for (let i = 0; i < 200; i++) {
      const id = generateNodeId(seen);
      expect(seen.has(id)).toBe(false);
      seen.add(id);
    }
  });

  it('ném lỗi thay vì lặp vô hạn khi không sinh nổi id mới', () => {
    const everything = { has: () => true } as unknown as ReadonlySet<string>;
    expect(() => generateNodeId(everything)).toThrow(/id/);
  });
});

describe('createNode', () => {
  it('node mới chỉ có id/type/variant — không props, không children (component tự có mặc định, #43 additive)', () => {
    const node = createNode('Hero', 'Hero01', new Set(['c_aaaaaa']));
    expect(node).toEqual({ id: expect.stringMatching(/^c_[A-Za-z0-9_-]{5,}$/) as string, type: 'Hero', variant: 'Hero01' });
    expect(node).not.toHaveProperty('props');
    expect(node).not.toHaveProperty('children');
  });
});
