import { render } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
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

describe('anchor — decorateWithAnchor + useAnchorScroll', () => {
  const scrollIntoView = vi.fn();
  const original: unknown = Reflect.get(Element.prototype, 'scrollIntoView');

  beforeEach(() => {
    scrollIntoView.mockClear();
    Element.prototype.scrollIntoView = scrollIntoView;
    window.location.hash = '';
  });
  afterEach(() => {
    Element.prototype.scrollIntoView = original as typeof Element.prototype.scrollIntoView;
    window.location.hash = '';
  });

  it('wrapper có id = node.id và display: contents', () => {
    const { container } = render(<Harness />);
    const el = container.querySelector('#c_hero') as HTMLElement;
    expect(el).not.toBeNull();
    expect(el.style.display).toBe('contents');
  });

  it('mở URL có hash → cuộn tới phần tử con đầu tiên của wrapper', () => {
    window.location.hash = '#c_hero';
    const { container } = render(<Harness />);
    const wrapper = container.querySelector('#c_hero') as HTMLElement;
    expect(scrollIntoView).toHaveBeenCalledTimes(1);
    expect(scrollIntoView.mock.contexts[0]).toBe(wrapper.firstElementChild);
  });

  it('không có hash → không cuộn', () => {
    render(<Harness />);
    expect(scrollIntoView).not.toHaveBeenCalled();
  });

  it('hashchange → cuộn; hash không tồn tại / mã hoá hỏng → không ném', () => {
    const { container } = render(<Harness />);
    window.location.hash = '#c_hero';
    window.dispatchEvent(new HashChangeEvent('hashchange'));
    expect(scrollIntoView).toHaveBeenCalledTimes(1);
    expect(scrollIntoView.mock.contexts[0]).toBe((container.querySelector('#c_hero') as HTMLElement).firstElementChild);

    window.location.hash = '#nope';
    window.dispatchEvent(new HashChangeEvent('hashchange'));
    window.location.hash = '#%E0%A4%A';
    expect(() => window.dispatchEvent(new HashChangeEvent('hashchange'))).not.toThrow();
    expect(scrollIntoView).toHaveBeenCalledTimes(1);
  });

  it('gỡ listener khi unmount', () => {
    const { unmount } = render(<Harness />);
    unmount();
    window.location.hash = '#c_hero';
    window.dispatchEvent(new HashChangeEvent('hashchange'));
    expect(scrollIntoView).not.toHaveBeenCalled();
  });
});
