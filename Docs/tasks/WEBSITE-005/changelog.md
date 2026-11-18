# WEBSITE-005 — Changelog (Bước 8c: `apps/web` render Shop Site công khai)

> **Trạng thái:** code xong T1–T10 + wave sửa review, nghiệm thu tay ĐẠT (7/7 bước). Chờ `review.md` (change-reviewer) rồi Gate 2, sau đó **mốc review #96–#98** (`step.md`). Còn nợ ⛔ trước production: xem "Chưa làm xong / nợ".

## Spike (S0)

> Ngày 2026-11-18, nhánh `feature/WEBSITE-005`. Code spike là code nháp, **đã xoá hết** (`git status` sạch dưới `apps/`, `packages/`; `routeTree.gen.ts` đã khôi phục, `.output` build lại từ code sạch).
> Phiên bản: `@tanstack/react-start` 1.168.49 → `@tanstack/start-server-core` 1.169.31 (h3 `2.0.1-rc.20`), `@tanstack/react-router` 1.170.32, `@tanstack/start-client-core` 1.170.27, `nitro-nightly` 3.0.1-20260904, Vite 8.2.2, axios 1.20.0, Node 24.
> **Kết luận: DONE_WITH_CONCERNS** — Host tự đặt tới được BE (dev lẫn build), BE thật trả 200/404/304 đúng. **Lưu ý:** `setResponseStatus` trong server fn **không** tự đổi status của HTML SSR. Phải thêm request middleware toàn cục (xem 3b). T4 phải tính tới điều này.

### Cách làm
- Echo server Node `127.0.0.1:4599` in `req.headers.host`.
- Route tạm `/spike`: `loader` gọi server fn (`createServerFn({ method: 'GET' })`). Server fn đọc `getRequestHost`, gọi `createApiClient({ baseURL: process.env.API_ORIGIN, headers: { Host } }).get('/api/site')` hai lần (có cổng và không cổng), rồi gọi `setResponseStatus(418)` và `setResponseHeader`.
- Route tạm `/spike404`: server fn gọi `setResponseHeader`, sau đó `loader` ném `notFound()`. Route có thêm option `headers`.
- Route tạm `/spikebe`: chuẩn hoá host (lowercase, bỏ cổng, bỏ dấu chấm cuối) rồi gọi BE thật, sau đó gửi lại `If-None-Match`.
- Server route tạm `src/routes/robots[.]txt.ts`.
- Middleware tạm `src/start.ts` để thử 3b.
- Hai môi trường chạy, cùng một kịch bản curl (`-H "Host: …:3000" -H "X-Forwarded-Host: …"`):
  - **dev:** `API_ORIGIN=http://127.0.0.1:4599 pnpm --filter @vsite/web dev`
  - **build:** `pnpm --filter @vsite/web build` rồi `PORT=3000 HOST=127.0.0.1 API_ORIGIN=… node apps/web/.output/server/index.mjs`

### 1. Host tự đặt có tới server không — ĐẠT (dev và build cho kết quả giống nhau)
- `createApiClient` **đã có trong barrel** (`index.ts:1` export `./mutator/axios-instance`). Import `import { createApiClient } from '@vsite/api-sdk'`, không cần đường dẫn tương đối.
- Echo server nhận đúng cả hai giá trị (`ua=axios/1.20.0`):
  - `host= spa-abc.vsite.local:3000`
  - `host= spa-abc.vsite.local`
- Kết quả này giống nhau ở dev và ở `node .output/server/index.mjs`. Không cần thử `undici.request` hay `node:http`, nên tiêu chí dừng không bị chạm.

### 2. Nitro bundle chọn axios bản Node — ĐẠT, không cần cấu hình thêm
- axios được **bundle vào chunk SSR** của route: `.output/server/_ssr/spike-*.mjs`, khoảng 489 KB, gồm cả barrel SDK và zod.
- Chunk đó có các dấu hiệu của bản Node:
  - `import http from "http"`, `https`, `http2`, `zlib`, `follow-redirects`
  - `isHttpAdapterSupported = … kindOf(process) === "process"`
  - `knownAdapters = { http: http_default, xhr: xhr_default, fetch: … }`
- Lúc chạy, `client.defaults.adapter` là `['xhr','http','fetch']`. Ở Node, axios bỏ qua `xhr` (không có `XMLHttpRequest`) và chọn `http`, khớp với echo nhận Host.
- **Không cần** `resolve.conditions`, `ssr.noExternal` hay `external`. T1 không phải thêm gì vào `vite.config.ts` cho axios.
- Bundle client (`.output/public/**`) không chứa `axios`, `follow-redirects` hay `API_ORIGIN`. Bộ biên dịch server fn đã bỏ phần handler và các import của nó khỏi bản client.
- **Env:** `process.env.API_ORIGIN` **không bị inline** lúc build. Bundle còn nguyên `process.env.API_ORIGIN ?? …`, nên giá trị được đọc lúc runtime (`API_ORIGIN=… node .output/server/index.mjs`). Ở dev, env của shell chạy `pnpm dev` cũng tới được server fn. Không cần prefix `VITE_` (prefix đó lại làm lộ biến ra client).
- Lưu ý kích thước: import barrel `@vsite/api-sdk` kéo theo toàn bộ SDK và schema zod vào chunk SSR. Server chấp nhận được, nhưng nên giữ import này ở module server-only.

