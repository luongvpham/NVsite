import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { SiteLayout } from './site-layout';

const base = { shop: { name: 'Spa ABC', logoUrl: null }, homeHref: '/', year: 2026 };

describe('SiteLayout — Header/Footer ngoài Component Tree (WEBSITE-003 T2)', () => {
  it('không logo → chỉ hiện tên shop, link về homeHref', () => {
    const { container } = render(<SiteLayout {...base}>nội dung</SiteLayout>);
    const header = container.querySelector('header');
    expect(header?.querySelector('img')).toBeNull();
    expect(header?.querySelector('a')).toHaveAttribute('href', '/');
    expect(header).toHaveTextContent('Spa ABC');
    expect(screen.getByText('nội dung')).toBeInTheDocument();
  });

  it('có logo → <img src> dùng nguyên văn logoUrl', () => {
    const { container } = render(
      <SiteLayout {...base} shop={{ name: 'Spa ABC', logoUrl: '/media/s/logo.png' }}>x</SiteLayout>,
    );
    expect(container.querySelector('header img')).toHaveAttribute('src', '/media/s/logo.png');
  });

  it('menuItems rỗng/không có → không có <nav>; có → href đúng', () => {
    const none = render(<SiteLayout {...base}>x</SiteLayout>);
    expect(none.container.querySelector('nav')).toBeNull();
    none.unmount();
    const empty = render(<SiteLayout {...base} menuItems={[]}>x</SiteLayout>);
    expect(empty.container.querySelector('nav')).toBeNull();
    empty.unmount();
    render(<SiteLayout {...base} menuItems={[{ label: 'Liên hệ', href: '/lien-he' }]}>x</SiteLayout>);
    for (const el of screen.getAllByText('Liên hệ')) expect(el.closest('a')).toHaveAttribute('href', '/lien-he');
  });

  it('Footer: tên + © năm lấy từ props; contact trống → không có vùng liên hệ', () => {
    const { container } = render(<SiteLayout {...base} year={2031}>x</SiteLayout>);
    const footer = container.querySelector('footer');
    expect(footer).toHaveTextContent('© 2031 Spa ABC');
    expect(footer?.querySelector('address')).toBeNull();
  });

  it('contact có dữ liệu → hiện vùng liên hệ', () => {
    const { container } = render(
      <SiteLayout {...base} contact={{ address: '1 Lê Lợi', phone: '0900000000' }}>x</SiteLayout>,
    );
    const addr = container.querySelector('footer address');
    expect(addr).toHaveTextContent('1 Lê Lợi');
    expect(addr).toHaveTextContent('0900000000');
  });

  describe('menu con + mobile (WEBSITE-007 F2)', () => {
    const nested = [
      {
        label: 'Dịch vụ',
        href: '/dich-vu',
        external: false,
        children: [
          {
            label: 'Massage',
            href: '/massage',
            external: false,
            children: [{ label: 'Massage đá nóng', href: '/da-nong', external: false, children: [] }],
          },
        ],
      },
      { label: 'Facebook', href: 'https://fb.com/spa', external: true, children: [] },
    ];

    it('render con và cấp thứ 3 (desktop + mobile)', () => {
      render(<SiteLayout {...base} menuItems={nested}>x</SiteLayout>);
      expect(screen.getAllByText('Massage').length).toBeGreaterThan(0);
      const deep = screen.getAllByText('Massage đá nóng');
      expect(deep.length).toBeGreaterThan(0);
      expect(deep[0]?.closest('a')).toHaveAttribute('href', '/da-nong');
    });

    it('nav có aria-label; mobile dùng <details><summary>', () => {
      const { container } = render(<SiteLayout {...base} menuItems={nested}>x</SiteLayout>);
      expect(container.querySelector('nav')).toHaveAttribute('aria-label');
      expect(container.querySelector('nav details > summary')).not.toBeNull();
    });

    it('link ngoài có target + rel; link nội bộ không', () => {
      render(<SiteLayout {...base} menuItems={nested}>x</SiteLayout>);
      for (const a of screen.getAllByText('Facebook').map((e) => e.closest('a'))) {
        expect(a).toHaveAttribute('target', '_blank');
        expect(a).toHaveAttribute('rel', 'noopener noreferrer');
      }
      for (const a of screen.getAllByText('Massage').map((e) => e.closest('a'))) {
        expect(a).not.toHaveAttribute('target');
        expect(a).not.toHaveAttribute('rel');
      }
    });

    it('nhãn HTML hiện dạng text (R4)', () => {
      const { container } = render(
        <SiteLayout {...base} menuItems={[{ label: '<b>in đậm</b>', href: '/x' }]}>x</SiteLayout>,
      );
      expect(container.querySelector('nav b')).toBeNull();
      expect(screen.getAllByText('<b>in đậm</b>').length).toBeGreaterThan(0);
    });

    it('menu rỗng → không có nav/list', () => {
      const { container } = render(<SiteLayout {...base} menuItems={[]}>x</SiteLayout>);
      expect(container.querySelector('nav')).toBeNull();
      expect(container.querySelector('header ul')).toBeNull();
    });
  });
});
