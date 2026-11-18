// @vitest-environment node
import { renderToString } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { SiteLoaderKind } from '../server/load-site-core';
import { ErrorPage, RouteErrorPage } from './error-page';
import { NotFoundPage } from './not-found-page';
import { PlatformNotFound, SitePage } from './site-page';
import { SuspendedPage } from './suspended-page';
import { buildSiteHead } from './site-head';

const shop = { name: 'Spa ABC', logoUrl: '/media/logo/abc.png' as string | null };

describe('SuspendedPage', () => {
  it('chỉ tên + logo, không nội dung site', () => {
    const html = renderToString(<SuspendedPage shop={shop} />);
    expect(html).toContain('Spa ABC');
    expect(html).toContain('src="/media/logo/abc.png"');
    expect(html).toContain('tạm ngưng');
    expect(html).not.toContain('data-site-path');
    expect(html).not.toContain('<footer');
  });
  it('logo null → không có img', () => {
    expect(renderToString(<SuspendedPage shop={{ name: 'Spa ABC', logoUrl: null }} />)).not.toContain('<img');
  });
  it('dùng theme mặc định (biến --vs-color)', () => {
    expect(renderToString(<SuspendedPage shop={shop} />)).toContain('--vs-color-');
  });
});

describe('ErrorPage', () => {
  it('chung, không rò chi tiết dù route đưa Error có message', () => {
    const err = new Error('secret http://10.0.0.5/internal ZodError');
    const html = renderToString(<RouteErrorPage error={err} />);
    expect(html).toContain('Đã có lỗi xảy ra');
    expect(html).not.toContain('secret');
    expect(html).not.toContain('10.0.0.5');
    expect(html).not.toContain('ZodError');
    expect(renderToString(<ErrorPage />)).toContain('Đã có lỗi xảy ra');
  });
});

describe('NotFoundPage', () => {
  it('404 chung', () => {
    const html = renderToString(<NotFoundPage />);
    expect(html).toContain('Không tìm thấy trang');
    expect(html).toContain('404');
  });
  it('PlatformNotFound dùng cùng trang', () => {
    expect(renderToString(<PlatformNotFound />)).toBe(renderToString(<NotFoundPage />));
  });
});

describe('SitePage dispatch', () => {
  it('notFound → NotFoundPage', () => {
    expect(renderToString(<SitePage data={{ kind: SiteLoaderKind.NotFound }} />)).toBe(renderToString(<NotFoundPage />));
  });
  it('suspended → SuspendedPage', () => {
    expect(renderToString(<SitePage data={{ kind: SiteLoaderKind.Suspended, shop }} />)).toBe(
      renderToString(<SuspendedPage shop={shop} />),
    );
  });
  it('error → ErrorPage', () => {
    expect(renderToString(<SitePage data={{ kind: SiteLoaderKind.Error }} />)).toBe(renderToString(<ErrorPage />));
  });
});

describe('noindex cho trang hệ thống (T7)', () => {
  it.each([
    [{ kind: SiteLoaderKind.Suspended, shop }],
    [{ kind: SiteLoaderKind.NotFound }],
    [{ kind: SiteLoaderKind.Error }],
  ] as const)('%j có robots noindex', (data) => {
    expect(buildSiteHead(data as never).meta).toContainEqual({ name: 'robots', content: 'noindex' });
  });
});
