// @vitest-environment node
import http, { type IncomingHttpHeaders, type IncomingMessage, type ServerResponse } from 'node:http';
import type { AddressInfo } from 'node:net';
import { siteSnapshotSchema } from '@vsite/builder-components';
import { afterAll, beforeAll, beforeEach, describe, expect, it } from 'vitest';
import themeTokensConfig from '../../../../config/theme-tokens.json';
import { server as mswServer } from '../test/msw-server';
import { LruCache } from './lru';
import { createSiteClient, SiteDataError, SiteFetchError, type CachedSite, type SiteResult } from './site-client';

/**
 * T3 (WEBSITE-005): site-client gọi `GET /api/site` với Host tự đặt. Dùng Node http server THẬT
 * (không MSW) để quan sát đúng header rời process — MSW không thấy Host tin cậy được (S0).
 */

// MSW toàn cục (setup.ts, onUnhandledRequest: 'error') sẽ chặn request tới server thật → tắt ở file này.
beforeAll(() => {
  mswServer.close();
});

/** Fixture snapshot dựng tay qua schema (A12). */
const snapshot = (extra: Record<string, unknown> = {}) =>
  siteSnapshotSchema.parse({
    schemaVersion: 1,
    version: 3,
    defaultLocale: 'vi',
    locales: ['vi'],
    theme: { presetKey: null, tokens: themeTokensConfig.defaults },
    navigation: { items: [] },
    seo: { defaultTitle: null, defaultDescription: null, ogImageId: null, faviconAssetId: null },
    tracking: {},
    media: {},
    pages: [
      {
        id: 'p_0001',
        slug: '',
        kind: 'Composable',
        systemType: null,
        title: 'Trang chủ',
        seo: { title: null, description: null, ogImageId: null, noIndex: false },
        tree: { schemaVersion: 1, id: 'root', type: 'Page', children: [] },
      },
    ],
    ...extra,
  });

const PUBLISHED_AT = '2026-11-01T10:00:00Z';
const published = (name = 'Spa ABC', snap: unknown = snapshot()) => ({
  status: 'Published',
  shop: { name, logoUrl: null },
  publication: { id: '11111111-2222-3333-4444-555555555555', version: 3, publishedAt: PUBLISHED_AT, snapshot: snap },
});
const suspended = { status: 'Suspended', shop: { name: 'Spa ABC', logoUrl: null }, publication: null };
const notFoundBody = { type: 'about:blank', title: 'Not Found', status: 404, error_code: 'SITE_NOT_FOUND' };

type Reply = { status: number; body?: unknown; etag?: string };
type Responder = (req: IncomingMessage) => Reply;

let srv: http.Server;
let origin: string;
let responder: Responder;
let seen: IncomingHttpHeaders[];

beforeAll(async () => {
  srv = http.createServer((req: IncomingMessage, res: ServerResponse) => {
    seen.push(req.headers);
    const r = responder(req);
    const headers: Record<string, string> = { 'Cache-Control': 'no-cache' };
    if (r.etag) headers.ETag = r.etag;
    if (r.body !== undefined) headers['Content-Type'] = 'application/json';
    res.writeHead(r.status, headers);
    res.end(r.body === undefined ? undefined : JSON.stringify(r.body));
  });
  await new Promise<void>((resolve) => srv.listen(0, '127.0.0.1', resolve));
  origin = `http://127.0.0.1:${(srv.address() as AddressInfo).port}`;
});
afterAll(() => new Promise<void>((resolve) => {
  srv.close(() => {
    resolve();
  });
}));

let cache: LruCache<string, CachedSite>;
let fetchSite: (host: string) => Promise<SiteResult>;
beforeEach(() => {
  seen = [];
  responder = () => ({ status: 200, body: published(), etag: '"e1"' });
  cache = new LruCache(200);
  fetchSite = createSiteClient({ apiOrigin: origin, cache }).fetchSite;
});

/** Một ETag cho mỗi host; 304 khi If-None-Match khớp. */
function etagServer(bodies: Record<string, { etag: string; body: unknown }>): Responder {
  return (req) => {
    const b = bodies[req.headers.host ?? ''];
    if (!b) return { status: 404, body: notFoundBody };
    if (req.headers['if-none-match'] === b.etag) return { status: 304, etag: b.etag };
    return { status: 200, body: b.body, etag: b.etag };
  };
}

