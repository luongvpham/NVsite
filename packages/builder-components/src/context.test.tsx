import { renderHook } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { RenderContextProvider, useRenderContext, type MediaRef } from './context';

function withProvider(mediaMap: Readonly<Record<string, MediaRef>>) {
  return ({ children }: { children: React.ReactNode }) => (
    <RenderContextProvider value={{ mediaMap }}>{children}</RenderContextProvider>
  );
}

describe('resolveImage (F1 — thân thật, #74)', () => {
  it('imageId có trong mediaMap, preset khớp → trả /media/{storageKey}', () => {
    const mediaMap: Record<string, MediaRef> = {
      'img-1': { storageKey: 'shops/s1/media/abc.webp', preset: 'hero' },
    };
    const { result } = renderHook(() => useRenderContext(), { wrapper: withProvider(mediaMap) });

    expect(result.current.resolveImage('img-1', 'hero')).toBe('/media/shops/s1/media/abc.webp');
  });

  it('imageId không có trong mediaMap → placeholder theo preset yêu cầu', () => {
    const { result } = renderHook(() => useRenderContext(), { wrapper: withProvider({}) });

    expect(result.current.resolveImage('missing', 'hero')).toBe('/_dev/placeholder/hero.svg');
  });

  it('preset lệch → console.warn đúng 1 lần, vẫn trả URL thật', () => {
    const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const mediaMap: Record<string, MediaRef> = {
      'img-1': { storageKey: 'shops/s1/media/abc.webp', preset: 'hero' },
    };
    const { result } = renderHook(() => useRenderContext(), { wrapper: withProvider(mediaMap) });

    const url = result.current.resolveImage('img-1', 'gallery');

    expect(url).toBe('/media/shops/s1/media/abc.webp');
    expect(warnSpy).toHaveBeenCalledTimes(1);
    warnSpy.mockRestore();
  });

  it('id bản Library (preset: null) → vẫn render và warn', () => {
    const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const mediaMap: Record<string, MediaRef> = {
      'lib-1': { storageKey: 'shops/s1/media/lib.webp', preset: null },
    };
    const { result } = renderHook(() => useRenderContext(), { wrapper: withProvider(mediaMap) });

    const url = result.current.resolveImage('lib-1', 'hero');

    expect(url).toBe('/media/shops/s1/media/lib.webp');
    expect(warnSpy).toHaveBeenCalledTimes(1);
    warnSpy.mockRestore();
  });

  it('mediaMap mặc định là {} khi không truyền value', () => {
    const { result } = renderHook(() => useRenderContext(), {
      wrapper: ({ children }) => <RenderContextProvider>{children}</RenderContextProvider>,
    });

    expect(result.current.mediaMap).toEqual({});
  });
});
