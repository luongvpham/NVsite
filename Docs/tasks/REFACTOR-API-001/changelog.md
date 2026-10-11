# REFACTOR-API-001 — changelog

Task 4 của đợt review kiến trúc 2026-10-08 (P0 #11 "API mount ở root" và #12 "api-sdk là singleton
chỉ chạy trên trình duyệt").

- **Lane:** B, chạm contract nhưng thay đổi cơ học.
- **Gate 1:** hai lần.
  - Lần 1: người duyệt chọn thêm `operationId`, chưa duyệt.
  - Lần 2: duyệt và promote 2026-10-09.
- **Quyết định:** #91, định nghĩa ở `backend/CLAUDE.md` §OpenAPI.
- **Banner cập nhật:** `DesignIdeal/02`, `03`.

---

## Đã làm

### BE

1. **Mọi endpoint dưới `/api`.**
   - `Program.cs` map ba module qua `app.MapGroup(ApiRoutes.Prefix)`.
   - Hằng `ApiRoutes.Prefix` đặt ở `Vsite.Application.Common`, vì handler dựng link trong email cũng
     cần.
   - Ngoại lệ có chủ đích: `/media/*` (file ảnh public, URL đã nằm trong DB/HTML) và `/openapi/*`
     (dev).
   - Link verify/reset trong email đổi theo: `{ApiBaseUrl}/api/auth/...`.
2. **`operationId` tường minh cho 22 endpoint** (`.WithName(...)`): `listShops`, `getShop`,
   `uploadToLibrary`, `login`… Bảng đầy đủ ở `contract-diff.md`.
3. **`ApiRoutePrefixTests`** (không cần Docker) chặn ba trường hợp:
   - endpoint nằm ngoài `/api`;
   - endpoint API thiếu `operationId`;
   - `operationId` trùng nhau hoặc không phải camelCase.
4. **Test tích hợp:** 87 URL đổi sang `/api/...`. Route template trong
   `ShopScopedRouteFilterTests` cũng đổi theo.
5. **Contract:**
   - Bỏ `/api` và `operationId` thì cả 3 module giống hệt bản đã chốt. Đã kiểm bằng script so
     sánh, không chỉ dựa vào nhãn của tool.
   - Orval chạy thử trên staging: không có `…Dto2`.

### FE

1. **`pnpm gen:api` → `scripts/gen.mjs`:**
   - Chạy Orval, rồi **sau đó** xoá các file trong `src/generated` mà lần sinh này không ghi lại. Orval
     không tự xoá file cũ, và thư mục này được commit, nên đổi tên `operationId` sẽ để lại file model
     mồ côi trong repo.
   - Orval lỗi thì không xoá gì. Script chạy sau `check-contract-lock`.
   - Bản đầu tiên **xoá trước rồi mới sinh**, và làm lộ một race có sẵn trong turbo (xem mục 6).
     Thư mục từng bị xoá trắng giữa lúc test đang chạy, nên đã đổi sang dọn sau.
2. **Mutator `axios-instance.ts`:**
   - Thêm `createApiClient({ baseURL, headers, getAccessToken })`: client riêng cho từng request
     (SSR), không chia sẻ token/state, reject bằng ProblemDetails như instance mặc định.
   - `customInstance(config, options?: { client })`: Orval tự sinh tham số `options` cho mọi hàm,
     nên gọi được `listShops({ client })`.
   - Thêm hằng `API_PREFIX`.
   - **Sửa bug có sẵn:** refresh token nối chuỗi `` `${baseURL}/auth/refresh-token` ``. Với
     `baseURL = "/"` (Portal), URL thành `//auth/refresh-token`, một URL protocol-relative tới
     **host `auth`**. Nghĩa là refresh token của Portal chưa bao giờ gọi đúng: mọi access token hết
     hạn đều dẫn thẳng về màn login. Giờ truyền `baseURL` cho axios tự ghép.
3. **`packages/api-sdk`:**
   - Thêm vitest (môi trường Node) và `tests/api-client.test.ts`, 4 test:
     - hai client SSR không lẫn token/Host, kể cả khi instance mặc định đang có token;
     - không có token thì không gửi `Authorization`;
     - reject bằng ProblemDetails;
     - instance mặc định: 401 → refresh tại `/api/auth/refresh-token` → retry.
   - `tests/refresh-relative-base.test.ts`: hồi quy cho bug `baseURL "/"`. Nạp lại module với
     `VITE_API_BASE_URL="/"`, khẳng định refresh gọi `'/api/auth/refresh-token'` với `{ baseURL: '/' }`,
     không nối chuỗi thành `//…`.
   - `tsconfig.test.json` để typecheck cả `tests/` (tsconfig chính chỉ có `src`).
   - Test SSR khẳng định header **`Host`** được gửi. BE đọc `Request.Host` và chưa bật
     `UseForwardedHeaders`, nên `X-Forwarded-Host` bị bỏ qua; đã sửa cả hướng dẫn trong mutator.
   - `src/index.ts` sửa 3 dòng export model theo tên file mới.
4. **Portal:**
   - Đổi tên 24 identifier (hook, query key, zod body) theo `operationId`.
   - MSW handler trong test đổi sang `/api/...`.
   - Proxy Vite: hai mục `/auth`, `/shops` thay bằng một mục `/api`. **Sửa bug có sẵn:** proxy
     `/shops` đẩy cả điều hướng tới route SPA `/shops`, `/shops/new`, `/shops/{id}` của Portal sang
     BE, nên F5 ở các trang đó ra 404/JSON.
5. **turbo:**
   - `test` thêm phụ thuộc `gen:api` và `^gen:api`. Trước đây `api-sdk#test` chạy **song song** với
     `api-sdk#gen:api` (bị kéo vào qua `build` của gói phụ thuộc), đúng race mà review FE ban đầu đã
     cảnh báo (P0 #2). Phần còn lại của P0 #2 để task 5.
   - Inputs của `gen:api` thêm `orval.config.ts`, `scripts/**`, `src/mutator/**`, để sửa mutator/
     config thì cache không phát lại output cũ.
6. **`apps/web`:**
   - Thêm proxy `/api`.
   - `CLAUDE.md` ghi quy tắc: SSR loader luôn dùng `createApiClient` theo từng request.
   - Chưa có trang nào gọi API.

## Kiểm chứng (2026-10-09)

- **BE, Docker thật:** `dotnet test backend/vsite.sln` → IntegrationTests **309 pass / 0 fail /
  1 skip** (skip có sẵn của SHOP-001), ArchitectureTests 9/9, ComponentSchemaTests 12/12. Con số
  này đo trước khi thêm `operationId`. Sau đó đã chạy lại `ApiRoutePrefixTests` +
  `ShopScopedRouteFilterTests`; full suite chạy lại ở mục "Chạy lần cuối".
- **FE:**
  - `pnpm build` 4/4.
  - `turbo run test --force` 8/8 task: Portal 48, api-sdk 5, builder-components 88,
    builder-renderer 3, web 1, shared 2.
  - `tsc --noEmit` sạch cho Portal, api-sdk (cả `tests/`), web. Repo chưa có bước typecheck trong CI (task 5).
- **Smoke app thật** (BE `dotnet run` + Portal `vite dev` tại `admin.vsite.local:5173`, không cần DB):
  - `GET /shops/new`, `/shops`, `/auth/me` → `200 text/html` (trang SPA). Trước đây `/shops*` bị
    proxy sang BE.
  - `GET /api/auth/me` → `401` JSON có `error_code` từ BE qua proxy (Host `admin` giữ nguyên).
  - `POST /api/auth/login` body sai → `422 VALIDATION_ERROR`.
  - Node xác nhận bug refresh cũ: `new URL('//auth/refresh-token', 'http://admin.vsite.local:5173')`
    → `http://auth/refresh-token`.
  - Chưa bấm tay luồng đăng nhập đầy đủ (cần DB dev).
- **Chạy lần cuối sau khi thêm `operationId`:** `dotnet test backend/vsite.sln` → IntegrationTests
  309 pass / 1 skip có sẵn, ArchitectureTests 9/9, ComponentSchemaTests 12/12.
- **Không còn URL API cũ** (`'/auth…'`, `'/shops…'` dùng làm URL) trong `apps/` và `packages/`.

## Chưa làm / để task khác

1. **Chưa bấm tay luồng đăng nhập → refresh trên trình duyệt với DB dev.** Proxy và route SPA đã
   smoke bằng HTTP (mục "Kiểm chứng"); refresh có test đơn vị trong `api-sdk`.
2. **Refresh token vẫn nằm trong memory** (F5 là mất phiên). Chuyển sang cookie httpOnly là task
   riêng (review P1).
3. **SSR loader thật cho `apps/web`** chưa có. `createApiClient` mới là phần chuẩn bị.
4. **Link verify/reset trong email trỏ thẳng vào API** (`GET /api/auth/verify-email` trả JSON;
   link reset gọi `GET` vào một endpoint `POST`). Đã vậy từ trước, chỉ đổi prefix. Nên trỏ về trang
   FE (`03` §5).

## Giả định tôi đã tự đặt

- Giữ `/media/*` và `/openapi/*` ngoài `/api` (đã ghi trong contract-diff, người duyệt chấp nhận).
- Đặt `ApiRoutes` ở Application thay vì Api, để handler dựng link email dùng chung một hằng.
- Thêm bước dọn file mồ côi trong `src/generated` vào `gen:api` (sau Orval). Đây là thay đổi quy trình sinh code, không phải sửa
  tay file generated.
