import { describe, expect, it } from 'vitest';
import { setIn } from './paths';

const freeze = <T>(value: T): T => {
  if (typeof value === 'object' && value !== null) {
    Object.values(value).forEach(freeze);
    Object.freeze(value);
  }
  return value;
};

describe('setIn (immutable)', () => {
  it('đặt khoá cấp 1 và lồng nhau, không mutate đầu vào, giữ tham chiếu nhánh không đụng tới', () => {
    const root = freeze({ a: { x: 1 }, b: { y: 2 } });
    const next = setIn(root, ['a', 'x'], 9) as typeof root;
    expect(next).toEqual({ a: { x: 9 }, b: { y: 2 } });
    expect(root.a.x).toBe(1);
    expect(next.b).toBe(root.b);
  });

  it('tạo object/mảng trung gian theo kiểu segment kế', () => {
    expect(setIn({}, ['cta', 'label'], 'x')).toEqual({ cta: { label: 'x' } });
    expect(setIn({}, ['items', 0, 'caption'], 'x')).toEqual({ items: [{ caption: 'x' }] });
    expect(setIn(undefined, ['a'], 1)).toEqual({ a: 1 });
  });

  it('value undefined xoá khoá; ở mảng gỡ phần tử', () => {
    expect(setIn({ a: 1, b: 2 }, ['a'], undefined)).toEqual({ b: 2 });
    expect(setIn({ items: [1, 2, 3] }, ['items', 1], undefined)).toEqual({ items: [1, 3] });
  });

  it('xoá thứ không tồn tại → trả đúng root (không tạo container thừa)', () => {
    const root = freeze({ a: 1 });
    expect(setIn(root, ['zzz'], undefined)).toBe(root);
    expect(setIn(root, ['cta', 'label'], undefined)).toBe(root);
    expect(setIn(freeze({ items: [1] }), ['items', 5], undefined)).toEqual({ items: [1] });
  });

  it('path rỗng thay cả giá trị', () => {
    expect(setIn({ a: 1 }, [], 'x')).toBe('x');
  });

  it('từ chối segment gây ô nhiễm prototype', () => {
    for (const segment of ['__proto__', 'constructor', 'prototype']) {
      expect(() => setIn({}, [segment, 'x'], 1)).toThrow(/không được phép/);
    }
    expect(({} as Record<string, unknown>).polluted).toBeUndefined();
  });
});
