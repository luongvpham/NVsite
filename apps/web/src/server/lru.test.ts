import { describe, expect, it } from 'vitest';
import { LruCache } from './lru';

describe('LruCache', () => {
  it('stores and returns values; missing key → undefined', () => {
    const c = new LruCache<string, number>(2);
    c.set('a', 1);
    expect(c.get('a')).toBe(1);
    expect(c.get('b')).toBeUndefined();
    expect(c.size).toBe(1);
  });

  it('evicts the least recently used entry when full', () => {
    const c = new LruCache<string, number>(2);
    c.set('a', 1);
    c.set('b', 2);
    c.get('a'); // a mới dùng → b là cũ nhất
    c.set('c', 3);
    expect(c.get('b')).toBeUndefined();
    expect(c.get('a')).toBe(1);
    expect(c.get('c')).toBe(3);
    expect(c.size).toBe(2);
  });

  it('overwriting a key refreshes it and does not grow size', () => {
    const c = new LruCache<string, number>(2);
    c.set('a', 1);
    c.set('b', 2);
    c.set('a', 10);
    c.set('c', 3); // b cũ nhất
    expect(c.get('a')).toBe(10);
    expect(c.get('b')).toBeUndefined();
    expect(c.size).toBe(2);
  });

  it('delete removes the entry', () => {
    const c = new LruCache<string, number>(2);
    c.set('a', 1);
    expect(c.delete('a')).toBe(true);
    expect(c.get('a')).toBeUndefined();
    expect(c.delete('a')).toBe(false);
  });

  it('rejects a non-positive or non-integer capacity', () => {
    expect(() => new LruCache(0)).toThrow();
    expect(() => new LruCache(1.5)).toThrow();
  });
});
