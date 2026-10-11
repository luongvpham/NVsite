# REFACTOR-API-001 — FE brief

## Contract

- `contracts/openapi/identity.v1.json`, `shop.v1.json`, `media.v1.json`. Promote 2026-10-09, sha256
  ở `contracts/contract.lock`.
- **Thay đổi duy nhất:** mọi path thêm prefix `/api`, và mỗi operation có `operationId` tường minh
  (Quyết định #91). Schema, auth, response, error code **không đổi**. Bảng `operationId` → tên hook
  ở `contract-diff.md`.

## Việc FE cần làm

1. `pnpm gen:api` (lock khớp).
2. **`packages/api-sdk/src/index.ts`** (viết tay, không generated): sửa các dòng export model chọn
   lọc theo tên file mới, vd. `getShopsShopIdMediaAssetsParams` → `getAssetsByIdsParams`.
3. **Mutator `axios-instance.ts`:**
   - `REFRESH_EXEMPT_URLS` và URL refresh viết tay đổi sang `/api/auth/...`.
   - Thêm factory `createApiClient(...)` cho SSR (mục "SSR" bên dưới). Giữ nguyên API hiện có của
     Portal: `setAccessToken`, `setRefreshToken`, `setOnSessionExpired`, `customInstance`.
4. **Portal:**
   - Đổi tên ~24 hook/hàm sang tên theo `operationId` (`useGetShops` → `useListShops`,
     `getGetShopsQueryKey` → `getListShopsQueryKey`, `postShopsBody` → `createShopBody`…).
   - `vite.config.ts` proxy: thay `/auth` + `/shops` bằng **một** mục `/api`. Bỏ proxy `/shops` cũng
     sửa luôn bug F5 ở route SPA `/shops/new` bị đẩy sang BE.
   - MSW handler trong test đổi sang `/api/...`.
5. **apps/web:** chưa gọi API. Chỉ thêm proxy `/api` vào `vite.config.ts` để sẵn sàng cho SSR loader.

## SSR — `createApiClient`

- **Vấn đề:** mutator hiện là singleton cấp module (một axios instance và token chung cho cả
  process). Khi `apps/web` render ở server, token của request này sẽ lẫn sang request khác. Server
  cũng không có relative URL, và phải forward Host gốc để BE resolve tenant.
- **Cách làm:** factory tạo client riêng cho từng request (baseURL tuyệt đối, header Host/
  X-Forwarded-Host, token riêng). `customInstance(config, options?)` nhận `options.client`. Orval tự
  thêm tham số `options` vào mọi hàm sinh ra, nên loader gọi được `listShops({ client })`. Không
  truyền client thì dùng instance mặc định của Portal.

## Ràng buộc

- Không sửa tay `packages/api-sdk/src/generated/**` (hook chặn).
- `/media/*` **không** đổi, vẫn ở root, không qua `/api`.
- Không đổi hành vi Portal ngoài tên hàm và URL.

## Không thuộc phạm vi

- Không viết SSR loader thật cho `apps/web`. Chỉ chuẩn bị client; trang dùng API là task sau.
- Không sửa BE. Sai contract thì dừng và báo (vòng D{n}).
- Không đổi refresh token sang httpOnly cookie (review P1, task riêng).

## Acceptance

- `pnpm gen:api && pnpm build && pnpm test` xanh.
- Không còn chuỗi `'/auth` hay `'/shops` dùng làm **URL API** trong `apps/` và `packages/` (route SPA
  `/shops` của Portal thì giữ).
- Test cho `createApiClient`: hai client độc lập không lẫn token; header Host được forward; client
  mặc định vẫn chạy với Portal.
