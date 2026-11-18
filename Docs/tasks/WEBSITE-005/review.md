# Review — WEBSITE-005

## Tự động (reviewer chạy lại, 2026-11-19, @ `16050df`)
- `pnpm --filter @vsite/web test`: 17 file, 264 test, pass. `typecheck`, `lint` sạch.
- `pnpm --filter @vsite/builder-renderer test`: 7 file, 36 test, pass. `typecheck` sạch.
- `pnpm check:docs`: OK (20 cảnh báo cũ, không chặn). 00-INDEX §2 khớp banner.
- `apps/web/.output/public`: không chứa `API_ORIGIN`, `follow-redirects`, `siteCache`.
- Controller chạy lại ở `16050df`: `pnpm build` exit 0, `pnpm test` exit 0 (mọi package xanh), typecheck + lint `@vsite/web`, `@vsite/builder-renderer` sạch.

## change-reviewer (2026-11-19, model mạnh nhất, toàn diff `master...HEAD`, 17 commit `7b3bf71..16050df`)

**Kết luận Gate 2 lần 1: CẦN SỬA (nhẹ).** Tenant, bảo mật, CSRF, ranh giới đạt; không có lỗi bảo mật. 1 Critical (tài liệu: banner `05`) + 2 Important (timeout gọi BE; chốt "cấm đọc draft" không tồn tại). Kết quả sửa: xem mục "Sau sửa" cuối file.