describe('site-client — request leaving the process', () => {
  it('sends GET /api/site with Host = the normalized host given (A11)', async () => {
    let path = '';
    responder = (req) => {
      path = req.url ?? '';
      return { status: 200, body: published(), etag: '"e1"' };
    };
    await fetchSite('spa-abc.vsite.local');
    expect(path).toBe('/api/site');
    expect(seen[0]?.host).toBe('spa-abc.vsite.local');
  });

  it('never sends Authorization, Cookie or X-Forwarded-Host (R2)', async () => {
    await fetchSite('spa-abc.vsite.local');
    const h = seen[0] ?? {};
    expect(h.authorization).toBeUndefined();
    expect(h.cookie).toBeUndefined();
    expect(h['x-forwarded-host']).toBeUndefined();
    expect(h.forwarded).toBeUndefined();
  });

  it('sends no If-None-Match on the first request', async () => {
    await fetchSite('spa-abc.vsite.local');
    expect(seen[0]?.['if-none-match']).toBeUndefined();
  });
});

describe('site-client — results', () => {
  it('200 Published → site with shop, parsed snapshot, etag, publishedAt', async () => {
    const r = await fetchSite('spa-abc.vsite.local');
    expect(r).toEqual({
      kind: 'site',
      shop: { name: 'Spa ABC', logoUrl: null },
      snapshot: snapshot(),
      etag: '"e1"',
      publishedAt: PUBLISHED_AT,
    });
  });

  it('200 Suspended → suspended with shop', async () => {
    responder = () => ({ status: 200, body: suspended });
    expect(await fetchSite('spa-abc.vsite.local')).toEqual({ kind: 'suspended', shop: { name: 'Spa ABC', logoUrl: null } });
  });

  it('404 SITE_NOT_FOUND → notFound', async () => {
    responder = () => ({ status: 404, body: notFoundBody });
    expect(await fetchSite('spa-abc.vsite.local')).toEqual({ kind: 'notFound' });
  });

  it('404 without error_code SITE_NOT_FOUND → SiteFetchError (not a silent notFound)', async () => {
    responder = () => ({ status: 404, body: { error_code: 'SOMETHING_ELSE' } });
    await expect(fetchSite('spa-abc.vsite.local')).rejects.toBeInstanceOf(SiteFetchError);
    responder = () => ({ status: 404 });
    await expect(fetchSite('spa-abc.vsite.local')).rejects.toBeInstanceOf(SiteFetchError);
  });
});

describe('site-client — ETag / LRU', () => {
  it('sends If-None-Match from LRU; 304 → cached data', async () => {
    responder = etagServer({ 'spa-abc.vsite.local': { etag: '"e1"', body: published() } });
    const first = await fetchSite('spa-abc.vsite.local');
    const second = await fetchSite('spa-abc.vsite.local');
    expect(seen[1]?.['if-none-match']).toBe('"e1"');
    expect(second).toEqual(first);
    expect(second.kind).toBe('site');
  });

  it('LRU key is the host: A’s ETag is never sent for B (R3)', async () => {
    responder = etagServer({
      'a.vsite.local': { etag: '"ea"', body: published('Shop A') },
      'b.vsite.local': { etag: '"eb"', body: published('Shop B') },
    });
    await fetchSite('a.vsite.local');
    const b = await fetchSite('b.vsite.local');
    expect(seen[1]?.host).toBe('b.vsite.local');
    expect(seen[1]?.['if-none-match']).toBeUndefined();
    expect(b.kind === 'site' && b.shop.name).toBe('Shop B');
    const a = await fetchSite('a.vsite.local');
    expect(seen[2]?.['if-none-match']).toBe('"ea"');
    expect(a.kind === 'site' && a.shop.name).toBe('Shop A');
  });

  it('shop rename → new ETag → 200 replaces cache and shows the new name (R12)', async () => {
    let state = { etag: '"v1"', body: published('Tên cũ') };
    responder = (req) => (req.headers['if-none-match'] === state.etag ? { status: 304, etag: state.etag } : { status: 200, ...state });
    await fetchSite('spa-abc.vsite.local');
    state = { etag: '"v2"', body: published('Tên mới') };
    const r = await fetchSite('spa-abc.vsite.local');
    expect(seen[1]?.['if-none-match']).toBe('"v1"');
    expect(r.kind === 'site' && r.shop.name).toBe('Tên mới');
    expect(cache.get('spa-abc.vsite.local')?.etag).toBe('"v2"');
    await fetchSite('spa-abc.vsite.local');
    expect(seen[2]?.['if-none-match']).toBe('"v2"');
  });

  it('Suspended clears the LRU entry; old site is not served (R4)', async () => {
    await fetchSite('spa-abc.vsite.local');
    expect(cache.get('spa-abc.vsite.local')).toBeDefined();
    responder = () => ({ status: 200, body: suspended });
    expect((await fetchSite('spa-abc.vsite.local')).kind).toBe('suspended');
    expect(cache.get('spa-abc.vsite.local')).toBeUndefined();
    // Lần sau không gửi ETag cũ.
    await fetchSite('spa-abc.vsite.local');
    expect(seen[2]?.['if-none-match']).toBeUndefined();
  });

  it('404 clears the LRU entry (R4)', async () => {
    await fetchSite('spa-abc.vsite.local');
    responder = () => ({ status: 404, body: notFoundBody });
    expect(await fetchSite('spa-abc.vsite.local')).toEqual({ kind: 'notFound' });
    expect(cache.get('spa-abc.vsite.local')).toBeUndefined();
  });

  it('304 without a cached entry → SiteFetchError (no loop, one request)', async () => {
    responder = () => ({ status: 304, etag: '"e1"' });
    await expect(fetchSite('spa-abc.vsite.local')).rejects.toBeInstanceOf(SiteFetchError);
    expect(seen).toHaveLength(1);
  });

  it('200 Published without ETag → served but not cached', async () => {
    responder = () => ({ status: 200, body: published() });
    const r = await fetchSite('spa-abc.vsite.local');
    expect(r.kind).toBe('site');
    expect(cache.get('spa-abc.vsite.local')).toBeUndefined();
  });
});

