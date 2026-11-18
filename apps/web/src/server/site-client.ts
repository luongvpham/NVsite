import '@tanstack/react-start/server-only';
import { createApiClient, getPublishedSiteResponse, type PublishedShopDto } from '@vsite/api-sdk';
import { siteSnapshotSchema, type SiteSnapshot } from '@vsite/builder-components';
import { z } from 'zod';
import { readApiOrigin } from './env';
import { LruCache } from './lru';

/**
 * Đọc Shop Site đã publish từ BE `GET /api/site` (WEBSITE-005 T3, #108, #109).
 *
 * - Tenant chỉ đi qua header `Host` = host ĐÃ CHUẨN HOÁ từ `resolveSiteHost` (A11); cùng giá trị làm
 *   key LRU. Không gửi Authorization/Cookie/X-Forwarded-Host (R2). Dùng axios (http adapter) vì
 *   fetch/undici bỏ Host tự đặt (S0) — không viết wrapper fetch.
 * - ETag/304: LRU giữ `{ etag, data }` theo host, gửi `If-None-Match`; 304 → dữ liệu LRU.
 *   Suspended/404 → xoá mục (R4). Dữ liệu hỏng / BE lỗi → ném, không cache (R5).
 * - Message lỗi chung chung (có thể lọt ra HTML); chi tiết nằm ở `cause`.
 */

export type SiteShop = PublishedShopDto;

export const SiteResultKind = {
  NotFound: 'notFound',
  Suspended: 'suspended',
  Site: 'site',
} as const;
export type SiteResultKind = (typeof SiteResultKind)[keyof typeof SiteResultKind];

export type SiteData = {
  readonly kind: typeof SiteResultKind.Site;
  readonly shop: SiteShop;
  readonly snapshot: SiteSnapshot;
  /** `null` khi BE (trái contract) không gửi ETag — khi đó không cache. */
  readonly etag: string | null;
  readonly publishedAt: string;
};

export type SiteResult =
  | { readonly kind: typeof SiteResultKind.NotFound }
  | { readonly kind: typeof SiteResultKind.Suspended; readonly shop: SiteShop }
  | SiteData;

export interface CachedSite {
  readonly etag: string;
  readonly data: SiteData;
}

/**
 * BE không trả được dữ liệu dùng được: 5xx, mạng, 304 không có bản giữ, 404 không phải `SITE_NOT_FOUND`.
 * ⚠️ `cause` có thể chứa BE URL / ProblemDetails / AxiosError — CHỈ log ở server, không bao giờ
 * serialize vào loader data hay HTML.
 */
export class SiteFetchError extends Error {
  override readonly name = 'SiteFetchError';
}

/**
 * BE trả 200 nhưng dữ liệu sai contract/schema (vỏ Zod, snapshot, `schemaVersion`).
 * ⚠️ `cause` (ZodError, dữ liệu BE) CHỈ log ở server, không bao giờ serialize vào loader data hay HTML.
 */
export class SiteDataError extends Error {
  override readonly name = 'SiteDataError';
}

export const SITE_NOT_FOUND = 'SITE_NOT_FOUND';
export const SUPPORTED_SNAPSHOT_SCHEMA_VERSION = 1;
export const SITE_CACHE_MAX_ENTRIES = 200;
/** Timeout mỗi request `GET /api/site` (review I1): BE treo → `SiteFetchError` → 502, không treo SSR. */
export const SITE_FETCH_TIMEOUT_MS = 5000;

export interface SiteClientDeps {
  /** Origin BE tuyệt đối (vd `http://localhost:5270`). */
  readonly apiOrigin: string;
  /** LRU theo host chuẩn hoá. */
  readonly cache: LruCache<string, CachedSite>;
  /** Mặc định {@link SITE_FETCH_TIMEOUT_MS}; test truyền nhỏ hơn. */
  readonly timeoutMs?: number;
}

export interface SiteClient {
  /** `host` phải là `host` đã chuẩn hoá từ `resolveSiteHost` (kind `shop`) — không tự resolve lại. */
  readonly fetchSite: (host: string) => Promise<SiteResult>;
}

/**
 * Vỏ response = schema sinh từ contract, CHỈ nới `publication.publishedAt` để nhận offset (`+00:00`,
 * kèm tới 7 chữ số thập phân) — đúng dạng .NET System.Text.Json ghi `DateTimeOffset`. Orval sinh
 * `datetime({})` (Zod 3 mặc định `offset: false`, chỉ nhận `Z`): lỗi cấu hình Orval. Xoá override này
 * khi config Orval zod có `dateTimeOptions: { offset: true }`.
 */
