# WEBSITE-005 — `apps/web` render Shop Site công khai (Bước 8c)

> **Đã chốt 2026-11-18 — chưa code** (nhánh `feature/WEBSITE-005`). Người duyệt đồng ý, gồm cả giả định A1–A12.
>
> **Lane C.** App SSR dựng lần đầu, lần đầu SSR gọi BE bằng `Host` tự đặt, chạm `apps/web` + `builder-renderer` + barrel viết tay của `api-sdk` (Phát hiện 5).
> **FE-only** — BE 8b đã xong, contract đã promote. Cổng: Gate 2 (Gate 1 chỉ khi mở vòng `WEBSITE-005-D{n}`). Một session FE, task tuần tự S0 → T1…T11.
>
> **Người thực thi dùng skill:** `superpowers:subagent-driven-development` hoặc `superpowers:executing-plans`. Mỗi bước có checkbox `- [ ]`. Subagent chạy **foreground**.

**Mục tiêu:** Khách mở `{slug}.vsite.local` (sau này `{slug}.vsite.vn`) thấy bản **đang publish** của shop, HTML render ở server, hydrate không cảnh báo; publish/rollback đổi site ngay; sửa draft không lộ.
**Nhu cầu dữ liệu:** [`data-needs.md`](data-needs.md) (không thiếu field) · **Nguồn:** [`../WEBSITE-004/brief.md`](../WEBSITE-004/brief.md) mục A, D · `step.md` §8c
**Quyết định liên quan:** #7, #10, #11, #23, #53, #94, #98, #103, #104, #108, #109, #111, #112

---

## 1. Yêu cầu (đã chốt với người, 2026-11-18)

