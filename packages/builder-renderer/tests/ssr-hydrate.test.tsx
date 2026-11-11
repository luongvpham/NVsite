import { act } from 'react';
import { renderToString } from 'react-dom/server';
import { hydrateRoot } from 'react-dom/client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { loadValidTrees, SiteApp } from './helpers/site-app';

/** R3 — HTML server == lần render client đầu: hydrate không báo lệch (năm ©, id, thời gian…). */
describe('hydrate SSR→CSR không lệch', () => {
  afterEach(() => vi.restoreAllMocks());

  it.each(loadValidTrees())('$name', ({ tree }) => {
    const errors: unknown[] = [];
    vi.spyOn(console, 'error').mockImplementation((...args: unknown[]) => void errors.push(args));
    const html = renderToString(<SiteApp tree={tree} />);
    const container = document.createElement('div');
    container.innerHTML = html;
    document.body.appendChild(container);

    act(() => {
      hydrateRoot(container, <SiteApp tree={tree} />, { onRecoverableError: (e) => errors.push(e) });
    });

    expect(errors).toEqual([]);
    // So qua DOM đã parse (chuẩn hoá &#x27; → '). React 19 nhấc <link rel=preload> của <img> lên <head> khi hydrate — bỏ ra.
    const strip = (h: string) => h.replace(/<link rel="preload"[^>]*>/g, '');
    const reference = document.createElement('div');
    reference.innerHTML = html;
    expect(strip(container.innerHTML)).toBe(strip(reference.innerHTML));
    container.remove();
  });
});
