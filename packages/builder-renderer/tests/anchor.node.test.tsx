// @vitest-environment node
import { renderToString } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { RenderContextProvider } from '../src/context';
import { decorateWithAnchor, useAnchorScroll } from '../src/anchor';
import { RenderTree } from '../src/renderTree';
import homeTree from './fixtures/home.tree.json';
import type { ComponentNode } from '@vsite/builder-components';

function Harness() {
  useAnchorScroll();
  return (
    <RenderContextProvider>
      <RenderTree tree={homeTree as ComponentNode} decorateNode={decorateWithAnchor} />
    </RenderContextProvider>
  );
}

describe('anchor — SSR ở Node (#23)', () => {
  it('render không đụng window/document và wrapper có id', () => {
    expect(typeof globalThis.window).toBe('undefined');
    const html = renderToString(<Harness />);
    expect(html).toContain('id="c_hero"');
    expect(html).toContain('display:contents');
  });
});
