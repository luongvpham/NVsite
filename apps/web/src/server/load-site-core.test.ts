// @vitest-environment node
import type { SiteSnapshot } from '@vsite/builder-components';
import { describe, expect, it, vi } from 'vitest';
import {
  loadSiteForHost,
  SITE_HTML_CACHE_CONTROL,
  SiteLoaderKind,
  type LoadSiteDeps,
} from './load-site-core';
import { SiteDataError, SiteFetchError, SiteResultKind, type SiteResult } from './site-client';

/**
 * T4 (WEBSITE-005): lõi thuần của loader Shop Site — test không cần Start.
 * Host → resolveSiteHost → fetchSite; trả loader data serialize được + status HTTP vỏ phải đặt.
 */

const ROOT = 'vsite.vn';
const ORIGIN = 'https://spa-abc.vsite.vn';
const SHOP = { name: 'Spa ABC', logoUrl: null };
// Lõi chỉ đọc pages (slug/systemType) để chọn trang (T5) — đối tượng thuần là đủ để kiểm truyền nguyên vẹn.
const SNAPSHOT = { schemaVersion: 1, pages: [{ id: 'p_0001', slug: '', systemType: null }, { id: 'p_0002', slug: 'lien-he', systemType: 'Contact' }] } as unknown as SiteSnapshot;
const PUBLISHED_AT = '2026-11-01T10:00:00+00:00';
const SECRET = 'http://10.0.0.5:5270/api/site';

function deps(result: SiteResult | Error, over: Partial<LoadSiteDeps> = {}) {
  const fetchSite = vi.fn((_host: string) =>
    result instanceof Error ? Promise.reject(result) : Promise.resolve(result),
  );
  const logError = vi.fn();
  const d: LoadSiteDeps = {
    rootDomain: ROOT,
    origin: ORIGIN,
    fetchSite,
    now: () => new Date('2026-12-31T16:59:59Z'),
    logError,
    ...over,
  };
  return { d, fetchSite, logError };
}

const siteResult: SiteResult = {
  kind: SiteResultKind.Site,
  shop: SHOP,
  snapshot: SNAPSHOT,
  etag: '"abc"',
  publishedAt: PUBLISHED_AT,
};

const roundTrip = (v: unknown) => JSON.parse(JSON.stringify(v)) as unknown;

describe('loadSiteForHost — host platform (R1, M4): 0 request tới BE', () => {
  it.each([
    'evil.com',
    'spa-abc.vsite.vn.evil.com',
    'a.b.vsite.vn',
    'admin.vsite.vn',
    'www.vsite.vn',
    '127.0.0.1',
    '127.0.0.1:3000',
    '[::1]:3000',
    '',
    'vsite.vn',
  ])('%j → platform, fetchSite không được gọi', async (host) => {
    const { d, fetchSite } = deps(siteResult);
    const home = await loadSiteForHost(host, '/', d);
    const other = await loadSiteForHost(host, '/lien-he', d);
    expect(fetchSite).not.toHaveBeenCalled();
    expect(home).toEqual({ status: 200, headers: {}, data: { kind: SiteLoaderKind.Platform } });
    // Path khác `/` trên host platform: chưa có trang nào (Main/Profile làm sau, #96) → 404.
    expect(other).toEqual({ status: 404, headers: {}, data: { kind: SiteLoaderKind.Platform } });
  });
});

describe('loadSiteForHost — host shop', () => {
  it('gọi fetchSite đúng một lần với host ĐÃ CHUẨN HOÁ (A11)', async () => {
    const { d, fetchSite } = deps(siteResult);
    await loadSiteForHost('SPA-ABC.VSITE.VN.:3000', '/', d);
    expect(fetchSite).toHaveBeenCalledTimes(1);
    expect(fetchSite).toHaveBeenCalledWith('spa-abc.vsite.vn');
  });

  it('site → 200, data gồm shop/snapshot/publishedAt/path/year; không lộ etag', async () => {
    const { d } = deps(siteResult);
    const r = await loadSiteForHost('spa-abc.vsite.vn', '/lien-he', d);
    expect(r).toEqual({
      status: 200,
      headers: { 'Cache-Control': SITE_HTML_CACHE_CONTROL },
      data: {
        kind: SiteLoaderKind.Site,
        shop: SHOP,
        snapshot: SNAPSHOT,
        publishedAt: PUBLISHED_AT,
        path: '/lien-he',
        year: 2026,
        origin: ORIGIN,
      },
    });
    expect(roundTrip(r.data)).toEqual(r.data); // serialize được (RPC điều hướng client)
    if (r.data.kind === SiteLoaderKind.Site) {
      const forRenderer: SiteSnapshot = r.data.snapshot; // kiểu JsonSafe gán ngược được cho T5
      expect(forRenderer).toBe(SNAPSHOT);
    }
  });

  it('origin của deps đi vào loader data (A3)', async () => {
    const { d } = deps(siteResult, { origin: 'http://spa-abc.vsite.local:3000' });
    const r = await loadSiteForHost('spa-abc.vsite.vn', '/', d);
    expect(r.data).toMatchObject({ origin: 'http://spa-abc.vsite.local:3000' });
  });

  it('year lấy năm theo giờ Asia/Ho_Chi_Minh (UTC+7) từ đồng hồ server, gọi now() đúng một lần (#23, R8)', async () => {
    const now = vi.fn(() => new Date('2027-01-01T00:00:01Z'));
    const { d } = deps(siteResult, { now });
    const r = await loadSiteForHost('spa-abc.vsite.vn', '/', d);
    expect(r.data).toMatchObject({ year: 2027 });
    expect(now).toHaveBeenCalledTimes(1);
  });

  it('year: 17:00 UTC 31/12 đã là năm sau ở Việt Nam; 16:59 UTC thì chưa', async () => {
    const at = (iso: string) => deps(siteResult, { now: () => new Date(iso) }).d;
    expect((await loadSiteForHost('spa-abc.vsite.vn', '/', at('2026-12-31T17:00:00Z'))).data).toMatchObject({ year: 2027 });
    expect((await loadSiteForHost('spa-abc.vsite.vn', '/', at('2026-12-31T16:59:59Z'))).data).toMatchObject({ year: 2026 });
  });

  it('notFound → 404 + Cache-Control', async () => {
    const { d } = deps({ kind: SiteResultKind.NotFound });
    expect(await loadSiteForHost('spa-abc.vsite.vn', '/', d)).toEqual({
      status: 404,
      headers: { 'Cache-Control': SITE_HTML_CACHE_CONTROL },
      data: { kind: SiteLoaderKind.NotFound },
    });
  });

  it('suspended → 503 (A5) + shop, không snapshot', async () => {
    const { d } = deps({ kind: SiteResultKind.Suspended, shop: SHOP });
    expect(await loadSiteForHost('spa-abc.vsite.vn', '/x', d)).toEqual({
      status: 503,
      headers: { 'Cache-Control': SITE_HTML_CACHE_CONTROL },
      data: { kind: SiteLoaderKind.Suspended, shop: SHOP },
    });
  });
});

