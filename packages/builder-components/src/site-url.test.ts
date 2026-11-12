import { describe, expect, it } from 'vitest';
import { createResolveUrl, matchSiteUrl, type SitePageRef, type SiteUrlOptions } from './site-url';

const pages: SitePageRef[] = [
  { id: 'p-home', slug: '', systemType: null, isEnabled: true },
  { id: 'p-about', slug: 'gioi-thieu', systemType: null, isEnabled: true },
  { id: 'p-off', slug: 'tat', systemType: null, isEnabled: false },
  { id: 'p-contact', slug: 'lien-he', systemType: 'Contact', isEnabled: true },
  { id: 'p-404', slug: null, systemType: 'NotFound', isEnabled: true },
];

const opts = (over: Partial<SiteUrlOptions> = {}): SiteUrlOptions => ({
  basePath: '',
  locale: 'vi',
  defaultLocale: 'vi',
  pages,
  ...over,
});

describe('createResolveUrl — quy tắc URL Shop Site (#103)', () => {
  it('trang chủ ở locale mặc định → "/" ; locale khác → "/en/"', () => {
    expect(createResolveUrl(opts())({ kind: 'page', pageId: 'p-home' })).toBe('/');
    expect(createResolveUrl(opts({ locale: 'en' }))({ kind: 'page', pageId: 'p-home' })).toBe('/en/');
  });

  it('trang thường ghép basePath + slug, có/không có "/" cuối ở basePath đều không ra "//"', () => {
    for (const basePath of ['', '/x', '/x/']) {
      const base = basePath.replace(/\/+$/, '');
      expect(createResolveUrl(opts({ basePath }))({ kind: 'page', pageId: 'p-about' })).toBe(`${base}/gioi-thieu`);
      expect(createResolveUrl(opts({ basePath }))({ kind: 'page', pageId: 'p-home' })).toBe(`${base}/`);
    }
  });

  it('locale khác mặc định chèn /{locale}/ trước slug', () => {
    expect(createResolveUrl(opts({ basePath: '/x', locale: 'en' }))({ kind: 'page', pageId: 'p-about' })).toBe('/x/en/gioi-thieu');
  });

  it('trang tắt vẫn có URL (preview báo lý do, không đổi href)', () => {
    expect(createResolveUrl(opts())({ kind: 'page', pageId: 'p-off' })).toBe('/tat');
  });

  it('trang đã xoá hoặc trang NotFound (không có slug) → "#"', () => {
    const r = createResolveUrl(opts());
    expect(r({ kind: 'page', pageId: 'ghost' })).toBe('#');
    expect(r({ kind: 'page', pageId: 'p-404' })).toBe('#');
    expect(r({ kind: 'systemPage', systemType: 'NotFound' })).toBe('#');
  });

  it('systemPage: có trang → slug của trang đó; chưa có trang → route đầu tiên trong config/system-pages.json', () => {
    const r = createResolveUrl(opts());
    expect(r({ kind: 'systemPage', systemType: 'Contact' })).toBe('/lien-he');
    expect(r({ kind: 'systemPage', systemType: 'ProductListing' })).toBe('/san-pham');
    expect(r({ kind: 'systemPage', systemType: 'Booking' })).toBe('/dat-lich');
    expect(r({ kind: 'systemPage', systemType: 'KhongCoLoaiNay' })).toBe('#');
  });

  it('external và anchor giữ nguyên; productCategory → "#"', () => {
    const r = createResolveUrl(opts());
    expect(r({ kind: 'external', url: 'https://a.vn/x' })).toBe('https://a.vn/x');
    expect(r({ kind: 'anchor', nodeId: 'n-1' })).toBe('#n-1');
    expect(r({ kind: 'productCategory', categoryId: 3 })).toBe('#');
  });
});

describe('matchSiteUrl — nghịch đảo của resolveUrl', () => {
  const combos: Partial<SiteUrlOptions>[] = [
    {},
    { basePath: '/x' },
    { basePath: '/x/' },
    { locale: 'en' },
    { basePath: '/__preview', locale: 'en' },
  ];

  it('mọi trang có slug: match(resolve(page)) trả đúng pageId + enabled', () => {
    for (const c of combos) {
      const o = opts(c);
      const resolve = createResolveUrl(o);
      for (const p of pages.filter((x) => x.slug !== null)) {
        expect(matchSiteUrl(resolve({ kind: 'page', pageId: p.id }), o)).toEqual({
          kind: 'page',
          pageId: p.id,
          enabled: p.isEnabled,
        });
      }
    }
  });

  it('bỏ query và hash trước khi tra', () => {
    expect(matchSiteUrl('/gioi-thieu?a=1#b', opts())).toEqual({ kind: 'page', pageId: 'p-about', enabled: true });
    expect(matchSiteUrl('/?utm=1', opts())).toEqual({ kind: 'page', pageId: 'p-home', enabled: true });
  });

  it('systemPage chưa có trang → systemMissing', () => {
    const o = opts();
    expect(matchSiteUrl(createResolveUrl(o)({ kind: 'systemPage', systemType: 'Booking' }), o)).toEqual({
      kind: 'systemMissing',
      systemType: 'Booking',
    });
  });

  it('slug lạ, locale lạ, ngoài basePath, "#" → deleted, không ném lỗi', () => {
    expect(matchSiteUrl('/khong-co', opts())).toEqual({ kind: 'deleted' });
    expect(matchSiteUrl('/fr/gioi-thieu', opts())).toEqual({ kind: 'deleted' });
    expect(matchSiteUrl('/gioi-thieu', opts({ locale: 'en' }))).toEqual({ kind: 'deleted' });
    expect(matchSiteUrl('/y/gioi-thieu', opts({ basePath: '/x' }))).toEqual({ kind: 'deleted' });
    expect(matchSiteUrl('/xy/gioi-thieu', opts({ basePath: '/x' }))).toEqual({ kind: 'deleted' });
    expect(matchSiteUrl('#', opts())).toEqual({ kind: 'deleted' });
    expect(matchSiteUrl('', opts())).toEqual({ kind: 'deleted' });
  });

  it('external và anchor', () => {
    expect(matchSiteUrl('https://a.vn', opts())).toEqual({ kind: 'external' });
    expect(matchSiteUrl('mailto:a@b.vn', opts())).toEqual({ kind: 'external' });
    expect(matchSiteUrl('//cdn.x/y', opts())).toEqual({ kind: 'external' });
    expect(matchSiteUrl('#n-1', opts())).toEqual({ kind: 'anchor', nodeId: 'n-1' });
  });
});
