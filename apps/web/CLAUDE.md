# apps/web — vsite

> **Cần nền thiết kế?** Vào `DesignIdeal/00-INDEX.md` trước (§2 nói file nào còn tin được), đừng mở
> thẳng file `0*.md`. Quyết định `#N` → `DesignIdeal/DECISIONS.md`.
> Phạm vi này hay cần: `01` §5.1 (tìm kiếm & khám phá) · `04` §5 (trang hồ sơ shop), §6 (`Review`) ·
> `05` §6 (Component Tree) cho Shop Site. Task chỉ sửa UI/CSS thì **không cần đọc gì**.

**SSR** bằng TanStack Start (Quyết định #22, #23). Gồm ba loại trang, đều render trong app này:

| | URL | Render bởi | Hiển thị `Review` |
|---|---|---|---|
| **Main** | `vsite.vn` (tìm kiếm, bản đồ, landing SEO) | code thường | — |
| **Shop Profile** | `vsite.vn/shop/{slug}` | vsite, mẫu thống nhất | ✅ |
| **Shop Site** | `{slug}.vsite.vn`, `spa-abc.com` | `builder-renderer` (Component Tree) | ❌ tuyệt đối không |

⚠️ **Shop Profile ≠ Shop Site.** Đây là chỗ dễ nhầm nhất — thấy "trang của shop" là dễ với tay sang `builder-renderer`. Shop Profile **không** dùng `builder-renderer`, nó là code thường như Main.

## SEO (Quyết định #10)

- Mỗi shop chỉ có **một** primary domain (bảng `ShopDomain`). Domain còn lại phải **301 redirect** về primary.
- `<link rel="canonical">` luôn trỏ về primary domain.
- `robots.txt` + `sitemap.xml` sinh riêng theo tenant, chỉ chứa primary URL.

## Tenant resolution (Quyết định #7, #104)

Tenant resolve **chỉ theo Host**, theo thứ tự: custom domain → subdomain `{slug}.vsite.vn` → platform route. Không có chế độ path `vsite.vn/{slug}` — đừng đọc slug shop từ path. Không tự đoán tenant bằng cách khác.

## SSR

`apps/web` **phải** SSR — Main và Shop đều cần SEO.

- **Lock version chính xác** của TanStack Start trong `package.json`. Không dùng `^` hay `~` (Quyết định #23 — Start chưa 1.0, API còn đổi).
- Code chạy ở cả server lẫn client. Không đụng `window`/`document` trong logic render chính; chỉ dùng trong effect chạy sau hydrate.
- Data fetching qua loader của TanStack Start/Router, không fetch trong `useEffect` cho nội dung cần index.

### Shop Site: chọn shop theo Host (WEBSITE-005, #104, #108)

Đường đi: `routes/index.tsx` và `routes/$.tsx` → server fn `server/load-site.ts` → `load-site-core.ts` → `site-client.ts`.

- **Host chỉ lấy trong server fn** bằng `getRequestHost({ xForwardedHost: false })`, rồi chuẩn hoá (lowercase, bỏ một dấu chấm cuối, bỏ cổng). `loadSite` chỉ nhận `path` làm input; server fn là endpoint RPC gọi được từ ngoài, nhận host qua input là mở cửa đọc shop bất kỳ. Không `X-Forwarded-Host`/`Forwarded`, không query/cookie.
- **Chỉ host dạng `{label}.{ROOT_DOMAIN}`** (một nhãn, không reserved) mới gọi BE (`server/site-host.ts`); host khác (IP, IPv6, `a.b.`, custom domain) là platform/404, **0 request** tới BE. BE vẫn suy tenant theo nhãn đầu của Host (M4) nên FE là lớp chặn duy nhất.
- **Forward Host bằng axios**: `createApiClient({ baseURL: API_ORIGIN, headers: { Host } })` cho từng request, `Host` = đúng giá trị đã chuẩn hoá (cũng là key LRU). `fetch`/undici **bỏ** header `Host` tự đặt, đừng đổi sang chúng. Không forward `Cookie`/`Authorization` của khách. ETag/304 và LRU ở `site-client.ts`; module có `API_ORIGIN`/LRU import `@tanstack/react-start/server-only`.
- **Không đọc draft** (`05` §7): từ `@vsite/api-sdk` chỉ import `createApiClient`, `getPublishedSiteResponse` (Zod) và type `PublishedShopDto`/`PublishedSite*`. Lint `no-restricted-imports` ở `eslint.config.mjs` (block `apps/web/**`) chặn theo tên mọi hàm/hook/query-key/mutation-options/schema Zod/mock MSW draft của module website (`getWebsite`, `listPages`, `getPageDraft`, `savePageDraft`, `updateTheme`, `publishWebsite`, `rollbackPublication`, `use*`…) và import đi tắt vào `api-sdk/src/**`. Orval sinh endpoint website mới → thêm tên vào `WEBSITE_DRAFT_SDK_NAMES`.
- **Gọi BE có giới hạn**: `GET /api/site` timeout `SITE_FETCH_TIMEOUT_MS` (5 s), `maxRedirects: 0`, `proxy: false` (bỏ `HTTP(S)_PROXY` của env); timeout/3xx → `SiteFetchError` → 502.
- **Chạy dev/build**: `pnpm --filter @vsite/web dev` tự nạp `apps/web/.env` vào `process.env` (đã kiểm: `API_ORIGIN`/`ROOT_DOMAIN` từ `.env` tới được server fn) — chép từ `.env.example`. Bản build `node .output/server/index.mjs` thì truyền qua env shell: `PORT=3000 API_ORIGIN=http://localhost:5270 ROOT_DOMAIN=vsite.local node .output/server/index.mjs`.
- **Reserved path trên host shop**: đoạn đầu `media|api|assets|static` (từ `config/reserved-routes.json`, không phân biệt hoa/thường) → 404 chung, không gọi BE. `node .output` **không proxy** `/media` và `/api`: production phải route chúng sang BE ở Caddy.
- **`src/start.ts`**: request middleware đưa status (503/502) và `Cache-Control` do server fn đặt vào HTML (router chỉ tự đặt 200/404/500; h3 bỏ header event ở response không ok). Khai `requestMiddleware` làm Start **bỏ** CSRF middleware mặc định, nên `csrfMiddleware` được thêm lại đứng đầu chuỗi: đừng xoá nó. Nợ ⛔ trước production: cookie `SameSite=Lax` chưa xử lý.
- **Server route cho `.txt`/`.xml`**: tên file escape dấu chấm, `routes/robots[.]txt.ts`, `routes/sitemap[.]xml.ts`; handler `server: { handlers: { GET } }`.
- Năm © và mọi giá trị theo giờ đến từ loader data, không `new Date()` lúc render (#23).

---

## Reserved routes

Import trực tiếp từ `config/reserved-routes.json` (Vite hỗ trợ import JSON native). **Không viết tay danh sách thứ hai** — lệch với BE là lỗi routing/bảo mật thật (Quyết định #24).

Tenant resolution theo host đọc từ chính file đó.

---

## API

```
contracts/openapi/*.json  ──pnpm gen:api (Orval)──→  packages/api-sdk/
```

- **Không sửa tay bất cứ file nào trong `packages/api-sdk/`.** Có header `// GENERATED — DO NOT EDIT`.
- Contract sai hoặc thiếu → **DỪNG và báo**. Không tự sửa contract, không workaround.
- Code với **MSW mock trước**, chỉ bật API thật ở bước integration. Mock sinh từ chính contract nên nếu UI chạy với mock thì chắc chắn không bám vào hành vi ngoài contract.
- Dùng **API model** từ `api-sdk`, không tự định nghĩa lại type để "mirror" entity của BE.
- ⚠️ **Gọi API trong SSR loader: luôn `createApiClient({ baseURL, headers, getAccessToken })` cho TỪNG
  request** rồi truyền vào hàm sinh ra (`getShop(id, { client })`). **Không** dùng instance mặc định
  của `@vsite/api-sdk` ở server — state cấp module dùng chung mọi request của process, token người
  này lọt sang người khác. `baseURL` phải tuyệt đối (server không có relative URL) và phải forward Host
  gốc của trình duyệt (BE resolve tenant theo Host, #7). Từ trình duyệt (sau hydrate) gọi qua `/api`
  như Portal.

---

## State (Quyết định #20)

| Loại | Sở hữu bởi |
|---|---|
| Server state (shop, listing, review, search result) | **TanStack Query** |
| Client / UI state (modal, filter chưa submit, bước wizard) | **Zustand** |

**KHÔNG copy dữ liệu từ Query vào Zustand.** Thấy cần làm vậy nghĩa là thiết kế đang sai.

---

## Quy ước

- TypeScript **strict**. Enum dùng `const object + union type`, **không** dùng `enum` của TS (#19).
- Validation dùng Zod schema **sinh từ contract**, không viết tay lại.
- Form: React Hook Form + Zod resolver.
- i18n: `react-i18next`. Error message map từ `error_code` trong ProblemDetails, không parse text.
- Mọi màn hình có dữ liệu: **loading · error · empty** đủ cả ba. Không bỏ empty.

---

## Phạm vi ghi

| | |
|---|---|
| ✅ Được ghi | `apps/web/**` · `packages/ui` · `packages/shared` · `packages/theme-engine` |
| ⚠️ Chỉ khi plan duyệt | `packages/builder-renderer` · barrel `packages/api-sdk/src/index.ts` + `src/mocks.ts` (chỉ thêm export) — WEBSITE-005 đã duyệt và làm cả hai (anchor; export site) |
| ❌ Cấm ghi | `packages/api-sdk/**` trừ hai file barrel trên · `backend/**` · `contracts/**` · `config/**` · `DesignIdeal/**` |

---

## Thứ tự dựng (#96)

Shop Site được code **trước** (Bước 8c ở `DesignIdeal/step.md`), Main và Shop Profile code sau (M3–M4) — dù về đợt ra mắt thì Main/Profile là Phase 1. Đừng scaffold sẵn route của bước chưa tới.