### Đã kiểm, đạt
1. **Tenant (#21, #108)**
   - `loadSite` chỉ nhận `{ path }` — `apps/web/src/server/load-site-input.ts:11-16` bỏ mọi khoá khác (R13).
   - Host: `load-site.ts:20` `getRequestHost({ xForwardedHost: false })`; protocol `load-site.ts:21` `xForwardedProto: false`. robots/sitemap đọc giống hệt (`seo-route.ts:11-12`).
   - `resolveSiteHost` (`site-host.ts:34-46`) chỉ cho qua `{label}.{ROOT}` một nhãn, không reserved, không IPv6, cổng chỉ chữ số. Host khác → platform, không gọi BE (`load-site-core.ts:99-103`, `site-seo-files.ts:94-96`).
   - Path reserved chặn trước `fetchSite` (`load-site-core.ts:107`).
   - Key LRU = `Host` gửi BE = host đã chuẩn hoá; `If-None-Match` theo cùng key (`site-client.ts:118-127`, A11). Test R3 có.
   - `createApiClient` không kèm `getAccessToken`; `site-client` không chạm request gốc → Cookie/Authorization không thể bị forward (R2).
   - Không `ShopId` trong input. Loader data Suspended chỉ có `shop {name, logoUrl}`.
2. **`src/start.ts`**
   - `createCsrfMiddleware({ filter: ctx => ctx.handlerType === 'serverFn' })` giống hệt `defaultCsrfMiddleware` (`start-server-core/dist/esm/createStartHandler.js:21`); Start chỉ dùng mặc định khi `!hasStartInstance` (`:288`) → gắn lại là bắt buộc, đã làm, đứng đầu chuỗi, có test `start.test.ts`.
   - `applyEventResponse` đọc status/header qua event h3 từng request (AsyncLocalStorage) → không rò giữa request; chỉ ghi đè khi router trả 200; bỏ qua `set-cookie`, `content-type`, `content-length`; giữ `response.body` (stream alias `isResponseAlias`, `:110`); RPC `/_serverFn` không có `Response` trả nguyên (có test).
3. **Không đọc draft, không rò lỗi** — `apps/web` chỉ import `createApiClient`, `getPublishedSiteResponse`, type `PublishedShopDto`. `SiteFetchError`/`SiteDataError` chỉ log ở server; loader data `{kind:'error'}` không có trường khác; `RouteErrorPage` bỏ qua `error`; 404 map theo `error_code === 'SITE_NOT_FOUND'`, 404 lạ → 502 (B7).
4. **#23, #19, ranh giới** — không `window`/`document`/`Date` trong render (`new Date()` chỉ ở deps server fn `load-site.ts:34`; `anchor.tsx` chỉ trong effect). Không TS `enum`. `anchor.tsx` chỉ import `react` + `./renderTree`. `apps/web` không import `builder-core`. Không sửa `generated/**` — chỉ barrel `index.ts` + `mocks.ts` (ngoại lệ plan §4).
5. **Khớp contract (WEBSITE-004 brief A, D)** — ETag/304 từ `response.headers`, 304 trả LRU; Suspended/404 xoá LRU; snapshot parse `siteSnapshotSchema` + `schemaVersion === 1`; `slug ""` trang chủ, `null` NotFound; trang chủ vắng → 404 (G10); OG/favicon dùng file gốc (G9).
6. **B3 (override Zod `publishedAt`): đúng chỗ, không phải workaround contract.** Contract `date-time` RFC 3339 cho phép offset; lỗi ở cấu hình Orval (`z.string().datetime({})` mặc định `offset: false`). Override `.extend` cục bộ một field, có comment cách gỡ, ghi theo dõi ở 00-INDEX §4. Không ghi `contracts/`/`generated/`.

### Critical
**C1 — Banner STATUS `DesignIdeal/05-website-builder-and-product-design.md:3` chưa cập nhật sau 8c.** Vẫn ghi "render site công khai ở apps/web (8c)" là chưa code và "anchor #nodeId và năm © chuyển 8c"; `Tasks:` thiếu `WEBSITE-005`; `Changelog:` vẫn trỏ WEBSITE-004. Dòng 05 ở `DesignIdeal/00-INDEX.md:51` (§2) sai y hệt nên `check:docs` vẫn pass. §7 dòng 281 ("`apps/web` chỉ đọc `site_publication` (qua cache)") nay đã code: LRU trong process theo ETag, HTML `private, no-cache`. Sửa theo "thay chứ không chồng".

### Important
**I1 — Gọi BE không có timeout** (`apps/web/src/server/site-client.ts:120-127`). `axios.create` không `timeout` → vô hạn. BE treo (M3/M6) thì SSR + robots/sitemap treo theo, không ra nhánh 502 (R5). Đề xuất `timeout` (vd 5000 ms) → `SiteFetchError` → 502, có test; cân nhắc `maxRedirects: 0`, `proxy: false` (m3).

**I2 — Thiếu chốt "không đọc draft" dù plan và tài liệu nói là có.** Plan §6 (`plan.md:118`) yêu cầu "test cấm import hook draft/page của SDK"; `apps/web/CLAUDE.md:43` khẳng định có. Thực tế không có test/lint nào (`eslint.config.mjs` chỉ chặn `builder-core`). Code hiện không import draft → thiếu bảo vệ (#17), không phải lỗi đang xảy ra. Sửa: `no-restricted-imports` cho `apps/web/**` hoặc test quét import; nếu không thì sửa CLAUDE.md cho đúng.

### Minor
- **m1** — `apps/web/CLAUDE.md:104-105`: "Ngoại lệ đã làm" thiếu `packages/api-sdk/src/mocks.ts`; dòng "❌ Cấm ghi `packages/api-sdk/**`" mâu thuẫn với ngoại lệ ngay trên; `:43` ghi "chỉ import `getPublishedSite`" trong khi code import `getPublishedSiteResponse` + `createApiClient`.
- **m2** — Status khác 404 qua RPC khi điều hướng client (503, 502) chưa kiểm; với RPC không ok h3 bỏ header event (S0 §3b) → `Cache-Control: private, no-cache` có thể vắng trên `GET /_serverFn/…` 404/503/502. Nên curl kiểm, ghi changelog; thiếu thì ghi nợ Caddy/CDN.
- **m3** — axios ở Node đọc `HTTP_PROXY`/`HTTPS_PROXY` và theo redirect (tối đa 21). Đề xuất `proxy: false, maxRedirects: 0` (`site-client.ts:124`).
- **m4** — 404 không cache ở FE: flood `{rand}.{ROOT}` → 1:1 request tới BE (BE có negative cache 1 phút). Chấp nhận trước production; ghi theo dõi rate-limit ở Caddy.
- **m5** — `site-head.ts:31` `encodeURI(data.path)` trên path router đã decode trừ `%25`/`%5C` → `%25` thành `%2525` trong canonical. Slug BE ASCII nên gần như không xảy ra. Ghi nhận.
- **m6** — `.env.example` có `API_ORIGIN`/`ROOT_DOMAIN` nhưng chưa ghi dev có tự nạp `.env` không (Vite chỉ nạp `VITE_*`). Ghi cách chạy dev vào `apps/web/CLAUDE.md`.

### Tài liệu
- Còn thiếu: banner `05` + dòng §2 00-INDEX (C1); `apps/web/CLAUDE.md:43` (I2) và Phạm vi ghi (m1).
- Đã đủ: changelog, 00-INDEX §3 (8c) và §4 (hai dòng WEBSITE-005, cập nhật WEBSITE-004/REFACTOR-API-001/REFACTOR-DB-001/MEDIA-001-ACCEPT), `builder-renderer/CLAUDE.md`.
- Không có quyết định Gate 1 mới, không cấp `#N` → `DECISIONS.md` không đổi. `DOCKER-TEST-DEBT.md` không liên quan (FE-only).

## Điểm cần người quyết
- **B6:** năm © tính theo UTC hay `Asia/Ho_Chi_Minh`.
- **A5:** 503 cho Suspended (đã duyệt cùng plan, Q4) — nhắc lại vì ảnh hưởng SEO nếu shop bị đình chỉ lâu.

## Ma sát quy trình
- Plan §6 ghi ràng buộc ("test cấm import draft") nhưng §9 không có checkbox thực hiện → rơi mất, CLAUDE.md lại chép như đã có. Ràng buộc §6 cần kiểm tự động nên có checkbox `Test:` ở §9.
- `check:docs` chỉ so banner với 00-INDEX §2, không biết dòng §3 vừa ✅ → banner `05` ghi "8c chưa code" vẫn pass. Gợi ý: dòng §3 ✅ kèm mã task thì cảnh báo nếu banner file DesignIdeal liên quan không nhắc mã đó.
- Hai bẫy framework (`setResponseStatus` không tới HTML; khai `requestMiddleware` mất CSRF mặc định) chỉ lộ qua spike + đọc `node_modules`. Đã ghi `apps/web/CLAUDE.md`. Giữ quy ước "S0 spike có quyền dừng plan" cho task chạm framework lần đầu.
- Không xác định được `.output` dựng từ commit nào. Ghi commit vào changelog mỗi lần đo trên build, hoặc cho script build in hash.
- Changelog ~330 dòng; "Lệch có chủ đích" và "Giả định" (B1–B13) chồng nhau (B1/lệch 1, B3/lệch 4, B5/lệch 5) → rà phải đọc hai lần.
- (Controller) Một implementer chạy `git stash` + `git stash pop` khi không có gì để stash → áp nhầm stash cũ `wip-docs-13files`, xung đột 3 file tài liệu; đã khôi phục về HEAD, stash còn nguyên. Đã thêm luật "không dùng `git stash`" vào brief chung cho subagent.
- (Controller) Một subagent bị từ chối lệnh kill tiến trình thử nghiệm → để lại server `node` trên cổng 3011–3013, 4597–4599; controller không tự kill thay, báo người.

## Sau sửa (Gate 2 lần 2)

Sửa ở `b67e293` (code + eslint) và `c4b5dfd` (docs); re-review có phạm vi `16050df..c4b5dfd` bằng `change-reviewer` (model mạnh nhất), 2026-11-19.

**Reviewer tự chạy lại:** `@vsite/web` test 17 file / 268 pass (thêm 4), lint exit 0. Thử luật lint qua `eslint --stdin`: chặn `getPageDraft`, `import * as sdk from '@vsite/api-sdk'`, `getWebsiteMock`, `@vsite/builder-core`; cho qua `createApiClient`, `getSiteMock`. `git diff master...HEAD -- packages/builder-components` rỗng → lỗi `pnpm lint` root ở `meta/envelope-schema.ts:231-232` có từ trước (commit `3b9f7c7`, WEBSITE-004), không do task này.

| Finding | Kết quả |
|---|---|
| C1 | ✅ Banner `DesignIdeal/05:3` (Tasks + WEBSITE-005, Changelog trỏ WEBSITE-005, "chưa code" bỏ 8c, anchor/năm © "đã code ở WEBSITE-005" — thay, không chồng); `05:281` viết đè khớp code; `00-INDEX.md:51` (§2) khớp banner |
| I1 + m3 | ✅ `site-client.ts:68` `SITE_FETCH_TIMEOUT_MS = 5000`, `:130-133` `timeout`, `maxRedirects: 0`, `proxy: false`; lỗi → `SiteFetchError` → 502. Test thật (`site-client.test.ts:288-339`): BE treo, 302 không bị theo, `HTTP_PROXY` bị bỏ qua |
| I2 | ✅ `eslint.config.mjs:113-132` block riêng `apps/web/**` cấm theo tên export draft/editing của `@vsite/api-sdk` và mock MSW, giữ luật cấm `builder-core`/`apps`; `apps/web/CLAUDE.md:43-44` mô tả đúng chốt |
| m1 | ✅ `apps/web/CLAUDE.md:106-107` hết mâu thuẫn; `:43` ghi đúng import thật |
| m2 | ✅ Đã kiểm: RPC `/_serverFn` 404/503/502 đều có `Cache-Control: private, no-cache`, body 502 chỉ `{kind:'error'}`, thiếu header CSRF → 403 (đo trên build `b67e293`, `changelog.md:52`) — không phát sinh nợ |
| m4, m5 | ✅ Ghi theo dõi (`changelog.md:43-44`) |
| m6 | ✅ `apps/web/CLAUDE.md:46`: dev tự nạp `apps/web/.env`; bản build truyền env qua shell |

**Hồi quy tenant/CSRF:** không có (diff code chỉ thêm 3 option axios + test).

**Phát hiện mới:**
- N1 (Minor) — `createPageBodyIsEnabledDefault` thiếu trong danh sách lint → **đã sửa** (controller, commit sau cùng).
- N2 (Minor) — `DesignIdeal/05:279` còn ghi "`apps/web` đọc qua upcaster" trong khi code chỉ nhận `schemaVersion` 1 → **đã sửa** câu cho đúng sự thật.
- S1 (gợi ý) — luật lint chặn theo tên, không ngăn gọi URL draft thô qua `createApiClient(...).get(...)`; phần bù: client không token, BE đòi auth. Ghi nhận.
- S2 (gợi ý) — danh sách tên trong eslint giữ tay khi Orval sinh endpoint mới; đề xuất test so `Object.keys` module generated website với danh sách + whitelist (#17). Theo dõi.

**Kết luận Gate 2 lần 2: SẴN SÀNG.** Chờ người duyệt; sau đó dừng cho mốc review #96–#98 (`step.md`).