describe('loadSiteForHost — lỗi (R5): status đúng, log ở server, loader data không có chi tiết', () => {
  it.each([
    ['SiteFetchError', new SiteFetchError('Không tải được dữ liệu site.', { cause: new Error(SECRET) }), 502],
    ['SiteDataError', new SiteDataError('Snapshot site không hợp lệ.', { cause: { url: SECRET } }), 500],
    ['lỗi bất ngờ (vd thiếu API_ORIGIN)', new Error(`boom ${SECRET}`), 500],
  ] as const)('%s → %i', async (_name, err, status) => {
    const { d, logError } = deps(err);
    const r = await loadSiteForHost('spa-abc.vsite.vn', '/', d);
    expect(r).toEqual({
      status,
      headers: { 'Cache-Control': SITE_HTML_CACHE_CONTROL },
      data: { kind: SiteLoaderKind.Error },
    });
    expect(JSON.stringify(r)).not.toContain('10.0.0.5');
    expect(logError).toHaveBeenCalledTimes(1);
    expect(logError.mock.calls[0]).toContain(err); // lỗi kèm cause đi vào log server
  });
});

describe('loadSiteForHost — chọn trang (T5, A10, R6, R7)', () => {
  const pg = (id: string, slug: string | null, systemType: string | null = null) => ({ id, slug, systemType });
  const withPages = (...pages: ReturnType<typeof pg>[]): SiteResult => ({
    ...siteResult,
    snapshot: { schemaVersion: 1, pages } as unknown as SiteSnapshot,
  });
  const run = (r: SiteResult, path: string) => loadSiteForHost('spa-abc.vsite.vn', path, deps(r).d);

  it('có trang khớp → 200', async () => {
    expect((await run(withPages(pg('h', ''), pg('c', 'lien-he')), '/lien-he')).status).toBe(200);
    expect((await run(withPages(pg('h', '')), '/')).status).toBe(200);
  });
  it('không khớp → 404 nhưng data vẫn mang site (để vẽ NotFound trong layout)', async () => {
    const out = await run(withPages(pg('h', '')), '/khong-co');
    expect(out.status).toBe(404);
    expect(out.data.kind).toBe(SiteLoaderKind.Site);
    expect(out.headers['Cache-Control']).toBe(SITE_HTML_CACHE_CONTROL);
  });
  it('R7: thiếu trang chủ → "/" 404', async () => {
    expect((await run(withPages(pg('c', 'lien-he')), '/')).status).toBe(404);
  });
  it('R6: /media/x, /api/x, //, /%E0%A4%A → 404, không ném', async () => {
    const r = withPages(pg('h', ''), pg('m', 'media'));
    for (const p of ['/media/x', '/media', '/api/x', '//', '/%E0%A4%A']) {
      expect((await run(r, p)).status, p).toBe(404);
    }
  });
});

describe('loadSiteForHost — path reserved (plan §2 Phát hiện 2): 404 chung, 0 request tới BE', () => {
  it.each(['/media/x', '/api/x', '/assets/a.js', '/static/x', '/Media/x', '/MEDIA', '/media', '/api/', '/%6dedia/x'])(
    '%s trên host shop → notFound 404 + Cache-Control, fetchSite không được gọi',
    async (path) => {
      const { d, fetchSite } = deps(siteResult);
      const out = await loadSiteForHost('spa-abc.vsite.vn', path, d);
      expect(fetchSite).not.toHaveBeenCalled();
      expect(out).toEqual({
        status: 404,
        headers: { 'Cache-Control': SITE_HTML_CACHE_CONTROL },
        data: { kind: SiteLoaderKind.NotFound },
      });
    },
  );
  it.each(['/lien-he', '/', '/mediathek', '/media-kit'])('%s không bị coi là reserved → gọi fetchSite', async (path) => {
    const { d, fetchSite } = deps(siteResult);
    await loadSiteForHost('spa-abc.vsite.vn', path, d);
    expect(fetchSite).toHaveBeenCalledTimes(1);
  });
  it('/lien-he vẫn 200 như cũ', async () => {
    const { d } = deps(siteResult);
    expect((await loadSiteForHost('spa-abc.vsite.vn', '/lien-he', d)).status).toBe(200);
  });
});
