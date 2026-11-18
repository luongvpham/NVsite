# WEBSITE-005 — Nhu cầu dữ liệu (FE → BE)

> Task FE-only (Bước 8c). Mọi dữ liệu đã có ở `GET /api/site` (WEBSITE-004). Mục tiêu file này: chứng minh **không thiếu field** trước khi dựng loader, để khỏi mở vòng `WEBSITE-005-D{n}` giữa chừng.
> Nguồn: [`../WEBSITE-004/brief.md`](../WEBSITE-004/brief.md) mục A · schema [`envelope-schema.ts:195-225`](../../../packages/builder-components/meta/envelope-schema.ts#L195-L225).

## Màn hình / route

| Route (host `{slug}.{ROOT}`) | Dữ liệu cần | Nguồn trong response | Có đủ? |
|---|---|---|---|
| `/` (trang chủ) | `pages[]` có `slug === ""`; `tree`; `title`; `seo` | `publication.snapshot.pages[]` | ✅ (trang chủ có thể vắng — G10 → NotFound) |
| `/{slug}` | `pages[]` có `slug === {slug}` | như trên | ✅ |
| Trang NotFound (404 trong site) | page có `slug === null` & `systemType === "NotFound"` | như trên | ✅ nếu shop có; **nếu bị tắt/vắng → trang 404 mặc định của app** (không có trong snapshot — chỉ có trang đang bật) |
| Trang Suspended | `shop.name`, `shop.logoUrl` | `shop` | ✅ (`status = "Suspended"`, `publication = null`) |
| Trang 404 (host không phải shop / chưa publish) | không | `404 SITE_NOT_FOUND` | ✅ một mã duy nhất (G4) |
| `/robots.txt`, `/sitemap.xml` | host, danh sách trang bật + `slug` + `seo.noIndex` | `snapshot.pages[]` | ✅ (không có `lastmod` theo trang — dùng `publication.publishedAt` cho cả site) |

## Dữ liệu toàn trang (SiteLayout + `<head>`)

| Cần | Nguồn | Có đủ? |
|---|---|---|
| Tên shop, logo | `shop.name`, `shop.logoUrl` (đã có tiền tố `/media/`, **không** qua `mediaUrl()`) | ✅ — dữ liệu sống, không nằm trong snapshot |
| Theme | `snapshot.theme.tokens` → `themeStyle()` | ✅ |
| Danh sách trang cho `resolveUrl` | `snapshot.pages[]` → `{id, slug, systemType, isEnabled:true}` | ✅ (snapshot chỉ có trang bật nên `isEnabled = true`) |
| `mediaMap` | `snapshot.media` (`imageId → {storageKey, preset}`) — **cùng shape `MediaRef`** của renderer, truyền thẳng | ✅ |
| `<title>` | `page.seo.title ?? page.title`, fallback `snapshot.seo.defaultTitle ?? shop.name` | ✅ |
| `<meta description>` | `page.seo.description ?? snapshot.seo.defaultDescription` | ✅ |
| `robots noindex` | `page.seo.noIndex` | ✅ |
| `og:image` | `page.seo.ogImageId ?? snapshot.seo.ogImageId` → `snapshot.media[id].storageKey` → `/media/{key}` (file gốc, `preset: null`, G9) | ✅ |
| favicon | `snapshot.seo.faviconAssetId` → `snapshot.media[id]` | ✅ |
| `lang` | `snapshot.defaultLocale` | ✅ |
| `ETag` | header `ETag` của response 200 | ✅ nhưng **không có trong OpenAPI**, và hàm Orval không lộ header → ngoại lệ đã biết (brief A.2), xử lý ở T3 |
| Năm © | **không có** (G3) | ➕ FE tự tính **một lần trong loader ở server**, đưa vào loader data (client dùng đúng giá trị đó, không `new Date()` khi render → không lệch hydrate) |
| Anchor `#nodeId` | **không có** (G3) | ➕ việc của renderer (xem plan §3 Flow 4) |
| Canonical URL | không có `ShopDomain` (G2) | ➕ dùng chính host đang truy cập (giả định A3) |

## Câu hỏi mở → quyết định

| # | Câu hỏi | Quyết định | Ai chốt |
|---|---|---|---|
| Q1 | Gộp màn publish Portal? | **Không** — tách WEBSITE-006 | Người duyệt, 2026-11-18 |
| Q2 | Nợ `shop_slug_history` / `/media` 404 dev / CSRF `SameSite=Lax` | (a) chấp nhận + ghi; (b) kiểm trên bản build rồi đóng hoặc ghi; (c) ghi rủi ro, không sửa ở FE | Người duyệt, 2026-11-18 |
| Q3 | Cache SSR | Map trong process, LRU nhỏ, theo ETag; không cache HTML | Người duyệt, 2026-11-18 |
| Q4 | `Suspended`/`SITE_NOT_FOUND` HTTP status của trang HTML | Suspended → **503** + noindex; NotFound → 404; lỗi BE → 502 (A5) | Người duyệt, 2026-11-18 (duyệt cùng plan) |
| Q6 | SDK không export `getPublishedSite`/`PublishedSiteDto`/`site.msw` (barrel viết tay chưa cập nhật ở WEBSITE-004) | Contract **đủ** — không mở `-D1`. T1 sửa barrel viết tay `index.ts` + `mocks.ts` (plan §4) | Người duyệt, 2026-11-18 |
| Q5 | Thiếu field/endpoint? | **Không phát hiện.** Nếu T-spike/T-test lộ ra → DỪNG, mở `WEBSITE-005-D1` | — |
