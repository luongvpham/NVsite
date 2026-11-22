import type { SiteSnapshot } from '@vsite/builder-components';
import { SiteDataError, SiteFetchError, SiteResultKind, type SiteResult, type SiteShop } from './site-client';
import { isReservedShopSitePath, selectPage } from '../lib/select-page';
import { resolveSiteHost } from './site-host';

/**
 * Lõi thuần của loader Shop Site (WEBSITE-005 T4, #104, #108). Không đọc request/env — vỏ
 * `load-site.ts` truyền host gốc (`getRequestHost({ xForwardedHost: false })`) và deps.
 *
 * Trả loader data SERIALIZE ĐƯỢC (đi qua RPC khi điều hướng phía client) + status/header HTTP mà
 * vỏ phải đặt. Status quyết định ở server; T5 thêm "không có trang khớp path → 404" ở nhánh Site.
 */

export const SiteLoaderKind = {
  Platform: 'platform',
  NotFound: 'notFound',
  Suspended: 'suspended',
  Site: 'site',
  Error: 'error',
} as const;
export type SiteLoaderKind = (typeof SiteLoaderKind)[keyof typeof SiteLoaderKind];

/**
 * Snapshot đi từ JSON của BE (đã qua Zod) nên luôn serialize được, nhưng schema để vài chỗ là
 * map mở / `unknown` (vd `tracking: Record<string, unknown>`, như `props` của node) và kiểm tra kiểu
 * serialize của Start từ chối `unknown`. Đổi
 * `unknown` → `JsonValue` ở tầng kiểu; vẫn gán ngược được cho `SiteSnapshot` (T5 dùng thẳng).
 */
export type JsonValue = string | number | boolean | null | JsonValue[] | { [key: string]: JsonValue };
export type JsonSafe<T> = unknown extends T
  ? JsonValue
  : T extends readonly (infer U)[]
    ? JsonSafe<U>[]
    : T extends object
      ? { [K in keyof T]: JsonSafe<T[K]> }
      : T;
export type SiteLoaderSnapshot = JsonSafe<SiteSnapshot>;

export type SiteLoaderData =
  | { readonly kind: typeof SiteLoaderKind.Platform }
  | { readonly kind: typeof SiteLoaderKind.NotFound }
  | { readonly kind: typeof SiteLoaderKind.Suspended; readonly shop: SiteShop }
  | {
      readonly kind: typeof SiteLoaderKind.Site;
      readonly shop: SiteShop;
      readonly snapshot: SiteLoaderSnapshot;
      readonly publishedAt: string;
      /** Path request (bắt đầu bằng `/`); `selectPage(snapshot, path)` cho biết trang nào (không khớp → status 404). */
      readonly path: string;
      /** Năm theo giờ Asia/Ho_Chi_Minh, đồng hồ server, tính một lần (#23, R8). */
      readonly year: number;
      /** Origin đang truy cập (`https://host[:port]`), dựng ở server từ request gốc — canonical/og:image tuyệt đối (A3). */
      readonly origin: string;
    }
  /** Không mang chi tiết lỗi (R5) — chi tiết chỉ nằm ở log server. */
  | { readonly kind: typeof SiteLoaderKind.Error };

export type SiteHttpStatus = 200 | 404 | 500 | 502 | 503;

export interface SiteLoadOutcome {
  readonly status: SiteHttpStatus;
  readonly headers: Readonly<Record<string, string>>;
  readonly data: SiteLoaderData;
}

/** A6, #109: HTML Shop Site không cache ở CDN/proxy dùng chung. */
export const SITE_HTML_CACHE_CONTROL = 'private, no-cache';

export interface LoadSiteDeps {
  readonly rootDomain: string;
  /** Origin của request gốc (protocol từ URL request, host từ `getRequestHost`) — không tin header proxy. */
  readonly origin: string;
  /** Nhận host đã chuẩn hoá (kind `shop` của `resolveSiteHost`). */
  readonly fetchSite: (host: string) => Promise<SiteResult>;
  /** Đồng hồ server — chỉ gọi khi cần `year`. */
  readonly now: () => Date;
  /** Log server-side; `error.cause` có thể chứa BE URL/ProblemDetails — không bao giờ ra HTML. */
  readonly logError: (message: string, error: unknown) => void;
}

/** Năm © theo giờ Asia/Ho_Chi_Minh (UTC+7, VN không có DST) — quyết định 2026-11-19, thay cho UTC (B6). */
const VN_UTC_OFFSET_MS = 7 * 60 * 60 * 1000;
const vietnamYear = (d: Date): number => new Date(d.getTime() + VN_UTC_OFFSET_MS).getUTCFullYear();

const SHOP_HEADERS ={ 'Cache-Control': SITE_HTML_CACHE_CONTROL } as const;
const shop = (status: SiteHttpStatus, data: SiteLoaderData): SiteLoadOutcome => ({ status, headers: SHOP_HEADERS, data });

export async function loadSiteForHost(rawHost: string, path: string, deps: LoadSiteDeps): Promise<SiteLoadOutcome> {
  const resolved = resolveSiteHost(rawHost, deps.rootDomain);
  if (resolved.kind === 'platform') {
    // M4: host ngoài `{label}.{ROOT_DOMAIN}` → không gọi BE. Platform mới có trang chủ (#96).
    return { status: path === '/' ? 200 : 404, headers: {}, data: { kind: SiteLoaderKind.Platform } };
  }

  // Plan §2 Phát hiện 2: /media|/api|/assets|/static là tài nguyên (prod: Caddy route sang BE), không phải trang site →
  // 404 chung, KHÔNG gọi BE (không tốn request, không render NotFound của shop cho URL ảnh vỡ).
  if (isReservedShopSitePath(path)) return shop(404, { kind: SiteLoaderKind.NotFound });

  let result: SiteResult;
  try {
    result = await deps.fetchSite(resolved.host);
  } catch (error) {
    if (error instanceof SiteFetchError) {
      deps.logError('[loadSite] BE không trả được dữ liệu site', error);
      return shop(502, { kind: SiteLoaderKind.Error });
    }
    deps.logError(
      error instanceof SiteDataError ? '[loadSite] dữ liệu site sai contract' : '[loadSite] lỗi bất ngờ',
      error,
    );
    return shop(500, { kind: SiteLoaderKind.Error });
  }

  switch (result.kind) {
    case SiteResultKind.NotFound:
      return shop(404, { kind: SiteLoaderKind.NotFound });
    case SiteResultKind.Suspended:
      return shop(503, { kind: SiteLoaderKind.Suspended, shop: result.shop });
    case SiteResultKind.Site:
      // T5: không có trang khớp path → 404 ngay ở server; data vẫn mang site để vẽ NotFound trong layout.
      return shop(selectPage(result.snapshot, path).kind === 'page' ? 200 : 404, {
        kind: SiteLoaderKind.Site,
        shop: result.shop,
        snapshot: result.snapshot as SiteLoaderSnapshot, // chỉ đổi kiểu, dữ liệu là JSON
        publishedAt: result.publishedAt,
        path,
        year: vietnamYear(deps.now()),
        origin: deps.origin,
      });
  }
}
