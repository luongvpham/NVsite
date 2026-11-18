// @vitest-environment node
import { describe, expect, it } from 'vitest';
import { SiteLoaderKind, type SiteLoaderData } from '../server/load-site-core';
import { buildSiteHead, siteLang } from './site-head';

/** T7 (WEBSITE-005): head SEO thuần — title/description fallback, og:image (R10), favicon, canonical (A3), noindex (R9). */
type SiteData = Extract<SiteLoaderData, { kind: 'site' }>;
type Seo = { title: string | null; description: string | null; ogImageId: string | null; noIndex: boolean };

const seo = (over: Partial<Seo> = {}): Seo => ({ title: null, description: null, ogImageId: null, noIndex: false, ...over });
const page = (slug: string | null, title: string, pageSeo: Seo, systemType: string | null = null) => ({
  id: `p_${slug ?? 'nf'}`,
  slug,
  kind: 'Composable',
  systemType,
  title,
  seo: pageSeo,
  tree: {},
});

function site(
  path: string,
  pages: ReturnType<typeof page>[],
  snap: {
    seo?: Partial<Record<string, string | null>>;
    media?: Record<string, { storageKey: string; preset: string | null }>;
    defaultLocale?: string;
  } = {},
): SiteData {
  return {
    kind: SiteLoaderKind.Site,
    shop: { name: 'Spa ABC', logoUrl: null },
    snapshot: {
      defaultLocale: snap.defaultLocale ?? 'vi',
      seo: { defaultTitle: null, defaultDescription: null, ogImageId: null, faviconAssetId: null, ...snap.seo },
      media: snap.media ?? {},
      pages,
    } as unknown as SiteData['snapshot'],
    publishedAt: '2026-11-01T10:00:00+00:00',
    path,
    year: 2026,
    origin: 'https://spa-abc.vsite.vn',
  };
}

type Head = ReturnType<typeof buildSiteHead>;
type Tag = Record<string, string>;
const metas = (h: Head) => (h.meta ?? []) as Tag[];
const meta = (h: Head, key: 'name' | 'property', v: string) => metas(h).find((m) => m[key] === v)?.content;
const title = (h: Head) => metas(h).find((m) => 'title' in m)?.title;
const link = (h: Head, rel: string) => (h.links ?? []).find((l) => l.rel === rel);

const HOME = page('', 'Trang chủ', seo());

describe('buildSiteHead — title / description', () => {
  it('page.seo.title thắng page.title', () => {
    expect(title(buildSiteHead(site('/', [page('', 'Trang chủ', seo({ title: 'SEO Home' }))])))).toBe('SEO Home');
  });
  it('không có seo.title → page.title', () => {
    expect(title(buildSiteHead(site('/', [HOME])))).toBe('Trang chủ');
  });
  it('page.title rỗng → snapshot.seo.defaultTitle → shop.name', () => {
    const empty = page('', '', seo());
    expect(title(buildSiteHead(site('/', [empty], { seo: { defaultTitle: 'Mặc định' } })))).toBe('Mặc định');
    expect(title(buildSiteHead(site('/', [empty])))).toBe('Spa ABC');
  });
  it('description: page.seo → snapshot default → bỏ thẻ', () => {
    const withPage = site('/', [page('', 'T', seo({ description: 'Mô tả trang' }))], {
      seo: { defaultDescription: 'Mô tả chung' },
    });
    expect(meta(buildSiteHead(withPage), 'name', 'description')).toBe('Mô tả trang');
    expect(meta(buildSiteHead(site('/', [HOME], { seo: { defaultDescription: 'Mô tả chung' } })), 'name', 'description')).toBe(
      'Mô tả chung',
    );
    expect(meta(buildSiteHead(site('/', [HOME])), 'name', 'description')).toBeUndefined();
  });
  it('og:title / og:description đi theo', () => {
    const h = buildSiteHead(site('/', [page('', 'T', seo({ description: 'D' }))]));
    expect(meta(h, 'property', 'og:title')).toBe('T');
    expect(meta(h, 'property', 'og:description')).toBe('D');
  });
});

describe('buildSiteHead — noindex (R9)', () => {
  it('seo.noIndex=true → robots noindex', () => {
    expect(meta(buildSiteHead(site('/', [page('', 'T', seo({ noIndex: true }))])), 'name', 'robots')).toBe('noindex');
  });
  it('noIndex=false → không có robots', () => {
    expect(meta(buildSiteHead(site('/', [HOME])), 'name', 'robots')).toBeUndefined();
  });
  it('trang NotFound của shop (404) → noindex, không canonical', () => {
    const h = buildSiteHead(site('/khong-co', [HOME, page(null, 'Không thấy', seo(), 'NotFound')]));
    expect(meta(h, 'name', 'robots')).toBe('noindex');
    expect(link(h, 'canonical')).toBeUndefined();
    expect(title(h)).toBe('Không thấy');
  });
  it('404 không có trang NotFound → title shop.name + noindex', () => {
    const h = buildSiteHead(site('/khong-co', [HOME]));
    expect(meta(h, 'name', 'robots')).toBe('noindex');
    expect(title(h)).toBe('Spa ABC');
  });
});

