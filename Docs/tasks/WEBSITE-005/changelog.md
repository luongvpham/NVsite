# WEBSITE-005 — Changelog (Bước 8c: `apps/web` render Shop Site công khai)

> Đang thực thi. Các mục còn lại điền ở T11.

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

## Giả định tôi đã tự đặt

| # | Giả định | Vì sao / cách đảo |
|---|---|---|
| B1 | Thêm `apps/web/src/start.ts` (request middleware toàn cục `createStart`) để status do server fn đặt (503/502) và header của event đi tới HTML; chỉ ghi đè khi router trả 200. File ngoài sơ đồ §8. | S0 3b: `setResponseStatus` trong server fn bị bỏ qua khi SSR — không có middleware thì không có 503/502 (A5). Đảo: bỏ file, chấp nhận 200 cho Suspended/lỗi BE. |
