/**
 * LRU thuần, không TTL (A9): `Map` giữ thứ tự chèn — mục đầu là mục ít dùng gần nhất.
 * `get` đẩy mục lên mới nhất; `set` khi đầy bỏ mục cũ nhất.
 */
export class LruCache<K, V> {
  readonly #max: number;
  readonly #map = new Map<K, V>();

  constructor(maxEntries: number) {
    if (!Number.isInteger(maxEntries) || maxEntries < 1) {
      throw new RangeError(`LruCache: maxEntries phải là số nguyên ≥ 1 (nhận ${maxEntries}).`);
    }
    this.#max = maxEntries;
  }

  get size(): number {
    return this.#map.size;
  }

  get(key: K): V | undefined {
    if (!this.#map.has(key)) return undefined;
    const value = this.#map.get(key) as V;
    this.#map.delete(key);
    this.#map.set(key, value);
    return value;
  }

  set(key: K, value: V): void {
    this.#map.delete(key);
    this.#map.set(key, value);
    if (this.#map.size > this.#max) {
      const oldest = this.#map.keys().next();
      if (!oldest.done) this.#map.delete(oldest.value);
    }
  }

  delete(key: K): boolean {
    return this.#map.delete(key);
  }
}