describe('buildSiteHead — og:image / favicon (R10)', () => {
  const media = {
    m1: { storageKey: 'shops/a/b.jpg', preset: null },
    m2: { storageKey: 'shops/a/c.png', preset: 'thumb' },
    f1: { storageKey: 'shops/a/fav.ico', preset: null },
  };
  it('page.seo.ogImageId → /media/{storageKey} tuyệt đối, bỏ qua preset', () => {
    const h = buildSiteHead(site('/', [page('', 'T', seo({ ogImageId: 'm2' }))], { media, seo: { ogImageId: 'm1' } }));
    expect(meta(h, 'property', 'og:image')).toBe('https://spa-abc.vsite.vn/media/shops/a/c.png');
  });
  it('preset null vẫn dùng file gốc; không có ở trang → snapshot.seo.ogImageId', () => {
    const h = buildSiteHead(site('/', [HOME], { media, seo: { ogImageId: 'm1' } }));
    expect(meta(h, 'property', 'og:image')).toBe('https://spa-abc.vsite.vn/media/shops/a/b.jpg');
  });
  it('id không có trong media → bỏ thẻ, không bao giờ /media/undefined', () => {
    const h = buildSiteHead(site('/', [page('', 'T', seo({ ogImageId: 'ghost' }))], { media }));
    expect(meta(h, 'property', 'og:image')).toBeUndefined();
    expect(JSON.stringify(h)).not.toContain('undefined');
  });
  it('không có ogImageId nào → không thẻ', () => {
    expect(meta(buildSiteHead(site('/', [HOME], { media })), 'property', 'og:image')).toBeUndefined();
  });
  it('favicon từ faviconAssetId; thiếu id trong media → bỏ link', () => {
    expect(link(buildSiteHead(site('/', [HOME], { media, seo: { faviconAssetId: 'f1' } })), 'icon')?.href).toBe(
      'https://spa-abc.vsite.vn/media/shops/a/fav.ico',
    );
    expect(link(buildSiteHead(site('/', [HOME], { media, seo: { faviconAssetId: 'ghost' } })), 'icon')).toBeUndefined();
    expect(link(buildSiteHead(site('/', [HOME], { media })), 'icon')).toBeUndefined();
  });
});

describe('buildSiteHead — canonical (A3)', () => {
  const pages = [HOME, page('lien-he', 'Liên hệ', seo(), 'Contact')];
  it('home → origin + "/"', () => {
    expect(link(buildSiteHead(site('/', pages)), 'canonical')?.href).toBe('https://spa-abc.vsite.vn/');
  });
  it('/lien-he và /lien-he/ cùng canonical /lien-he', () => {
    expect(link(buildSiteHead(site('/lien-he', pages)), 'canonical')?.href).toBe('https://spa-abc.vsite.vn/lien-he');
    expect(link(buildSiteHead(site('/lien-he/', pages)), 'canonical')?.href).toBe('https://spa-abc.vsite.vn/lien-he');
  });
  it('origin giữ cổng (dev) và og:url trùng canonical', () => {
    const d = { ...site('/lien-he', pages), origin: 'http://spa.vsite.local:3000' };
    const h = buildSiteHead(d);
    expect(link(h, 'canonical')?.href).toBe('http://spa.vsite.local:3000/lien-he');
    expect(meta(h, 'property', 'og:url')).toBe('http://spa.vsite.local:3000/lien-he');
  });
  it('slug có dấu được mã hoá', () => {
    const p = [HOME, page('liên-hệ', 'x', seo())];
    expect(link(buildSiteHead(site('/liên-hệ', p)), 'canonical')?.href).toBe('https://spa-abc.vsite.vn/li%C3%AAn-h%E1%BB%87');
  });
});

describe('buildSiteHead — kind khác', () => {
  it('platform → rỗng', () => {
    expect(buildSiteHead({ kind: SiteLoaderKind.Platform })).toEqual({});
  });
  it('platform 404 → noindex', () => {
    const h = buildSiteHead({ kind: SiteLoaderKind.Platform }, { platformNotFound: true });
    expect(meta(h, 'name', 'robots')).toBe('noindex');
  });
  it('suspended → noindex + title shop; error/notFound → noindex', () => {
    const s = buildSiteHead({ kind: SiteLoaderKind.Suspended, shop: { name: 'Spa ABC', logoUrl: null } });
    expect(meta(s, 'name', 'robots')).toBe('noindex');
    expect(title(s)).toBe('Spa ABC');
    expect(meta(buildSiteHead({ kind: SiteLoaderKind.Error }), 'name', 'robots')).toBe('noindex');
    expect(meta(buildSiteHead({ kind: SiteLoaderKind.NotFound }), 'name', 'robots')).toBe('noindex');
  });
  it('undefined (loader lỗi) → rỗng', () => {
    expect(buildSiteHead(undefined)).toEqual({});
  });
});

describe('siteLang', () => {
  it('lang = snapshot.defaultLocale; kind khác → undefined', () => {
    expect(siteLang(site('/', [HOME], { defaultLocale: 'en' }))).toBe('en');
    expect(siteLang({ kind: SiteLoaderKind.Platform })).toBeUndefined();
    expect(siteLang(undefined)).toBeUndefined();
  });
});
