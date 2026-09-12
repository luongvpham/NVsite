import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { RenderContextProvider } from '../src/context';
import { RenderTree } from '../src/renderTree';
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

  it('Hero image resolves via ctx.resolveImage stub (không hardcode URL)', () => {
    render(
      <RenderContextProvider>
        <RenderTree tree={homeTree as ComponentNode} />
      </RenderContextProvider>,
    );

    const heroSection = screen.getByText('Chào mừng đến Spa ABC').closest('section');
    expect(heroSection?.style.backgroundImage).toContain('/_dev/placeholder/1600x900,cover.svg');
  });
});