### 3. API của Start 1.168.49 — import và hành vi
Import đúng:
```ts
import { createServerFn, createMiddleware, createStart } from '@tanstack/react-start';
import {
  getRequestHost, setResponseStatus, setResponseHeader,
  getResponseStatus, getResponseHeaders,
} from '@tanstack/react-start/server';           // re-export của @tanstack/start-server-core
import { createFileRoute, notFound } from '@tanstack/react-router';
import '@tanstack/react-start/server-only';       // export "./server-only" có trong package.json
```

**3a. `getRequestHost({ xForwardedHost: false })` bỏ qua X-Forwarded-Host — ĐẠT**
- Request gửi `Host: spa-abc.vsite.local:3000` kèm `X-Forwarded-Host: spa-xyz.vsite.local`:
  - `{xForwardedHost:false}` trả `spa-abc.vsite.local:3000`.
  - `{xForwardedHost:true}` trả `spa-xyz.vsite.local`, để đối chiếu.
- Mã nguồn h3 (`h3-Bz4OPZv_.mjs:622`) chỉ đọc `x-forwarded-host` khi `opts.xForwardedHost` là truthy, còn lại trả `headers.get("host") || ""`.
- ⚠️ Có ba điểm T2 phải biết:
  - Giá trị trả về **còn cổng** (`:3000`).
  - **Chưa lowercase.**
  - Thiếu header thì nhận **chuỗi rỗng `""`**, không phải `"localhost"` như JSDoc ghi.
  - Vì vậy T2 phải tự chuẩn hoá (A11). Spike đã thử `ACCEPT-4955.VSITE.LOCAL.:3000` → `accept-4955.vsite.local`, BE trả 200.

**3b. `setResponseStatus` / `setResponseHeader` trong server fn được loader gọi lúc SSR — ⚠️ ĐẠT MỘT PHẦN**

| Kịch bản (build, dev giống hệt) | Status HTML | Header từ server fn |
|---|---|---|
| server fn `setResponseStatus(418)`, loader trả bình thường, **không** middleware | **200** ❌ | ✅ có (`cache-control`, `x-spike`) |
| loader ném `notFound()`, **không** middleware | 404 ✅ | ❌ **mất** (`cache-control` và `x-spike-nf` không có); option `headers` của route thì **còn** (`x-route-headers: yes`) |
| có middleware dưới đây | 418 ✅ / 404 ✅ | ✅ còn đủ ở cả hai |

- **Vì sao status bị bỏ qua:**
  - Status của HTML SSR **chỉ** lấy từ `router._serverResult.status` (`react-router/dist/esm/ssr/renderRouterToStream.js:37`).
  - Router chỉ sinh ra ba giá trị: 200, 404 (`notFound`) và 500 (loader ném lỗi), theo `router-core/dist/esm/load-server.js:281-327`.
- **Vì sao header bị mất ở response lỗi:**
  - h3 chỉ ghép header của event vào response **ok**.
  - Với response không ok, `mergeEventResponseHeaders` (`start-server-core/dist/esm/request-response.js:17`) chỉ ghép `set-cookie`.
- **Cách làm đã kiểm (dev + build).** Tạo file `apps/web/src/start.ts`. Start tự nhận file này, không cần sửa config:
```ts
import { createMiddleware, createStart } from '@tanstack/react-start';
import { getResponseHeaders, getResponseStatus } from '@tanstack/react-start/server';

const applyEventStatus = createMiddleware().server(async ({ next }) => {
  const result = await next();
  const eventStatus = getResponseStatus();               // 200 nếu không ai set
  const status = eventStatus !== 200 && result.response.status === 200 ? eventStatus : result.response.status;
  if (status === 200) return result;
  const headers = new Headers(result.response.headers);
  (getResponseHeaders() as unknown as Headers).forEach((v, k) => headers.set(k, v)); // h3 bỏ header event khi !ok
  return new Response(result.response.body, { status, headers });
});

export const startInstance = createStart(() => ({ requestMiddleware: [applyEventStatus] }));
```
- Middleware này chạy cho **mọi** request: trang SSR, RPC `/_serverFn/*` và server route. Lý do điều kiện chỉ ghi đè khi router trả 200: để không che mất 404/500 của router.
- **Gợi ý cho T4 (người/plan chốt):**
  - 404: dùng `throw notFound()`, kèm `notFoundComponent`. Status đúng sẵn.
  - 503 và 502: cần middleware trên, hoặc một cơ chế tương đương.
  - `Cache-Control: private, no-cache`: đặt bằng option `headers` của route. Option này còn nguyên ở cả 404, khỏi phụ thuộc middleware. Nếu dùng `setResponseHeader` thì phải có middleware để header không mất khi status không ok.
