import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { RenderContextProvider } from '../src/context';
import { RenderTree, type PageRoot } from '../src/renderTree';
import homeTree from './fixtures/home.tree.json';
import type { ComponentNode } from '@vsite/builder-components';

describe('RenderTree — render fixture ra HTML (§9, §14 2.6/2.7)', () => {
  it('renders the full home.tree.json — Section chứa Hero + RichText + Gallery + ServiceGrid', () => {
    const { container } = render(
      <RenderContextProvider>
        <RenderTree tree={homeTree as ComponentNode} />
      </RenderContextProvider>,
    );

    // Section (container) render con của nó — test acceptsChildren (05 §6 quy tắc 5).
    expect(screen.getByText('Chào mừng đến Spa ABC')).toBeInTheDocument();

    // RichText — nội dung đã sanitize nhưng <strong> (được whitelist) vẫn còn.
    expect(container.querySelector('strong')).toHaveTextContent('massage');

    // Gallery — render đúng số ảnh từ list.
    expect(screen.getByText('Phòng massage')).toBeInTheDocument();
    expect(screen.getByText('Khu vực chờ')).toBeInTheDocument();
    expect(container.querySelectorAll('figure')).toHaveLength(2);

    // ServiceGrid — skeleton, KHÔNG có dữ liệu dịch vụ thật (Binding Resolver là Bước 9).
    expect(screen.getByText('Dịch vụ nổi bật')).toBeInTheDocument();
  });

  it('CTA link resolves via ctx.resolveUrl (không hardcode href)', () => {
    render(
      <RenderContextProvider>
        <RenderTree tree={homeTree as ComponentNode} />
      </RenderContextProvider>,
    );

    const link = screen.getByText('Đặt lịch ngay').closest('a');
    expect(link).toHaveAttribute('href', 'https://vsite.vn/dat-lich');
  });

  describe('decorateNode + gốc Page (WEBSITE-002 F6)', () => {
    const pageTree = {
      schemaVersion: 1,
      id: 'root',
      type: 'Page',
      children: [homeTree],
    } as unknown as PageRoot;

    it('gốc Page render children, không render chính nó', () => {
      render(
        <RenderContextProvider>
          <RenderTree tree={pageTree} />
        </RenderContextProvider>,
      );
      expect(screen.getByText('Chào mừng đến Spa ABC')).toBeInTheDocument();
    });

    it('gốc Page rỗng render không có gì và không ném lỗi', () => {
      const { container } = render(
        <RenderContextProvider>
          <RenderTree tree={{ schemaVersion: 1, id: 'root', type: 'Page', children: [] }} />
        </RenderContextProvider>,
      );
      expect(container).toBeEmptyDOMElement();
    });

    it('decorateNode được gọi cho mỗi node (không gọi cho gốc Page), con lồng trong wrapper của cha', () => {
      const seen: string[] = [];
      render(
        <RenderContextProvider>
          <RenderTree
            tree={pageTree}
            decorateNode={(node, rendered) => {
              seen.push(node.id);
              return <div data-node-id={node.id}>{rendered}</div>;
            }}
          />
        </RenderContextProvider>,
      );
      expect(seen).toContain('c_root');
      expect(seen).toContain('c_hero');
      expect(seen).not.toContain('root');
      expect(document.querySelector('[data-node-id="c_hero"]')).not.toBeNull();
      // node con nằm trong wrapper của node cha
      expect(document.querySelector('[data-node-id="c_root"] [data-node-id="c_hero"]')).not.toBeNull();
    });

    it('không truyền decorateNode thì output y như cũ', () => {
      const a = render(
        <RenderContextProvider>
          <RenderTree tree={homeTree as ComponentNode} />
        </RenderContextProvider>,
      ).container.innerHTML;
      const b = render(
        <RenderContextProvider>
          <RenderTree tree={pageTree} />
        </RenderContextProvider>,
      ).container.innerHTML;
      expect(b).toBe(a);
    });
  });

  it('Hero image resolves via ctx.resolveImage stub (không hardcode URL)', () => {
    render(
      <RenderContextProvider>
        <RenderTree tree={homeTree as ComponentNode} />
      </RenderContextProvider>,
    );

    const heroSection = screen.getByText('Chào mừng đến Spa ABC').closest('section');
    expect(heroSection?.style.backgroundImage).toContain('/_dev/placeholder/1600x900,cover.svg');
  });

  describe('API site: basePath/locale/pages (WEBSITE-003 T1, #103)', () => {
    const heroWithPageCta = JSON.parse(JSON.stringify(homeTree)) as ComponentNode;
    const findCta = (n: ComponentNode): Record<string, unknown> | null => {
      const cta = (n.props as { cta?: Record<string, unknown> } | undefined)?.cta;
      if (cta) return cta;
      for (const c of n.children ?? []) {
        const r = findCta(c);
        if (r) return r;
      }
      return null;
    };
    const cta = findCta(heroWithPageCta);
    if (!cta) throw new Error('fixture thiếu cta');
    cta.target = { kind: 'page', pageId: 'p-about' };
    const pages = [{ id: 'p-about', slug: 'gioi-thieu', systemType: null, isEnabled: true }];

    it('truyền pages → CTA page ra href theo #103 (basePath + locale)', () => {
      render(
        <RenderTree tree={heroWithPageCta} basePath="/x" locale="en" defaultLocale="vi" pages={pages} />,
      );
      expect(screen.getByText('Đặt lịch ngay').closest('a')).toHaveAttribute('href', '/x/en/gioi-thieu');
    });

    it('không truyền pages → "#" như cũ', () => {
      render(
        <RenderContextProvider>
          <RenderTree tree={heroWithPageCta} />
        </RenderContextProvider>,
      );
      expect(screen.getByText('Đặt lịch ngay').closest('a')).toHaveAttribute('href', '#');
    });

    it('giữ mediaMap/resolveImage của Provider bên ngoài khi RenderTree bọc thêm context', () => {
      render(
        <RenderContextProvider value={{ resolveImage: () => '/outer.svg' }}>
          <RenderTree tree={heroWithPageCta} pages={pages} />
        </RenderContextProvider>,
      );
      const heroSection = screen.getByText('Chào mừng đến Spa ABC').closest('section');
      expect(heroSection?.style.backgroundImage).toContain('/outer.svg');
    });
  });
});
