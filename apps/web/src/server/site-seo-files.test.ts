// @vitest-environment node
import type { SiteSnapshot } from '@vsite/builder-components';
import { describe, expect, it, vi } from 'vitest';
import {
  buildRobotsTxt,
  buildSitemapXml,
  handleSeoFile,
  SeoFileKind,
  type SeoFileDeps,
} from './site-seo-files';
import { SiteDataError, SiteFetchError, SiteResultKind, type SiteResult } from './site-client';

const ORIGIN = 'https://spa-abc.vsite.vn';
const PUB = '2026-11-01T10:00:00+00:00';
const snap = (pages: unknown[]) => ({ schemaVersion: 1, pages }) as unknown as SiteSnapshot;
const page = (slug: string | null, noIndex: boolean | null = false) => ({ slug, seo: { noIndex } });

describe('buildRobotsTxt', () => {
  it('Allow all + Sitemap tuyệt đối', () => {
    expect(buildRobotsTxt(ORIGIN)).toBe(`User-agent: *\nAllow: /\nSitemap: ${ORIGIN}/sitemap.xml\n`);
  });
});

describe('buildSitemapXml', () => {
  it('trang chủ là origin/, trang khác origin/slug, lastmod = publishedAt', () => {
    const xml = buildSitemapXml(ORIGIN, snap([page(''), page('lien-he')]), PUB);
    expect(xml).toContain('<?xml version="1.0" encoding="UTF-8"?>');
    expect(xml).toContain('<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">');
    expect(xml).toContain(`<loc>${ORIGIN}/</loc>`);
    expect(xml).toContain(`<loc>${ORIGIN}/lien-he</loc>`);
    expect(xml.match(/<lastmod>2026-11-01T10:00:00\+00:00<\/lastmod>/g)).toHaveLength(2);
  });
  it('loại slug null, noIndex=true (R9), slug reserved', () => {
    const xml = buildSitemapXml(ORIGIN, snap([page(''), page(null), page('an', true), page('media'), page('api'), page('ok')]), PUB);
    expect(xml).not.toContain('/an<');
    expect(xml).not.toContain('/media<');
    expect(xml).not.toContain('/api<');
    expect(xml.match(/<loc>/g)).toHaveLength(2);
    expect(xml).toContain(`${ORIGIN}/ok`);
  });
  it('noIndex null vẫn được index', () => {
    expect(buildSitemapXml(ORIGIN, snap([page('a', null)]), PUB)).toContain('/a</loc>');
  });
  it('encodeURIComponent slug có dấu và escape XML', () => {
    const xml = buildSitemapXml(ORIGIN, snap([page('lien-hệ'), page("a&b'c")]), PUB);
    expect(xml).toContain(`${ORIGIN}/lien-h%E1%BB%87</loc>`);
    expect(xml).toContain('a%26b\'c'.replace("'", '&apos;') + '</loc>');
    expect(xml).not.toMatch(/&b/);
  });
  it('escape origin có ký tự XML', () => {
    expect(buildSitemapXml('http://x.vsite.vn:3000/<"&', snap([page('')]), PUB)).toContain('&lt;&quot;&amp;');
  });
  it('lastmod không hợp lệ được chuẩn hoá hoặc bỏ', () => {
    expect(buildSitemapXml(ORIGIN, snap([page('')]), '2026-11-01T10:00:00Z')).toContain('<lastmod>2026-11-01T10:00:00Z</lastmod>');
    expect(buildSitemapXml(ORIGIN, snap([page('')]), 'Nov 1 2026 10:00 UTC')).toContain('<lastmod>2026-11-01T10:00:00.000Z</lastmod>');
    expect(buildSitemapXml(ORIGIN, snap([page('')]), 'rác')).not.toContain('lastmod');
  });
  it('không có trang nào → urlset rỗng hợp lệ', () => {
    expect(buildSitemapXml(ORIGIN, snap([page(null)]), PUB)).toContain('<urlset');
  });
});