- **RPC khi điều hướng phía client:** gọi thẳng `GET /_serverFn/<id>` của server fn có `setResponseStatus(418)` thì nhận `HTTP 418` + `x-tss-serialized: true` + body serialized đầy đủ.
  - `serverFnFetcher.js` giải mã nhánh serialized trước khi xét `!response.ok`, nên khả năng cao client vẫn nhận được dữ liệu.
  - **Chưa kiểm bằng trình duyệt**, cần kiểm ở T10: điều hướng client từ trang có sang trang 404/503.

**3c. Server route `/robots.txt` — ĐẠT**
- Tên file: **`src/routes/robots[.]txt.ts`**. Dấu `.` được escape bằng `[.]`. Router plugin sinh route `/robots.txt` trong `routeTree.gen.ts`.
- Cú pháp:
```ts
import { createFileRoute } from '@tanstack/react-router';
import { getRequestHost } from '@tanstack/react-start/server';

export const Route = createFileRoute('/robots.txt')({
  server: {
    handlers: {
      GET: ({ request }) =>
        new Response('User-agent: *\nAllow: /\n', { headers: { 'content-type': 'text/plain; charset=utf-8' } }),
    },
  },
});
```
- Kiểm thử: `HTTP 200`, `content-type: text/plain; charset=utf-8`. Trong handler, `getRequestHost({xForwardedHost:false})` trả `spa-abc.vsite.local:3000` dù request có `X-Forwarded-Host: evil.com`, và `request.url` là `http://spa-abc.vsite.local:3000/robots.txt`.
- Request middleware toàn cục (3b) cũng chạy qua server route này.
- Type `handlers` nằm ở `start-client-core/dist/esm/serverRoute.d.ts`. Có hai dạng: object `{ GET, POST, … }`, hoặc `({ createHandlers }) => …` khi cần middleware riêng cho từng method.

### 4. BE thật — ĐẠT
- Hạ tầng dùng trong spike:
  - `docker compose up -d` (Redis 6379).
  - BE đọc Postgres ở **cổng 5433**, user `postgres`, theo `appsettings.Development.json`. Đây **không phải** container `postgres` của compose (5432, DB rỗng). Instance 5433 có sẵn dữ liệu nghiệm thu của WEBSITE-004.
  - Chạy BE: `dotnet run --project backend/src/Vsite.Api --launch-profile http` (`http://localhost:5270`).
- Gọi từ **bundle build** qua server fn, `API_ORIGIN=http://localhost:5270`, Host đã chuẩn hoá:

| Host request gốc | Host gửi BE | Lần 1 | `If-None-Match` |
|---|---|---|---|
| `accept-4955.vsite.local:3000` (Published) | `accept-4955.vsite.local` | **200** `status=Published`, ETag `"e44a35db…-1795014991182"`, `Cache-Control: no-cache` | **304**, body rỗng |
| `ACCEPT-4955.VSITE.LOCAL.:3000` | `accept-4955.vsite.local` | 200, cùng ETag | 304 |
| `spa-abc.vsite.local:3000` (không có shop) | `spa-abc.vsite.local` | **404** `error_code=SITE_NOT_FOUND`, `Cache-Control: no-cache,no-store` | — |
| `acc-lt53lv.vsite.local:3000` (Suspended) | `acc-lt53lv.vsite.local` | **200** `status=Suspended`, `publication` null, **không ETag** | — |

- Request lần nào cũng kèm `X-Forwarded-Host: spa-abc.vsite.local`, nhưng shop vẫn được chọn theo Host gốc.
- ⚠️ Hành vi của axios mà T3 cần biết:
  - Với `validateStatus` mặc định, **304 bị reject**. Đó là AxiosError thô có `response.status=304`, vì `rejectWithProblemDetails` chỉ đổi lỗi thành `data` khi `data` khác rỗng.
  - Với 404 (`validateStatus` mặc định), promise reject bằng **body ProblemDetails**, không có status code.
  - Cách đã kiểm: `client.get('/api/site', { headers: { 'If-None-Match': etag }, validateStatus: () => true })`, rồi đọc `r.status` và `r.headers['etag']`. Plan T3 dùng `validateStatus: s => s===200||s===304||s===404` là đúng.
  - Header gửi theo từng request qua `config.headers` được ghép với `Host` của client.
- Đã dừng BE, echo server, các process Node và `docker compose stop`. Các container compose vốn ở trạng thái Exited trước khi chạy spike.