const generatedPublication = getPublishedSiteResponse.shape.publication.unwrap();
const publishedSiteEnvelope = getPublishedSiteResponse.extend({
  publication: generatedPublication
    .extend({ publishedAt: z.string().datetime({ offset: true }) })
    .nullable(),
});

function errorCodeOf(body: unknown): unknown {
  return typeof body === 'object' && body !== null ? (body as Record<string, unknown>).error_code : undefined;
}

function parseSnapshot(raw: unknown): SiteSnapshot {
  const schemaVersion =
    typeof raw === 'object' && raw !== null ? (raw as Record<string, unknown>).schemaVersion : undefined;
  if (schemaVersion !== SUPPORTED_SNAPSHOT_SCHEMA_VERSION) {
    throw new SiteDataError(
      `Snapshot schemaVersion ${String(schemaVersion)} không được hỗ trợ (chỉ hỗ trợ ${SUPPORTED_SNAPSHOT_SCHEMA_VERSION}).`,
    );
  }
  const parsed = siteSnapshotSchema.safeParse(raw);
  if (!parsed.success) {
    throw new SiteDataError('Snapshot site không hợp lệ.', { cause: parsed.error });
  }
  return parsed.data;
}

export function createSiteClient(deps: SiteClientDeps): SiteClient {
  const { apiOrigin, cache, timeoutMs = SITE_FETCH_TIMEOUT_MS } = deps;

  async function fetchSite(host: string): Promise<SiteResult> {
    if (host === '') throw new TypeError('site-client: host rỗng.');

    const cached = cache.get(host);
    // Client riêng cho từng request; KHÔNG truyền getAccessToken (R2).
    const client = createApiClient({ baseURL: apiOrigin, headers: { Host: host } });

    let response;
    try {
      response = await client.get<unknown>('/api/site', {
        validateStatus: (s) => s === 200 || s === 304 || s === 404,
        // I1/m3: không treo vô hạn; không theo redirect (3xx → lỗi → 502); bỏ qua HTTP(S)_PROXY của env.
        timeout: timeoutMs,
        maxRedirects: 0,
        proxy: false,
        headers: cached ? { 'If-None-Match': cached.etag } : undefined,
      });
    } catch (cause) {
      throw new SiteFetchError('Không tải được dữ liệu site.', { cause });
    }

    if (response.status === 304) {
      if (!cached) throw new SiteFetchError('BE trả 304 nhưng không có bản đã giữ.');
      return cached.data;
    }

    if (response.status === 404) {
      if (errorCodeOf(response.data) !== SITE_NOT_FOUND) {
        throw new SiteFetchError('BE trả 404 không rõ nguồn.', { cause: response.data });
      }
      cache.delete(host);
      return { kind: SiteResultKind.NotFound };
    }

    // 200
    const envelope = publishedSiteEnvelope.safeParse(response.data);
    if (!envelope.success) {
      cache.delete(host);
      throw new SiteDataError('Dữ liệu site không hợp lệ.', { cause: envelope.error });
    }
    const dto = envelope.data;

    if (dto.status === 'Suspended') {
      cache.delete(host);
      return { kind: SiteResultKind.Suspended, shop: dto.shop };
    }
    if (dto.status !== 'Published' || dto.publication === null) {
      cache.delete(host);
      throw new SiteDataError('Dữ liệu site không hợp lệ.', { cause: { status: dto.status } });
    }

    let snapshot: SiteSnapshot;
    try {
      snapshot = parseSnapshot(dto.publication.snapshot);
    } catch (err) {
      cache.delete(host);
      throw err;
    }

    const etagHeader: unknown = response.headers.etag;
    const etag = typeof etagHeader === 'string' && etagHeader !== '' ? etagHeader : null;
    const data: SiteData = {
      kind: SiteResultKind.Site,
      shop: dto.shop,
      snapshot,
      etag,
      publishedAt: dto.publication.publishedAt,
    };
    if (etag === null) cache.delete(host);
    else cache.set(host, { etag, data });
    return data;
  }

  return { fetchSite };
}

/** LRU mặc định cấp process cho production (A9: 200 host, không TTL). */
export const siteCache = new LruCache<string, CachedSite>(SITE_CACHE_MAX_ENTRIES);

/** Bản production: đọc `API_ORIGIN` lười (ném nếu thiếu) và dùng {@link siteCache}. */
export function fetchPublishedSite(host: string): Promise<SiteResult> {
  return createSiteClient({ apiOrigin: readApiOrigin(), cache: siteCache }).fetchSite(host);
}
