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
    expect(screen.getByText('Liên hệ').closest('a')).toHaveAttribute('href', '/lien-he');
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
});