- **Vai trò:** khách vãng lai, không token.
- **Điểm chạm:** mọi URL trên host `{slug}.{ROOT_DOMAIN}`; `/robots.txt`; `/sitemap.xml`; trang NotFound / Suspended / 404 chung.
- **Xong khi** (`step.md` §8c): publish → site thấy bản mới · sửa draft không lộ · rollback → về bản cũ · view-source thấy HTML server-render, hydrate không cảnh báo.
- **Phạm vi: chỉ `apps/web`** (+ phần nhỏ trong `builder-renderer` cho anchor). Màn publish/rollback Portal + nạp lại version draft (R9) + cảnh báo `references` (brief B, C) → **task riêng WEBSITE-006**. Nghiệm thu 8c bằng `curl` publish.
- **Nợ mang theo:** (a) `shop_slug_history`: **chấp nhận**, ghi `00-INDEX` §4, task riêng sau (cần BE + Gate 1). (b) `/media` 404 HTML: chỉ lỗi dev (Vite proxy) — T10 kiểm trên bản build `node .output`; ổn thì đóng, không ổn thì ghi. (c) CSRF `SameSite=Lax`: **không sửa ở FE**, ghi rủi ro trước production vào `00-INDEX` §4, đề xuất task IDENTITY riêng.
- **Cache SSR:** Map trong process, LRU 200 host, theo ETag; không cache HTML (#109).
- **Không làm:** custom domain / `ShopDomain` (G2) · `NavigationConfig` (Bước 7; `menuItems` để trống) · bind `Service`/`Product` (Bước 9) · prune (8d) · Shop Profile / Main (M3–M4) · overlay đa ngôn ngữ · redirect slug cũ · sửa BE/contract/generated.

## 2. Hiện trạng (khảo sát 2026-11-18)

| Khả năng | Trạng thái | Ở đâu | Ghi chú |
|---|---|---|---|
| Endpoint `GET /api/site` (ETag/304, theo Host) | ✅ | [SiteEndpoints.cs:21-38](../../../backend/src/Vsite.Api/Website/SiteEndpoints.cs#L21-L38) | `Cache-Control: no-cache`; ETag chỉ khi Published |
| SDK `getPublishedSite` | ✅ | [site.ts:31](../../../packages/api-sdk/src/generated/website/site/site.ts#L31) | Chỉ trả `data` → **không lộ `ETag` / `304`** (Phát hiện 1) |
| `createApiClient({baseURL, headers})` | ✅ | [axios-instance.ts:174-186](../../../packages/api-sdk/src/mutator/axios-instance.ts#L174-L186) | Không token nếu không truyền `getAccessToken` |
| `siteSnapshotSchema` | ✅ | [index.ts:13-14](../../../packages/builder-components/src/index.ts#L13-L14) | Shape: [envelope-schema.ts:195-225](../../../packages/builder-components/meta/envelope-schema.ts#L195-L225) (có `page.seo.noIndex`) |
| `RenderTree` / `SiteLayout` / `createResolveUrl` | ✅ | [renderTree.tsx:67](../../../packages/builder-renderer/src/renderTree.tsx#L67) · [site-layout.tsx:21](../../../packages/builder-renderer/src/site-layout.tsx#L21) · [site-url.ts](../../../packages/builder-components/src/site-url.ts) | `year` truyền từ ngoài |
| `snapshot.media` = shape `MediaRef` | ✅ | [context.tsx:15-18](../../../packages/builder-components/src/context.tsx#L15-L18) | Truyền thẳng vào `RenderContextProvider value={{mediaMap}}` |
| Mẫu ghép theme + layout + tree | ✅ tham chiếu | [site-preview.tsx:172-185, 241-251](../../../apps/portal/src/components/website/site-preview.tsx#L172-L185) | |
| Anchor `#nodeId` | 🟡 | `decorateWithId` ở [site-preview.tsx:37-41](../../../apps/portal/src/components/website/site-preview.tsx#L37-L41) | Wrapper `display: contents` **không cuộn được** bằng hash của trình duyệt — Portal tự `scrollIntoView` ở [dòng 112-121](../../../apps/portal/src/components/website/site-preview.tsx#L112-L121). Site thật cần làm lại (Flow 4) |
| `getRequestHost`, `setResponseStatus`, `setResponseHeader` của Start | ✅ tồn tại | `@tanstack/start-server-core/dist/esm/request-response.d.ts:26` | `getRequestHost({xForwardedHost?})` — luôn truyền `false` tường minh |
| Reserved subdomain / shop-site path | ✅ | [reserved-routes.json](../../../config/reserved-routes.json) (`reservedSubdomains`, `reservedShopSitePaths: media, api, assets, static`) | Đọc qua [reserved-routes.ts](../../../apps/web/src/lib/reserved-routes.ts) |
| Cấm `apps/web` import `builder-core`; `packages/` không import `apps/` | ✅ | [eslint.config.mjs:59-87](../../../eslint.config.mjs) | Đã có sẵn |
| `apps/web` | ❌ chỉ khung | [index.tsx](../../../apps/web/src/routes/index.tsx), [__root.tsx](../../../apps/web/src/routes/__root.tsx), [package.json](../../../apps/web/package.json) | Chưa có dep `builder-components`/`builder-renderer`, loader, route site, env |
| Tenant BE theo nhãn đầu Host | 🟡 | [TenantResolutionMiddleware.cs:27-42](../../../backend/src/Vsite.Api/Tenancy/TenantResolutionMiddleware.cs#L27-L42) | `evil.com` → slug `evil` (M4). Chặn ở FE (A1) |
| `shop_slug_history` | ❌ | grep `slug_history` trong `backend/src`: 0 | Chấp nhận (§1) |

**Kết quả spike nháp (chạy trong scratchpad, không đụng repo, Node v24.14):** `fetch`/undici **bỏ** `Host` tự đặt (server nhận `127.0.0.1:port`); `axios` (adapter `http`) và `axios.create({headers:{Host}})` **gửi đúng** `Host`. ⇒ phải dùng axios (đã là nền của `createApiClient`), **không** viết wrapper bằng `fetch`. Chưa kiểm trong bundle Nitro — đó là S0.

### Phát hiện khi khảo sát
1. **ETag/304 không đi qua hàm Orval** ([axios-instance.ts:195-196](../../../packages/api-sdk/src/mutator/axios-instance.ts#L195-L196) chỉ trả `response.data`; axios ném lỗi với 304 trừ khi `validateStatus`). Không phải thiếu contract (brief A.2 đã nói header không nằm trong OpenAPI) ⇒ **không mở `-D1`**. Giải: wrapper trong `apps/web` gọi `client.get(...)` với `validateStatus`, dùng type `PublishedSiteDto` từ SDK. Không sửa `api-sdk` (A4).
2. Dev proxy `/media`, `/api` ở [vite.config.ts:35-37](../../../apps/web/vite.config.ts#L35-L37) chỉ có ở dev; prod do Caddy. Route site **phải** từ chối slug `media|api|assets|static` (đã nằm `reservedShopSitePaths`) thay vì render NotFound của site.
3. [apps/web/CLAUDE.md](../../../apps/web/CLAUDE.md) "Phạm vi ghi" cấm `packages/api-sdk/**` và không liệt kê `packages/builder-renderer`. Plan này **cần sửa `builder-renderer`** (anchor) — người duyệt rà ở A8.
4. **Barrel `api-sdk` thiếu export của WEBSITE-004** (phát hiện khi rà plan): contract đã promote + regen ở `d592df9` (`generated/website/site/site.ts`, `model/publishedSiteDto.ts`, `publicationDto.ts`… có đủ), nhưng [index.ts:42-61](../../../packages/api-sdk/src/index.ts#L42-L61) và [mocks.ts](../../../packages/api-sdk/src/mocks.ts) viết tay chưa export `site/site`, `site.msw` và các model `published*`/`publication*`. `package.json` chỉ mở `.` và `./mocks` ⇒ `apps/web` không import được. Không phải thiếu contract ⇒ không mở `-D1`. Xử lý ở T1 (xem §4).
5. **Snapshot upcaster (#94):** `step.md` §8c ghi "đọc snapshot qua upcaster". Chưa có upcaster nào (v1). T3 kiểm `schemaVersion === 1` sau khi parse Zod; khác → lỗi 500 rõ ràng, không render mò.
6. Hạ tầng: `X-Forwarded-Host` — BE chưa bật `UseForwardedHeaders` ([WEBSITE-004 changelog](../WEBSITE-004/changelog.md) mục Hạ tầng) — Caddy thật chưa kiểm; FE không phụ thuộc.

## 3. Flow

### Flow 1 — Khách mở trang site
```
1. Trình duyệt GET https://spa-abc.vsite.vn/lien-he        ❌ chưa có route (T4)
2. SSR đọc host từ URL gốc (getRequestHost, xForwardedHost:false)   ✅ API có; ❌ chưa dùng
3. resolveSiteHost: đúng 1 nhãn dưới ROOT_DOMAIN & không reserved? ❌ T2 (nếu sai → trang platform/404, KHÔNG gọi BE)
4. createApiClient({baseURL: API_ORIGIN, headers:{Host}}) cho TỪNG request; không token/cookie   ✅ SDK / ❌ T3
5. GET /api/site (If-None-Match nếu LRU có)                 ✅ BE
6. 200 → parse siteSnapshotSchema → lưu LRU {etag, data}; 304 → dùng LRU; 404 → NotFound chung; Suspended → trang tạm ngưng, xoá LRU   ❌ T3
7. Chọn trang theo slug (decode, bỏ "/" cuối); thiếu → trang NotFound của site, status 404   ❌ T5
8. themeStyle + SiteLayout(year từ loader) + RenderTree(basePath '', mediaMap=snapshot.media)   ✅ package / ❌ T5
9. <head>: title, description, og, favicon, canonical, noindex  ❌ T7
10. Hydrate: client dùng đúng loader data, không new Date()    ❌ T10
```
**Nhánh lỗi (hành vi mặc định):** snapshot parse lỗi → 500 trang lỗi chung (BE bảo đảm hợp lệ, #94) · BE 5xx / mạng / Redis lỗi (M3/M6) → 502 trang lỗi chung, **không** cache · host không phải shop → trang platform hiện tại (`/`) hoặc 404 (path khác) · trang chủ vắng (G10) → `/` ra NotFound 404 · NotFound của site bị tắt/vắng → trang 404 mặc định của app (theme mặc định).

### Flow 2 — Publish rồi xem (nghiệm thu)
```
1. Owner POST /api/shops/{id}/website/publications (curl)   ✅ BE 8b (WEBSITE-006 sẽ làm UI)
2. Reload spa-abc.vsite.local:3000 → ETag đổi → 200 mới      ❌ T3/T10
3. Sửa draft (PUT) → reload → vẫn bản cũ                     ✅ BE bất biến / ❌ T10 kiểm
4. POST rollback → reload → về bản cũ                        ❌ T10
```

### Flow 3 — Website Suspended / shop Closed / Draft
`Suspended` → `200 {status:"Suspended", publication:null}`, không ETag → trang tạm ngưng (503, noindex, xoá LRU). `Closed`/chưa publish/`Draft` → `404 SITE_NOT_FOUND` → một trang 404 chung, không phân biệt (G4).

### Flow 4 — Link anchor `#nodeId`
Link `{kind:'anchor'}` đã ra `#nodeId` ([site-url.ts](../../../packages/builder-components/src/site-url.ts)), nhưng không có phần tử mang `id` cuộn được. Giải (A8): `builder-renderer` export `decorateWithAnchor` (wrapper `display: contents` có `id`) + hook `useAnchorScroll()` (sau hydrate và khi `hashchange`: cuộn tới `firstElementChild` của wrapper). Không `window` trong logic render chính (#23) — chỉ trong effect. Portal có thể đổi dùng chung sau (ngoài phạm vi, ghi changelog).

## 4. Đã chốt trong session lập plan

| # | Chốt |
|---|---|
| — | Phạm vi chỉ `apps/web`; màn publish Portal → WEBSITE-006 (người duyệt) |
| — | Nợ (a) chấp nhận, (b) kiểm bản build, (c) ghi rủi ro (người duyệt) |
| — | Cache SSR: LRU trong process theo ETag, không HTML (người duyệt) |
| — | **Barrel `api-sdk`** (khi rà plan): T1 được sửa **chỉ** `packages/api-sdk/src/index.ts` + `mocks.ts` để thêm export còn thiếu của WEBSITE-004. Không đụng `generated/**`, không đụng mutator. Ngoại lệ có chủ đích với "Phạm vi ghi" của `apps/web/CLAUDE.md` (người duyệt, 2026-11-18) |
| — | Không cấp số `#N` mới: không có quyết định kiến trúc mới — chi tiết thực thi của #108/#109 |

## 5. Trình tự

Một session FE, tuần tự. **S0 (spike) làm trước và có quyền dừng cả plan.**

| Task | Việc | Phụ thuộc |
|---|---|---|
| **S0** | Spike: `Host` tự đặt có sống tới BE trong bản build Nitro không + kiểm API Start | — |
| T1 | Barrel SDK, dependency, env, cấu hình `apps/web` | S0 |
| T2 | `resolveSiteHost` (chặn M4 ở FE) | T1 |
| T3 | `site-client` (axios + ETag/304 + LRU + Zod) | T1, T2 |
| T4 | Loader + route site, status HTTP, header cache | T3 |
| T5 | `SiteView`: chọn trang, theme, layout, render | T4 |
| T6 | Anchor ở `builder-renderer` + dùng ở `apps/web` | T5 |
| T7 | `<head>` SEO | T5 |
| T8 | Trang NotFound mặc định / Suspended / lỗi | T4 |
| T9 | `robots.txt`, `sitemap.xml` | T3 |
| T10 | Hydrate test + nghiệm thu tay (Flow 2) + kiểm `/media` bản build | T5–T9 |
| T11 | Tài liệu: `changelog.md`, `00-INDEX` §3/§4, `apps/web/CLAUDE.md` | T10 |

## 6. Ràng buộc toàn cục

- **Tenant (#21, #108):** FE **không bao giờ** chọn shop bằng thứ khác ngoài host của request gốc. Không `X-Forwarded-Host`/`Forwarded`, không query/path/cookie. Không forward `Authorization`/`Cookie` của khách. Host ngoài `{label}.{ROOT_DOMAIN}` → **không gọi BE** (M4).
- **Không đọc draft** (`05` §7): `apps/web` chỉ import `getPublishedSite`/type của nó; test cấm import hook draft/page của SDK.
- **#23 isomorphic:** không `window`/`document`/`Date.now()` trong logic render; năm © đến từ loader data.
- **#19:** `const object + union`, không `enum` TS (kết quả `SiteResult` là discriminated union).
- **Generated:** không sửa `packages/api-sdk/src/generated/**` / `packages/builder-components/generated/**`. Thiếu field/endpoint → DỪNG, mở `WEBSITE-005-D{n}`.
- **Server fn không nhận host:** `loadSite` chỉ nhận `path` (splat) làm input; host **luôn** đọc trong server fn bằng `getRequestHost({ xForwardedHost: false })`. Server fn là endpoint RPC gọi được từ ngoài — nhận host qua input là mở cửa đọc shop bất kỳ.
- **Server-only:** module có `API_ORIGIN`/LRU import `@tanstack/react-start/server-only` (không lọt vào bundle client).
- **Lỗi:** map theo `error_code`, không parse text; không rò stack/BE URL ra HTML.
- Subagent: foreground.

## 7. Review focus

| # | Tình huống | Hành vi đúng | Test ở |
|---|---|---|---|
| R1 | Host `evil.com`, `spa-abc.vsite.vn.evil.com`, `a.b.vsite.vn`, `admin.vsite.vn`, `www.vsite.vn`, IP, `X-Forwarded-Host: spa-xyz.vsite.vn` | Không bao giờ chọn nhầm shop; host ngoài mẫu → **0 request** tới BE | T2, T4 |
| R2 | Request đến có `Cookie`/`Authorization` | Request SSR→BE **không** có hai header đó | T3 |
| R3 | Hai host A, B; ETag của A | Key LRU theo host chuẩn hoá; `If-None-Match` của A không gửi cho B | T3 |
| R4 | LRU đang giữ bản A, BE trả `Suspended` (không ETag) hoặc 404 | Xoá mục LRU; không phục vụ bản cũ | T3 |
| R5 | Snapshot sai schema / BE 5xx / Redis lỗi (M3) | 500/502 trang chung, không cache, không lộ chi tiết | T3, T4 |
| R6 | Path `/media/x`, `/api/x`, `//`, `/Lien-He`, `/lien-he/`, `/%2e%2e`, `/%E0%A4%A` (URI hỏng) | `media|api|assets|static` không thành trang site; còn lại theo quy tắc A10, `decodeURIComponent` lỗi → 404, không ném | T5 |
| R13 | Gọi thẳng RPC của server fn `loadSite` với input chứa `host`/`shopId` | Bị bỏ qua — input chỉ có `path` | T4 |
| R7 | Trang chủ vắng (G10) | `/` → NotFound 404, không crash | T5 |
| R8 | Giao thừa: loader chạy 23:59:59 31/12, client hydrate 00:00:01 | Không lệch (năm từ loader) | T10 |
| R9 | `seo.noIndex = true` | `<meta robots noindex>` và loại khỏi sitemap | T7, T9 |
| R10 | OG/favicon `preset: null`, imageId không có trong `media` | Dùng file gốc; thiếu → bỏ thẻ, không `/media/undefined` | T7 |
| R11 | Sửa draft sau publish | HTML không đổi (kiểm tay + test không import draft) | T10 |
| R12 | Rename shop → ETag đổi | 200 mới, hiện tên mới | T3 (MSW) |

## 8. Sơ đồ file

```
apps/web/
  package.json                       sửa: + @vsite/builder-components, @vsite/builder-renderer (workspace:*)
  .env.example                       mới: API_ORIGIN, ROOT_DOMAIN
  src/server/site-host.ts            mới (T2) + .test.ts
  src/server/lru.ts                  mới (T3) + .test.ts
  src/server/site-client.ts          mới (T3) + .test.ts   (server-only)
  src/server/load-site.ts            mới (T4) createServerFn
  src/routes/index.tsx               sửa (T4): host shop → site, host platform → trang hiện tại
  src/routes/$.tsx                   mới (T4/T5): splat → trang site
  src/routes/robots[.]txt.ts         mới (T9)
  src/routes/sitemap[.]xml.ts        mới (T9)
  src/components/site-view.tsx       mới (T5)
  src/components/site-head.ts        mới (T7)
  src/components/{not-found,suspended,error}-page.tsx   mới (T8)
packages/builder-renderer/src/anchor.tsx   mới (T6) + export ở index.ts + test
Docs/tasks/WEBSITE-005/{changelog.md}      T11
DesignIdeal/00-INDEX.md                    T11 (§3 dòng 8c, §4 nợ a/c)
```

## 9. Task chi tiết

> Mọi `Test:` viết **trước** (RED) rồi hiện thực (GREEN). Lệnh: `pnpm --filter @vsite/web test`, `typecheck`, `lint`; build: `pnpm --filter @vsite/web build`.
> Vitest của `apps/web` mặc định `jsdom` ([vite.config.ts:38](../../../apps/web/vite.config.ts#L38)) — test chạy server (http server, axios adapter `http`) đặt `// @vitest-environment node` ở đầu file. `@vsite/web:test` đang in `ReferenceError: module is not defined` nhưng exit 0 — có từ trước (WEBSITE-004 changelog), không phải lỗi của task này; đừng sửa lan.

### S0 — Spike: Host có sống tới BE không (làm trước, ném bỏ phần code)
- [ ] Dựng echo server Node cổng tạm in `req.headers.host`; `API_ORIGIN` trỏ vào nó.
- [ ] Gọi từ **trong bundle** `apps/web` (server fn tạm) bằng `createApiClient({headers:{Host:'spa-abc.vsite.local:3000'}})`; chạy cả `pnpm dev` và `pnpm build && node .output/server/index.mjs`. Xác nhận echo nhận đúng Host.
- [ ] Xác nhận Nitro bundle chọn axios build Node (adapter `http`), không phải build browser/`fetch`. Nếu không → thử `resolve.conditions`/`ssr.noExternal`/đánh dấu external.
- [ ] Xác nhận `getRequestHost({xForwardedHost:false})` bỏ qua `X-Forwarded-Host`; `setResponseStatus`/`setResponseHeader` dùng được trong server fn; **server route** (`robots.txt`) viết kiểu nào ở phiên bản `1.168.49`.
- [ ] Gọi BE thật (`dotnet run` + hosts `spa-abc.vsite.local`) → 200/404 đúng; `If-None-Match` → 304.
- [ ] **Tiêu chí dừng:** không gửi được Host tự đặt bằng bất kỳ cách nào (axios, `undici.request`, `node:http`) → **DỪNG, báo người**, không làm tạm. Kết quả S0 ghi vào đầu `changelog.md` mục "Spike".

### T1 — Barrel SDK + dependency + env
- [ ] **Barrel SDK (§4):** thêm vào [index.ts](../../../packages/api-sdk/src/index.ts) `export * from './generated/website/site/site'` và các model `publishedSiteDto`, `publishedSiteDtoPublication`, `publishedPublicationDto`, `publishedShopDto`, `publicationDto`, `publishWebsiteBody`, `publishWebsiteRequest` theo kiểu từng file như dòng 44-61 (không `model/index` — tránh TS2308 với `ProblemDetails`); thêm `export * from './generated/website/site/site.msw'` vào [mocks.ts](../../../packages/api-sdk/src/mocks.ts). Kiểm `pnpm --filter @vsite/api-sdk typecheck` + `pnpm --filter @vsite/portal typecheck`. Commit riêng `fix(api-sdk): export site + publication của WEBSITE-004 ra barrel (WEBSITE-005)`.
- [ ] Thêm dep workspace; `pnpm install`; ESLint không báo (đã chặn `builder-core`).
- [ ] `.env.example`: `API_ORIGIN=http://localhost:5270`, `ROOT_DOMAIN=vsite.local`. Đọc bằng `process.env` trong module `src/server/` (không `VITE_`). Thiếu biến → ném lỗi rõ khi khởi động, không mặc định ngầm ở prod.
- [ ] Commit `chore(web): dependency + env cho Shop Site (WEBSITE-005)`.

### T2 — `resolveSiteHost`
- [ ] Test: bảng R1 (kèm hoa/thường, `:3000`, dấu chấm cuối). Kết quả `{kind:'shop', host, slug}` | `{kind:'platform'}`.
- [ ] Hiện thực thuần, dùng `reservedRoutes.reservedSubdomains`; `host` chuẩn hoá lowercase bỏ cổng và dấu chấm cuối.
- [ ] Commit `feat(web): nhận diện host Shop Site, chặn host lạ ở FE (M4, WEBSITE-005)`.

### T3 — `site-client`
- [ ] Test (Node http server hoặc MSW): Host đúng; **không** Authorization/Cookie (R2); `If-None-Match` gửi khi có LRU; 304 → dữ liệu LRU; 200 → thay LRU; Suspended/404 → xoá LRU (R4); parse Zod hỏng → ném; 5xx/mạng → ném lỗi kiểu `SiteFetchError`; hai host tách key (R3); đổi tên shop đổi ETag (R12).
- [ ] Test thêm: `schemaVersion` khác 1 → lỗi rõ (Phát hiện 5); `Host` gửi đi = host **đã chuẩn hoá** (A11).
- [ ] `lru.ts` (200 mục, thuần). `site-client.ts`: `client.get<PublishedSiteDto>('/api/site', {validateStatus: s => s===200||s===304||s===404})`, đọc `response.headers.etag`. Vỏ response parse bằng Zod sinh từ contract `getPublishedSiteResponse`, `snapshot` parse bằng `siteSnapshotSchema`. Kết quả `SiteResult = notFound | suspended{shop} | site{shop, snapshot, etag, publishedAt}`.
- [ ] Commit `feat(web): site-client — Host tự đặt, ETag/304, LRU, Zod (#108, #109, WEBSITE-005)`.

### T4 — Loader + route
- [ ] Test: server fn trả đúng union; host ngoài mẫu → 0 request BE (R1); `setResponseStatus` 404/503/502; header `Cache-Control: private, no-cache` trên HTML.
- [ ] Tách lõi thuần `loadSiteForHost(rawHost, path, deps)` (test được không cần Start) khỏi vỏ `createServerFn` mỏng; vỏ chỉ đọc `getRequestHost({xForwardedHost:false})`, validate input `{ path: string }` (R13), gọi `setResponseStatus`/`setResponseHeader`.
- [ ] `load-site.ts` (`createServerFn`), `year` tính ở đây một lần. `index.tsx`/`$.tsx` gọi loader; host platform giữ trang hiện tại.
- [ ] Commit `feat(web): loader + route Shop Site theo host (#104, WEBSITE-005)`.

### T5 — `SiteView`
- [ ] Test: chọn trang theo slug (A12), trang chủ vắng → NotFound (R7), slug reserved (R6); SSR snapshot bằng fixture snapshot dựng qua `siteSnapshotSchema.parse` (mẫu: [home.tree.json](../../../packages/builder-renderer/tests/fixtures/home.tree.json), [site-app.tsx](../../../packages/builder-renderer/tests/helpers/site-app.tsx)).
- [ ] Ghép theo mẫu Portal: `<div style={themeStyle(tokens)}><SiteLayout shop homeHref="/" year>` + `RenderContextProvider value={{mediaMap: snapshot.media}}` + `RenderTree basePath="" locale=defaultLocale defaultLocale pages=refs`.
- [ ] Commit `feat(web): SiteView render snapshot bằng builder-renderer (#103, WEBSITE-005)`.

### T6 — Anchor
- [ ] Test (renderer): wrapper có `id`; hook cuộn khi hash đổi/khi mở URL có hash (mock `scrollIntoView`); node test không đụng `window` khi render.
- [ ] `anchor.tsx`: `decorateWithAnchor`, `useAnchorScroll`. Dùng ở `SiteView`.
- [ ] Commit `feat(renderer): anchor #nodeId cuộn được trên site thật (G3, WEBSITE-005)`.

### T7 — `<head>`
- [ ] Test: title/description fallback (page → snapshot → shop), `og:image` từ `media[id].storageKey` thành URL tuyệt đối `/media/…` (R10), favicon, canonical = origin hiện tại (A3), `noindex` (R9), `html lang`.
- [ ] Commit `feat(web): SEO head cho Shop Site (G9, WEBSITE-005)`.

### T8 — Trang hệ thống
- [ ] Test: Suspended (tên + logo, không nội dung site, noindex, 503); 404 chung; lỗi chung (không rò chi tiết). Theme mặc định (`defaultThemeVars`). Đủ loading/error/empty theo quy ước `apps/web`.
- [ ] Commit `feat(web): trang NotFound/Suspended/lỗi cho Shop Site (WEBSITE-005)`.

### T9 — `robots.txt` / `sitemap.xml`
- [ ] Test: robots `Allow: /` + `Sitemap:`; host không phải shop → 404; sitemap chỉ trang bật, `slug !== null`, `noIndex=false`; trang chủ `/`; `lastmod = publishedAt` (A7); Suspended/404 → 404.
- [ ] Commit `feat(web): robots.txt + sitemap.xml theo host (#10, WEBSITE-005)`.

### T10 — Hydrate + nghiệm thu
- [ ] Test jsdom: `renderToString` loader data → `hydrateRoot` → `console.error` không được gọi (R8).
- [ ] Nghiệm thu tay (ghi `changelog.md`): hosts `spa-abc.vsite.local`; Flow 2 bằng `curl` (login Owner → publish → xem → sửa draft → xem → rollback → xem); view-source thấy HTML; DevTools không cảnh báo hydrate; `GET /media/…` mở thẳng trên bản `node .output` (nợ b).
- [ ] Chạy: `pnpm build && pnpm test`, `pnpm check:docs`.

### T11 — Tài liệu (không có bước này thì task CHƯA XONG)
- [ ] `Docs/tasks/WEBSITE-005/changelog.md`: Spike · lệch có chủ đích · chưa làm xong (WEBSITE-006, nợ a/b/c, M4 phía BE, Portal đổi dùng `decorateWithAnchor`).
- [ ] `DesignIdeal/00-INDEX.md`: §3 dòng 8c, §4 nợ `shop_slug_history` + CSRF (⛔ trước production).
- [ ] `apps/web/CLAUDE.md`: mục SSR ghi cách forward Host đã kiểm; `builder-renderer/CLAUDE.md` thêm anchor.
- [ ] `change-reviewer` (foreground, **model mạnh nhất**: chạm tenant) → `review.md`. Sau đó **dừng cho mốc review #96–#98** (`step.md`).

## 10. Giả định tôi đã tự đặt

| # | Giả định | Vì sao / cách đảo |
|---|---|---|
| A1 | Host shop = **đúng một nhãn** dưới `ROOT_DOMAIN` (env), không reserved; mọi host khác (kể cả custom domain) coi là platform/404 và **không gọi BE**. | Vá M4 ở FE mà không đụng BE; custom domain (G2) cần `ShopDomain` → ngoài phạm vi. Lỗ M4 ở BE vẫn còn. |
| A2 | `API_ORIGIN` env server-only, không có giá trị mặc định ở prod. | Tránh gọi nhầm BE. |
| A3 | Canonical = origin đang truy cập (protocol từ request URL). | Chưa có `ShopDomain.primary` (#10). |
| A4 | Wrapper ETag nằm trong `apps/web`, **không** sửa `api-sdk` mutator (chỉ barrel, §4). | Mutator dùng chung Portal; đổi ở SDK nếu sau này cần dùng chung. |
| A5 | Suspended → HTTP **503** + noindex; NotFound → 404; lỗi BE → 502. | SEO không index trang tạm ngưng. Chưa hỏi người. |
| A6 | HTML: `Cache-Control: private, no-cache`. | #109 "không cache HTML ở CDN". |
| A7 | `lastmod` sitemap = `publication.publishedAt` cho mọi URL. | Snapshot không có mốc theo trang. |
| A8 | Sửa `builder-renderer` (anchor) trong task này; Portal giữ `decorateWithId` riêng. | G3 giao 8c; hạn chế chạm Portal. |
| A9 | LRU 200 host, không TTL (luôn hỏi lại BE bằng `If-None-Match`). | BE `no-cache` + 304 rẻ. Snapshot lớn (vài trăm KB) × 200 có thể tới hàng chục MB — đo ở T10, ghi changelog; giảm trần nếu cần. |
| A10 | Khớp slug: bỏ `/` cuối, decodeURI, so khớp **chính xác** (không hạ chữ hoa, không 301). | Slug do BE chuẩn hoá thường; redirect là việc của slug history. |
| A11 | Forward `Host` = host **đã chuẩn hoá** (lowercase, bỏ dấu chấm cuối, bỏ cổng) — cùng giá trị làm key LRU. | BE chỉ đọc `Request.Host.Host` (không cổng); một giá trị duy nhất tránh lệch key cache ↔ shop. (Sửa khi rà plan 2026-11-18.) |
| A12 | Fixture snapshot dựng bằng tay qua `siteSnapshotSchema.parse` (không có fixture snapshot sẵn). | Chưa tìm thấy; nếu có fixture BE thì dùng. |