### Điều bất ngờ / cần plan lưu ý
1. **`setResponseStatus` trong server fn không đổi status HTML** (3b). Plan T4 đang ghi "`setResponseStatus` 404/503/502". Để đạt được phải thêm `src/start.ts` + request middleware (đã kiểm), hoặc dùng `notFound()` cho 404. Đây là **file mới chưa có trong cây file của plan §5**, cần người duyệt trước khi T4 thêm.
2. Với response không ok, header đặt bằng `setResponseHeader` bị h3 bỏ, trừ khi có middleware. Option `headers` của route thì luôn còn.
3. `getRequestHost` trả `""` khi thiếu Host, khác với JSDoc. Kết quả còn cổng và chưa lowercase, nên T2 phải xử lý.
4. Postgres dev mà BE dùng ở cổng 5433, không phải container compose ở 5432. Để ý khi nghiệm thu T10.
5. Dev server Vite tự sinh lại `routeTree.gen.ts` khi thêm hoặc xoá route. File này được git track, nên mỗi task thêm route sẽ có diff ở file đó (đúng như mong đợi).

## Đã làm (theo task)

| Task | Commit | Nội dung |
|---|---|---|
| Plan | `7b3bf71` | plan + data-needs Bước 8c |
| S0 | `04ea8f5` | Spike (xem trên) |
| T1 | `73111c9`, `f78b1b4` | Barrel `@vsite/api-sdk` export site + publication của WEBSITE-004; dependency + env `ROOT_DOMAIN`/`API_ORIGIN` |
| T2 | `19f4c8d` | `resolveSiteHost` (`server/site-host.ts`): host đúng một nhãn dưới `ROOT_DOMAIN`, không reserved; host khác → platform/404, **không gọi BE** (vá M4 ở FE) |
| T3 | `92221b9`, `be9728e` | `site-client`: axios per-request với `Host` đã chuẩn hoá, ETag/304, LRU 200 host (key = host chuẩn hoá), Zod parse; không forward `Cookie`/`Authorization` |
| T4 | `5ea4c4c` | `loadSite` server fn (input chỉ `path`, host đọc trong server fn) + `loadSiteForHost` + route `/` và `/$`; `start.ts` (B1) |
| T5 | `f303879` | `SiteView` render snapshot bằng `builder-renderer`, năm © từ loader (#23) |
| T6 | `d054c11` | Anchor `#nodeId` cuộn được (G3): `decorateWithAnchor` + `useAnchorScroll` ở `builder-renderer` |
| T7 | `3451ed1` | `<head>` SEO: title, description, canonical, OG, favicon, noindex (G9, R9, R10) |
| T8 | `074707b` | Trang NotFound / Suspended (503) / lỗi (502) cho Shop Site |
| T9 | `784a518` | `robots.txt` + `sitemap.xml` theo host (#10) |
| Fix wave | `f1a85b0` | 7 điểm minor từ review T2–T8: CSRF guard test, host dấu chấm, path, noindex |
| T10 | `db71a28`, `69e53b0` | Test hydrate không lệch (R8); path reserved `media/api/assets/static` trả 404 chung, không gọi BE |

## Cách forward Host đã kiểm (để người sau khỏi làm lại)

- Forward `Host` bằng **axios** qua `createApiClient({ baseURL: API_ORIGIN, headers: { Host } })` cho từng request. `fetch`/undici **bỏ** header `Host` tự đặt.
- Host lấy bằng `getRequestHost({ xForwardedHost: false })`, rồi chuẩn hoá (lowercase, bỏ một dấu chấm cuối, bỏ cổng). Cùng một giá trị làm key LRU và gửi BE (A11).
- `Cookie`/`Authorization` của khách không bao giờ được forward.

## Nghiệm thu tay (T10)

> Ngày 2026-11-19, nhánh `feature/WEBSITE-005` @ `db71a28`.
> - BE thật: `dotnet run --project backend/src/Vsite.Api --launch-profile http` (`:5270`), Postgres dev `:5433`, Redis `docker compose up -d redis`.
> - Web: `pnpm --filter @vsite/web build` rồi `PORT=3000 HOST=127.0.0.1 API_ORIGIN=http://localhost:5270 ROOT_DOMAIN=vsite.local node apps/web/.output/server/index.mjs`.
> - Gọi web bằng `curl --resolve spa-abc.vsite.local:3000:127.0.0.1 http://spa-abc.vsite.local:3000/…`. Phải dùng `--resolve` vì hosts file map `spa-abc.vsite.local` cả `::1` lẫn `127.0.0.1`, còn server chỉ nghe IPv4.
>
> **Owner spa-abc:** không có seed hay credential sẵn. Trước khi chạy, DB không có shop slug `spa-abc` (vì thế S0 thấy 404). Đã làm theo đúng luồng WEBSITE-004:
> 1. `POST /api/auth/register` một Owner mới (`t10-owner-…@example.com`).
> 2. Verify bằng link trong log của `LoggingEmailSender`, rồi `POST /api/auth/login` để lấy token global.
> 3. `POST /api/shops` `{name:"Spa ABC", slug:"spa-abc", kind:"Hosted"}` → shop `5a59cc83-…`.
> 4. `POST /api/shops/{id}/website` → 201, có sẵn 3 trang: Trang chủ, Liên hệ, NotFound.
>
> ⚠️ Shop, website, 2 bản publish và logo này **vẫn còn trong DB dev :5433**.

### Bước 1 — Publish → xem site: **ĐẠT**
- `PUT …/pages/{home}/draft` với cây A (RichText "Nội dung phiên bản A T10" + Section `c_anchor01` / RichText `c_deep001`): 200.
- `POST …/website/publications {"note":"T10 v A"}`: 201, `version:1`, `isCurrent:true`.
- `curl … http://spa-abc.vsite.local:3000/` → **200**, `content-type: text/html; charset=utf-8`, `cache-control: private, no-cache`. HTML chứa "Nội dung phiên bản A T10", `<title>Trang chủ</title>`, `© 2026 Spa ABC`.
- Ghi chú: lần GET đầu sau publish trả 404. Nguyên nhân là BE vẫn giữ **negative cache 1 phút** của `ShopLookupService`, sinh ra từ lần tra `spa-abc` trước khi tạo shop. Đây là hành vi đã thiết kế ở BE. Hết 1 phút thì 200.

### Bước 2 — Sửa draft sau publish (R11): **ĐẠT**
- `PUT …/draft` với cây B ("phiên bản B T10"): 200, draft `isDirty:true`, `GET …/draft` thấy B.
- `curl …/` → 200, **vẫn** "Nội dung phiên bản A T10" (2 lần trong HTML, không có B).

### Bước 3 — Publish lại, rồi rollback: **ĐẠT**
- `POST …/publications {"note":"T10 v B"}`: 201, `version:2`. `curl …/` → 200, "Nội dung phiên bản B T10".
- `POST …/publications/{v1-id}/rollback`: 200, trả `version:1`, `isCurrent:true`.
- `GET …/publications`: `v2 isCurrent:false`, `v1 isCurrent:true`.
- `curl …/` → 200, **về lại** "Nội dung phiên bản A T10". Site đổi ngay, không phải chờ: LRU hỏi lại BE bằng `If-None-Match`.

### Bước 4 — View-source: **ĐẠT**
- HTML 8022 byte, `<body>` chứa sẵn nội dung server render **trước** thẻ `<script>` đầu tiên (2343 ký tự):
  - header ("Spa ABC")
  - `<main><div id="c_intro01" style="display:contents"><div class="text-left"><p>Nội dung phiên bản A T10</p>…`
  - wrapper anchor `c_anchor01` / `c_deep001`
  - footer `© 2026 Spa ABC`
- Không phải vỏ rỗng.

### Bước 5 — Cảnh báo hydrate trên trình duyệt thật: **ĐẠT** (headless Chrome qua CDP)
- Công cụ: Chrome `C:\Program Files\Google\Chrome\Application\chrome.exe --headless=new --host-resolver-rules="MAP spa-abc.vsite.local 127.0.0.1"`. Script Node (WebSocket CDP) thu `Runtime.consoleAPICalled`, `Runtime.exceptionThrown` và `Log.entryAdded`. Script ở scratchpad, không commit.

| Kịch bản | Console / exception | Quan sát |
|---|---|---|
| Load `/` | **0** console.error/warn, 0 exception. Chỉ có `log.error` mạng `favicon.ico` 404 (trình duyệt tự xin, snapshot không có favicon) | Router `status: idle` (đã hydrate); footer `© 2026 Spa ABC` |
| Điều hướng client `__TSR_ROUTER__.navigate({to:'/khong-co-trang-nay'})` | 0 console. Có `log.error` mạng: RPC `GET /_serverFn/…` → **404** | Client **vẫn render đúng** NotFound của shop trong layout ("Trang bạn tìm không tồn tại…", © 2026). Mối lo của S0 §3b (status ≠ 2xx ở RPC) không thành lỗi |
| Điều hướng client tới `/` kèm `hash:'c_deep001'` | 0 | `scrollY=117`, phần tử đích `top=0`: **có cuộn** |
| Load thẳng `/#c_deep001` | 0 | `scrollY=117`, `top=0`: **có cuộn** (`useAnchorScroll` sau hydrate) |
| Load `/khong-co-trang-nay` | 0 console. `log.error` mạng: document 404 (đúng status) | NotFound của shop |
| Load `/lien-he` | 0 | `document.title = "Liên hệ"` |

- Chưa kiểm: Firefox/Safari, DevTools có giao diện, bấm link `<a>` thật trong nội dung. Link trong nội dung là `<a>` thường nên đi bằng full reload, không qua router.

### Bước 6 — `GET /media/…` trên `node .output` (nợ b): **ĐẠT sau fix `69e53b0`** (nợ b vẫn mở)
**Lần đo đầu, trước fix, ở `db71a28`, BE thật:**
- `PUT /api/shops/{id}/logo` (PNG 64×64): 200. `/api/site` trả `shop.logoUrl = /media/shops/5a59cc83-…/website/2026/11/879f4c52-….webp`.
- `curl …:3000{logoUrl}` → 404 `text/html`, 7780 byte. Đó là **trang NotFound của shop** (layout có tên shop và © năm), và loader **có gọi BE `/api/site`**. Kết quả này trái với plan §2 Phát hiện 2.
- Cùng URL gọi thẳng BE (`Host: spa-abc.vsite.local`, `:5270`) → **200 `image/webp`**, 258 byte, `nosniff`.

**Sau fix `69e53b0`:**
- Thay đổi: `loadSiteForHost` trả 404 chung (`kind: notFound`, `Cache-Control: private, no-cache`) cho path có đoạn đầu thuộc `reservedShopSitePaths`, **trước** `fetchSite`.
- Môi trường đo: `pnpm --filter @vsite/web build`, `node .output` với `API_ORIGIN=http://127.0.0.1:5271`. Cổng 5271 là **BE stub** in ra mọi request nhận được.

| Path trên `spa-abc.vsite.local:3000` | Kết quả |
|---|---|
| `{logoUrl}` (`/media/shops/…/879f4c52-….webp`) | 404 `text/html`, 3316 B, `cache-control: private, no-cache` |
| `/media/khong-co.png` | 404 `text/html`, 3228 B, `private, no-cache` |
| `/api/site` | 404 `text/html`, 3218 B, `private, no-cache` |
| `/assets/x.js` | 404 `text/html`, 3221 B, **`cache-control: public, max-age=31536000, immutable`** (header do handler static của Nitro đặt cho tiền tố `/assets/`, xem Concerns) |
| `/static/x` | 404 `text/html`, 3218 B, `private, no-cache` |
| `/Media/x` | 404 `text/html`, 3217 B, `private, no-cache` |

- **Số request BE stub nhận trong 6 request trên: 0.**
- Đối chứng: `/lien-he` → stub nhận đúng 1 `GET /api/site host=spa-abc.vsite.local`.
- Body là **trang 404 chung** (`<title>vsite</title>`, "404 Không tìm thấy trang… Trang bạn tìm không tồn tại hoặc đã được chuyển đi."). Không có layout shop, không lộ tên shop.

**Nợ (b) vẫn mở.** App **không** proxy `/media` hay `/api`. Ở prod, Caddy bắt buộc phải route `/media/*` và `/api/*` sang BE. Thiếu Caddy thì `<img src="/media/…">` (logo, ảnh Hero) vẫn vỡ ảnh, chỉ khác là giờ ra 404 chung, nhanh và không gọi BE.

### Bước 7 — Bộ nhớ (A9): **ĐẠT** (đo thô, WorkingSet của process Node)
- Snapshot thật: `/api/site` body **2932 byte** (spa-abc, 3 trang) và **2012 byte** (accept-4955). 200 host × vài KB thì LRU chỉ tốn cỡ < 1 MB. Snapshot vài trăm KB × 200 = hàng chục MB, như A9 lo, chỉ xảy ra với site rất lớn.
- Không giới hạn heap:

| Thời điểm | RSS |
|---|---|
| idle | 49.1 MB |
| sau 1 request mỗi host (2 host) | 67.3 MB |
| +200 request | 95.2 MB |
| +200 | 106.7 MB |
| +200 | 140.1 MB |
| +200 | 164.4 MB |

  RSS tăng dần vì V8 thu rác lười.
- Kiểm rò rỉ: chạy lại với `--max-old-space-size=48`. RSS **dừng ở ~88 MB** (80.5 → 83.3 → 87.8 → 87.6 → 88.0 qua 1000 request) và request cuối vẫn 200. Không thấy rò rỉ. Mức tăng ở trên là heap chưa thu, không phải LRU.

### Bước 8 — Dọn process: **ĐẠT**
- Đã dừng:
  - Vsite.Api (pid 4948, `:5270`)
  - các lần chạy web `node .output` (pid 21000, 7428, 7836, `:3000`)
  - Chrome headless (script `kill`, sau đó kiểm không còn process `t10-chrome-*`)
  - `docker compose stop redis` (container `nvsite-redis-1` Exited, `wslrelay` giữ `:6379` cũng tự tắt)
- Kiểm lại: không còn gì LISTEN trên 3000/5270/6379/9333.
- Postgres `:5433` (pid 4836) là instance native có sẵn từ trước, **không** do task này bật nên để nguyên.



## Lệch có chủ đích so với plan

1. **Thêm `apps/web/src/start.ts`** (ngoài sơ đồ §8): request middleware đưa status/header từ server fn vào HTML (B1).
2. **Path `/media`, `/api`, `/assets`, `/static` trên host shop** trả 404 chung, không gọi BE (plan Phát hiện 2; sửa ở `69e53b0` sau khi T10 đo thấy loader vẫn gọi BE).
3. **R6 URI hỏng** (`/%E0%A4%A`): framework trả 400 trước loader, không phải 404 như plan ghi (B4). Không crash.
4. **Override `publishedAt`** trong `site-client` (B3), thay vì dùng thẳng schema sinh.
5. **Router `trailingSlash: 'preserve'`** (B5) để `/lien-he/` ra cùng trang, không redirect.
6. **Sửa `builder-renderer`** (anchor) trong task này; `api-sdk` chỉ thêm export barrel (A4, A8). Portal giữ `decorateWithId` riêng.

## Giả định tôi đã tự đặt

| # | Giả định | Vì sao / cách đảo |
|---|---|---|
| B1 | `apps/web/src/start.ts`: request middleware toàn cục để status (503/502) và header (`Cache-Control`) do server fn đặt tới HTML; chỉ ghi đè khi router trả 200; header của event **không** ghi đè `content-type`/`content-length`/`set-cookie`. Khai `requestMiddleware` làm Start **bỏ** CSRF middleware mặc định (`createStartHandler.js:21,288`), nên thêm lại y hệt `createCsrfMiddleware({ filter: serverFn })` đứng đầu chuỗi + test hồi quy. Snippet S0 làm hỏng RPC `/_serverFn` (không có `result.response`) nên đã guard + test. | S0 3b. Đảo: bỏ file, chấp nhận 200 cho Suspended/lỗi BE (và phải tự giữ CSRF). |
| B2 | Env đọc lazy; tách `readRootDomain()` / `readApiOrigin()` để host platform chạy được khi thiếu `API_ORIGIN`; thiếu biến thì ném lỗi rõ ràng. | Tránh sập cả app vì biến chỉ shop host cần. |
| B3 | Zod sinh ra `publishedAt: z.string().datetime({})` từ chối `+00:00` mà .NET `DateTimeOffset` ghi. Override cục bộ ở `site-client` (extends schema sinh, chỉ `publishedAt` cho `offset: true`). Contract đúng, cấu hình Orval quá chặt. | **Đề xuất task riêng:** Orval zod `dateTimeOptions: { offset: true }` + regen (ảnh hưởng mọi datetime sinh ra). |
| B4 | URI hỏng (`/%E0%A4%A`) → framework 400 trước loader (R6 ghi 404); `//x` → 308 do framework. Không crash. | Hành vi framework; chặn sớm hơn là chấp nhận được. |
| B5 | Router `trailingSlash: 'preserve'`; canonical bỏ dấu `/` cuối. | A10: không redirect. |
| B6 | Năm © theo giờ `Asia/Ho_Chi_Minh` (UTC+7 cố định, VN không DST), tính một lần ở loader. | Người quyết 2026-11-19 (ban đầu tôi đặt UTC). |
| B7 | 404 không có `error_code = SITE_NOT_FOUND` → `SiteFetchError` (502); Published không có ETag thì phục vụ nhưng không cache. | Không đoán nghĩa của 404 lạ. |
| B8 | Đoạn đầu path thuộc `reservedShopSitePaths` (không phân biệt hoa/thường, đã decode) trên host shop → 404 chung, không gọi BE. | Plan Phát hiện 2; cùng một nguồn `config/reserved-routes.json`. |
| B9 | robots/sitemap dùng `resolveSiteHost` + `fetchPublishedSite` trực tiếp (không qua `loadSiteForHost`, vì hàm đó 404 khi thiếu trang chủ); Suspended → 404; mọi response `Cache-Control: private, no-cache`. | Site không có trang chủ vẫn có robots. |
| B10 | `apps/web` chưa có quy ước loading/empty → không thêm `pendingComponent`; `errorComponent` ở `/` và `/$` render trang chung, không bao giờ in `error.message`. | R5: không lộ chi tiết. |
| B11 | Host IPv6 literal → platform; chỉ bỏ **một** dấu chấm cuối (`..` → platform); cổng chỉ gồm chữ số. | R1: không chọn nhầm shop. |
| B12 | Anchor: wrapper không có `firstElementChild` → no-op; hash hỏng (decode lỗi) → dùng giá trị thô. | Không ném khi hydrate. |
| B13 | Canonical/og/sitemap dùng protocol của request; `X-Forwarded-Proto` bị bỏ qua có chủ ý (A3). | Xem nợ ⛔ `http://` sau TLS proxy. |

## Chưa làm xong / nợ

**⛔ Trước production**
1. **CSRF `SameSite=Lax`** (nợ c): không sửa ở FE; đề xuất task IDENTITY.
2. **Canonical/og:image/sitemap thành `http://`** khi sau TLS proxy (A3 bỏ qua `X-Forwarded-Proto`): quyết `PUBLIC_PROTOCOL` env hoặc trusted proxy.
3. **Caddy phải route `/media/*` và `/api/*` sang BE** (nợ b): `node .output` không có proxy; app trả 404 chung cho các path này. Nitro còn trả 404 của `/assets/*` không tồn tại kèm `Cache-Control: public, max-age=31536000, immutable`: ghi vào cấu hình Caddy/CDN.
4. **`shop_slug_history`** (nợ a): đã chấp nhận, task riêng (BE + Gate 1). Chưa có thì slug đổi tên là 404, không 301.

**Theo dõi**
5. **M4 phía BE vẫn mở:** BE resolve tenant theo nhãn đầu của Host (`evil.com` → slug `evil`); FE chỉ chặn.
6. **Task Orval datetime offset** (B3).
7. **Portal đổi sang `decorateWithAnchor`** (A8); Portal hiện còn `decorateWithId` riêng.
8. **WEBSITE-006:** UI publish/rollback ở Portal, nạp lại version draft sau publish (R9), cảnh báo `references`.
9. **Lỗi typecheck Portal có từ trước nhánh này:** `apps/portal/src/components/media/media-library-picker.tsx:27` TS2366; `apps/portal/src/routes/-website.test.tsx:17` thiếu `currentPublicationId`.
10. **Dữ liệu nghiệm thu T10 còn trong Postgres dev :5433:** Owner `t10-owner-…@example.com`, shop `spa-abc`, 2 publication, logo.
11. **A9 đã đo:** snapshot ~3 KB; RSS dừng ở ~88 MB sau 1000 request với heap cap 48 MB. Chỉ đáng lo khi snapshot rất lớn.
12. Điều hướng client tới trang NotFound: RPC `_serverFn` trả 404 (DevTools hiện dòng đỏ), client vẫn render đúng. `favicon.ico` 404 khi snapshot không có favicon: vô hại.
13. Chưa kiểm Firefox/Safari; link `<a>` trong nội dung là reload đầy đủ, không qua router.
14. **Không cache 404 ở FE** (review m4): flood `{rand}.{ROOT_DOMAIN}` → mỗi request một lần gọi BE (BE có negative cache 1'). Chấp nhận trước production; rate-limit ở Caddy khi lên production.
15. **Canonical `encodeURI` trên path router đã decode** (review m5, `site-head.ts:31`): path chứa `%25` thành `%2525` trong canonical. Slug BE là ASCII nên gần như không xảy ra; sửa khi có slug ngoài ASCII.

## Sửa sau review (Gate 2)

- **C1** — Banner STATUS `DesignIdeal/05` (thêm WEBSITE-005 vào Tasks, Changelog trỏ file này, bỏ "render site công khai (8c)" khỏi "chưa code", anchor/năm © "đã code ở WEBSITE-005") + câu "Runtime đọc gì" ở §7 viết lại theo thực tế (GET /api/site theo Host, LRU theo host + ETag/304, HTML `private, no-cache`). 00-INDEX §2 sinh lại bằng `pnpm gen:doc-index`.
- **I1 + m3** — `site-client.ts`: request `/api/site` có `timeout: SITE_FETCH_TIMEOUT_MS` (5000, tiêm được qua `deps.timeoutMs`), `maxRedirects: 0`, `proxy: false`. BE treo / 3xx → `SiteFetchError` → 502. Test bằng Node http server thật: server không trả lời (timeout 200 ms), 302 (không theo, đúng 1 request), `HTTP_PROXY` trỏ cổng chết vẫn tới BE.
- **I2** — `eslint.config.mjs`: block riêng `apps/web/**` với `no-restricted-imports` — `paths` cấm theo tên mọi export runtime draft/editing của module website (`@vsite/api-sdk`: hàm, hook, query key/options, mutation options, schema Zod; `@vsite/api-sdk/mocks`: mock MSW của `website.msw`), giữ luật cấm `builder-core` + `apps/`, thêm cấm đi tắt `api-sdk/src/**`. Đã thử RED (file thăm dò: 4 import draft + builder-core bị chặn, `getPublishedSite`/`createApiClient`/`getSiteMock` không) rồi xoá.
- **m1, m6** — `apps/web/CLAUDE.md`: mô tả chốt lint thật, timeout BE, cách chạy dev/build với `API_ORIGIN`/`ROOT_DOMAIN`, bảng Phạm vi ghi một dòng nhất quán.
- **m2 — đã kiểm, ĐẠT, không nợ.** Build `b67e293`, `node .output` + BE stub, gọi `GET /_serverFn/<loadSite id>?payload=…` như client (kèm `x-tsr-serverFn`, `Sec-Fetch-Site: same-origin`): shop không tồn tại → 404, Suspended → 503, BE 500 → 502; cả ba có `Cache-Control: private, no-cache` + `x-tss-serialized: true`, body 502 chỉ `{kind:'error'}`. Thiếu `Sec-Fetch-Site`/`Origin` → 403 (CSRF guard đúng).
- **m4, m5** — ghi theo dõi (mục 14, 15 ở trên).

## Kiểm tra

- `pnpm --filter @vsite/web test`: **268 passed** (17 file, sau sửa review ở `b67e293`); lint + typecheck + build sạch. `pnpm lint` toàn repo: fail ở `@vsite/builder-components` (`meta/envelope-schema.ts:231-232` no-unused-vars) — có từ trước, fail y hệt với `eslint.config.mjs` của HEAD cũ, không thuộc task này.
- `pnpm build && pnpm test` toàn repo: exit 0 (build 4/4, test 10/10; còn dòng nhiễu cũ `ReferenceError: module is not defined`, exit 0). Đo ở `db71a28`; `69e53b0` sau đó chạy lại package web.
- `pnpm check:docs`: OK (20 cảnh báo cũ, không chặn).
- Nghiệm thu tay: 7/7 bước ĐẠT (phần trên).
