import { describe, expect, it } from 'vitest';
import { sameJson } from './same-json';

describe('sameJson — so cấu trúc kiểu JSON', () => {
  it('không phụ thuộc thứ tự key, đệ quy', () => {
    expect(sameJson({ a: 1, b: { c: [1, { d: 'x', e: null }] } }, { b: { c: [1, { e: null, d: 'x' }] }, a: 1 })).toBe(true);
  });

  it('mảng phân biệt thứ tự và độ dài', () => {
    expect(sameJson([1, 2], [2, 1])).toBe(false);
    expect(sameJson([1], [1, 1])).toBe(false);
  });

  it('khác giá trị / kiểu / thiếu key → khác', () => {
    expect(sameJson({ a: 1 }, { a: '1' })).toBe(false);
    expect(sameJson({ a: 1 }, { a: 1, b: 2 })).toBe(false);
    expect(sameJson({ a: [] }, { a: {} })).toBe(false);
    expect(sameJson(null, {})).toBe(false);
  });

  it('key giá trị undefined coi như không có (JSON bỏ nó khi gửi)', () => {
    expect(sameJson({ a: 1, b: undefined }, { a: 1 })).toBe(true);
  });
});
