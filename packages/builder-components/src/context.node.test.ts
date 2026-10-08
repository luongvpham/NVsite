// @vitest-environment node
import { describe, expect, it, vi } from 'vitest';
import { createResolveImage } from './context';

describe('resolveImage — isomorphic, không đụng window/document (#23)', () => {
  it('môi trường không có window/document (SSR)', () => {
    expect(typeof globalThis.window).toBe('undefined');
    expect(typeof globalThis.document).toBe('undefined');
  });

  it('resolveImage chạy được trong môi trường node, không throw', () => {
    const resolveImage = createResolveImage({
      'img-1': { storageKey: 'shops/s1/media/abc.webp', preset: 'hero' },
    });

    expect(() => resolveImage('img-1', 'hero')).not.toThrow();
    expect(resolveImage('img-1', 'hero')).toBe('/media/shops/s1/media/abc.webp');
    expect(resolveImage('missing', 'hero')).toBe('/_dev/placeholder/hero.svg');
  });

  it('preset lệch trong môi trường node vẫn warn và trả URL thật', () => {
    const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const resolveImage = createResolveImage({
      'lib-1': { storageKey: 'shops/s1/media/lib.webp', preset: null },
    });

    const url = resolveImage('lib-1', 'hero');

    expect(url).toBe('/media/shops/s1/media/lib.webp');
    expect(warnSpy).toHaveBeenCalledTimes(1);
    warnSpy.mockRestore();
  });
});