describe('site-client — invalid data and failures (R5)', () => {
  it('envelope failing Zod → SiteDataError, nothing cached', async () => {
    responder = () => ({ status: 200, body: { status: 'Published', shop: { name: 1 } }, etag: '"bad"' });
    await expect(fetchSite('spa-abc.vsite.local')).rejects.toBeInstanceOf(SiteDataError);
    expect(cache.get('spa-abc.vsite.local')).toBeUndefined();
  });

  it('snapshot failing siteSnapshotSchema → SiteDataError, nothing cached', async () => {
    responder = () => ({ status: 200, body: published('Spa', { ...snapshot(), extra: 1 }), etag: '"bad"' });
    await expect(fetchSite('spa-abc.vsite.local')).rejects.toBeInstanceOf(SiteDataError);
    expect(cache.get('spa-abc.vsite.local')).toBeUndefined();
  });

  it('schemaVersion !== 1 → clear SiteDataError (Phát hiện 5)', async () => {
    responder = () => ({ status: 200, body: published('Spa', { ...snapshot(), schemaVersion: 2 }), etag: '"v"' });
    await expect(fetchSite('spa-abc.vsite.local')).rejects.toThrow(/schemaVersion/);
    await expect(fetchSite('spa-abc.vsite.local')).rejects.toBeInstanceOf(SiteDataError);
  });

  it('Published with publication null → SiteDataError', async () => {
    responder = () => ({ status: 200, body: { ...published(), publication: null }, etag: '"v"' });
    await expect(fetchSite('spa-abc.vsite.local')).rejects.toBeInstanceOf(SiteDataError);
  });

  it('5xx → SiteFetchError with a generic message (no BE URL), cache kept untouched', async () => {
    await fetchSite('spa-abc.vsite.local');
    responder = () => ({ status: 503, body: { error_code: 'X', detail: 'boom' } });
    const err = await fetchSite('spa-abc.vsite.local').catch((e: unknown) => e);
    expect(err).toBeInstanceOf(SiteFetchError);
    expect((err as Error).message).not.toContain('127.0.0.1');
    expect((err as Error).message).not.toContain(origin);
    expect((err as Error).cause).toBeDefined();
  });

  it('network error → SiteFetchError', async () => {
    const dead = createSiteClient({ apiOrigin: 'http://127.0.0.1:1', cache }).fetchSite;
    const err = await dead('spa-abc.vsite.local').catch((e: unknown) => e);
    expect(err).toBeInstanceOf(SiteFetchError);
    expect((err as Error).message).not.toContain('127.0.0.1');
  });
});