function deps(result: SiteResult | Error, over: Partial<SeoFileDeps> = {}) {
  const fetchSite = vi.fn((_h: string) => (result instanceof Error ? Promise.reject(result) : Promise.resolve(result)));
  const logError = vi.fn();
  const d: SeoFileDeps = { rootDomain: 'vsite.vn', fetchSite, logError, ...over };
  return { d, fetchSite, logError };
}
const SITE: SiteResult = {
  kind: SiteResultKind.Site,
  shop: { name: 'Spa', logoUrl: null },
  snapshot: snap([page(''), page('lien-he')]),
  publishedAt: PUB,
  etag: null,
};

describe('handleSeoFile', () => {
  it('host platform → 404, KHÔNG gọi BE', async () => {
    for (const host of ['evil.com', 'www.vsite.vn', 'a.b.vsite.vn', '127.0.0.1:3000', '']) {
      for (const kind of [SeoFileKind.Robots, SeoFileKind.Sitemap]) {
        const { d, fetchSite } = deps(SITE);
        const r = await handleSeoFile(kind, host, 'https:', d);
        expect(r.status).toBe(404);
        expect(r.headers['Content-Type']).toBe('text/plain; charset=utf-8');
        expect(fetchSite).not.toHaveBeenCalled();
      }
    }
  });
  it('shop → 200 robots text/plain, origin từ protocol + host', async () => {
    const { d, fetchSite } = deps(SITE);
    const r = await handleSeoFile(SeoFileKind.Robots, 'SPA-ABC.vsite.vn:8080', 'http:', d);
    expect(r.status).toBe(200);
    expect(r.headers['Content-Type']).toBe('text/plain; charset=utf-8');
    expect(r.headers['Cache-Control']).toBe('private, no-cache');
    expect(r.body).toContain('Sitemap: http://spa-abc.vsite.vn:8080/sitemap.xml');
    expect(fetchSite).toHaveBeenCalledWith('spa-abc.vsite.vn');
  });
  it('shop → 200 sitemap application/xml', async () => {
    const { d } = deps(SITE);
    const r = await handleSeoFile(SeoFileKind.Sitemap, 'spa-abc.vsite.vn', 'https:', d);
    expect(r.status).toBe(200);
    expect(r.headers['Content-Type']).toBe('application/xml; charset=utf-8');
    expect(r.body).toContain(`<loc>${ORIGIN}/lien-he</loc>`);
  });
  it('NotFound và Suspended → 404 text/plain', async () => {
    for (const result of [
      { kind: SiteResultKind.NotFound } as SiteResult,
      { kind: SiteResultKind.Suspended, shop: { name: 'S', logoUrl: null } } as SiteResult,
    ]) {
      for (const kind of [SeoFileKind.Robots, SeoFileKind.Sitemap]) {
        const r = await handleSeoFile(kind, 'spa-abc.vsite.vn', 'https:', deps(result).d);
        expect(r.status).toBe(404);
        expect(r.headers['Content-Type']).toBe('text/plain; charset=utf-8');
      }
    }
  });
  it('SiteFetchError → 502, SiteDataError/bất ngờ → 500, body chung, cause chỉ log', async () => {
    const secret = 'http://10.0.0.5:5270';
    const cases: [Error, number][] = [
      [new SiteFetchError('x', { cause: new Error(secret) }), 502],
      [new SiteDataError('x', { cause: new Error(secret) }), 500],
      [new Error(secret), 500],
    ];
    for (const [err, status] of cases) {
      const { d, logError } = deps(err);
      const r = await handleSeoFile(SeoFileKind.Sitemap, 'spa-abc.vsite.vn', 'https:', d);
      expect(r.status).toBe(status);
      expect(r.headers['Content-Type']).toBe('text/plain; charset=utf-8');
      expect(r.body).not.toContain('10.0.0.5');
      expect(logError).toHaveBeenCalledWith(expect.any(String), err);
    }
  });
});
