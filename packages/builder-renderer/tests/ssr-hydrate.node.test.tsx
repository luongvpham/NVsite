// @vitest-environment node
import { renderToString } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { loadValidTrees, SiteApp } from './helpers/site-app';

/** Snapshot HTML server (#23): renderToString ở Node, không window/document. Hydrate ở `ssr-hydrate.test.tsx`. */
describe('SSR snapshot — SiteLayout + RenderTree ở Node', () => {
  it('chạy ở môi trường không có window/document', () => {
    expect(typeof globalThis.window).toBe('undefined');
    expect(typeof globalThis.document).toBe('undefined');
  });

  const trees = loadValidTrees();
  it('có fixture để phủ', () => {
    expect(trees.length).toBeGreaterThan(0);
  });

  it.each(trees)('$name → HTML server ổn định', ({ tree }) => {
    const html = renderToString(<SiteApp tree={tree} />);
    expect(html).toContain('© 2026 Spa ABC');
    expect(html).toMatchSnapshot();
  });
});
