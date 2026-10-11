# REFACTOR-API-001 — contract diff

## ⚠️ BREAKING / REMOVED
- **REMOVED** `POST /auth/forgot-password` (identity.v1.json) — endpoint có trong contract nhưng runtime không còn
- **REMOVED** `POST /auth/login` (identity.v1.json) — endpoint có trong contract nhưng runtime không còn
- **REMOVED** `GET /auth/me` (identity.v1.json) — endpoint có trong contract nhưng runtime không còn
- **REMOVED** `POST /auth/me/change-password` (identity.v1.json) — endpoint có trong contract nhưng runtime không còn
- **REMOVED** `POST /auth/refresh-token` (identity.v1.json) — endpoint có trong contract nhưng runtime không còn
- **REMOVED** `POST /auth/register` (identity.v1.json) — endpoint có trong contract nhưng runtime không còn
- **REMOVED** `POST /auth/reset-password` (identity.v1.json) — endpoint có trong contract nhưng runtime không còn
- **REMOVED** `GET /auth/verify-email` (identity.v1.json) — endpoint có trong contract nhưng runtime không còn
- **REMOVED** `PUT /shops/{shopId}/logo` (media.v1.json) — endpoint có trong contract nhưng runtime không còn
- **REMOVED** `GET /shops/{shopId}/media/assets` (media.v1.json) — endpoint có trong contract nhưng runtime không còn
- **REMOVED** `GET /shops/{shopId}/media/library` (media.v1.json) — endpoint có trong contract nhưng runtime không còn
- **REMOVED** `POST /shops/{shopId}/media/library` (media.v1.json) — endpoint có trong contract nhưng runtime không còn
- **REMOVED** `DELETE /shops/{shopId}/media/library/{assetId}` (media.v1.json) — endpoint có trong contract nhưng runtime không còn
- **REMOVED** `POST /shops/{shopId}/media/library/{assetId}/clones` (media.v1.json) — endpoint có trong contract nhưng runtime không còn
- **REMOVED** `GET /shops/{shopId}/media/library/{assetId}/derivatives` (media.v1.json) — endpoint có trong contract nhưng runtime không còn
- **REMOVED** `GET /shops/{shopId}/media/library/{assetId}/references` (media.v1.json) — endpoint có trong contract nhưng runtime không còn
- **REMOVED** `POST /shops/{shopId}/media/slot-uploads` (media.v1.json) — endpoint có trong contract nhưng runtime không còn
- **REMOVED** `GET /shops/{shopId}/media/usage` (media.v1.json) — endpoint có trong contract nhưng runtime không còn
- **REMOVED** `GET /shops` (shop.v1.json) — endpoint có trong contract nhưng runtime không còn
- **REMOVED** `POST /shops` (shop.v1.json) — endpoint có trong contract nhưng runtime không còn
- **REMOVED** `GET /shops/{shopId}` (shop.v1.json) — endpoint có trong contract nhưng runtime không còn
- **REMOVED** `PATCH /shops/{shopId}` (shop.v1.json) — endpoint có trong contract nhưng runtime không còn

## NEW_ENDPOINT
- POST /api/auth/forgot-password
- POST /api/auth/login
- GET /api/auth/me
- POST /api/auth/me/change-password
- POST /api/auth/refresh-token
- POST /api/auth/register
- POST /api/auth/reset-password
- GET /api/auth/verify-email
- PUT /api/shops/{shopId}/logo
- GET /api/shops/{shopId}/media/assets
- GET /api/shops/{shopId}/media/library
- POST /api/shops/{shopId}/media/library
- DELETE /api/shops/{shopId}/media/library/{assetId}
- POST /api/shops/{shopId}/media/library/{assetId}/clones
- GET /api/shops/{shopId}/media/library/{assetId}/derivatives
- GET /api/shops/{shopId}/media/library/{assetId}/references
- POST /api/shops/{shopId}/media/slot-uploads
- GET /api/shops/{shopId}/media/usage
- GET /api/shops
- POST /api/shops
- GET /api/shops/{shopId}
- PATCH /api/shops/{shopId}

## ADDITIVE
(không có)

## UNCHANGED
0 operation không đổi.

> **Đọc nhanh:** 22 cặp REMOVED/NEW ở trên là **cùng 22 operation chuyển sang prefix `/api`**, kèm
> **`operationId` tường minh** cho từng operation (người duyệt chọn, xem bảng dưới). Đã đối chiếu: bỏ
> `/api` khỏi path và bỏ `operationId` thì cả 3 module **giống hệt** contract đã chốt (schema, auth,
> response, error code không đổi). Orval chạy thử trên staging: không có type trùng `…Dto2`.

## operationId → tên hàm/hook FE (mới)

| Module | Operation | operationId | Hook React Query |
|---|---|---|---|
| identity | `POST /api/auth/register` | `register` | `useRegister` |
| identity | `GET /api/auth/verify-email` | `verifyEmail` | `useVerifyEmail` |
| identity | `POST /api/auth/login` | `login` | `useLogin` |
| identity | `POST /api/auth/refresh-token` | `refreshToken` | `useRefreshToken` |
| identity | `POST /api/auth/forgot-password` | `forgotPassword` | `useForgotPassword` |
| identity | `POST /api/auth/reset-password` | `resetPassword` | `useResetPassword` |
| identity | `GET /api/auth/me` | `getMe` | `useGetMe` |
| identity | `POST /api/auth/me/change-password` | `changePassword` | `useChangePassword` |
| shop | `POST /api/shops` | `createShop` | `useCreateShop` |
| shop | `GET /api/shops` | `listShops` | `useListShops` |
| shop | `GET /api/shops/{shopId}` | `getShop` | `useGetShop` |
| shop | `PATCH /api/shops/{shopId}` | `updateShop` | `useUpdateShop` |
| media | `POST /api/shops/{shopId}/media/slot-uploads` | `uploadToSlot` | `useUploadToSlot` |
| media | `POST /api/shops/{shopId}/media/library` | `uploadToLibrary` | `useUploadToLibrary` |
| media | `GET /api/shops/{shopId}/media/library` | `listLibrary` | `useListLibrary` |
| media | `POST …/library/{assetId}/clones` | `cloneFromLibrary` | `useCloneFromLibrary` |
| media | `GET …/library/{assetId}/references` | `getAssetReferences` | `useGetAssetReferences` |
| media | `GET …/library/{assetId}/derivatives` | `getDerivatives` | `useGetDerivatives` |
| media | `DELETE …/library/{assetId}` | `deleteFromLibrary` | `useDeleteFromLibrary` |
| media | `GET /api/shops/{shopId}/media/assets` | `getAssetsByIds` | `useGetAssetsByIds` |
| media | `GET /api/shops/{shopId}/media/usage` | `getMediaUsage` | `useGetMediaUsage` |
| media | `PUT /api/shops/{shopId}/logo` | `uploadShopLogo` | `useUploadShopLogo` |

`ApiRoutePrefixTests` khẳng định mọi endpoint API có `operationId`, không trùng, dạng camelCase.

## Auth policy
Không đổi — từng endpoint giữ nguyên policy/role như contract cũ.

## Giả định tôi đã tự đặt (không hỏi)
- `/media/*` (file ảnh public, #53) **không** chuyển vào `/api` — không phải API, URL nằm trong DB/HTML.
- `/openapi/{module}.json` (chỉ dev) giữ ngoài `/api`.
- Link trong email (verify, reset) đổi theo: `{ApiBaseUrl}/api/auth/...`.
- Tên `operationId` theo động từ nghiệp vụ (bảng trên), không theo path.
- Lane B (chạm contract nhưng thay đổi cơ học), không cần `data-needs.md`/`plan.md`.

## Người duyệt đã quyết
| Câu hỏi | Quyết định |
|---|---|
| Tên hàm/hook sinh ra | Thêm `operationId` tường minh (2026-10-09) → Quyết định #91 |
| Chuyển API sang `/api` + bộ `operationId` | **Duyệt** Gate 1 (lần 2, 2026-10-09), promote bởi VietLuong Pham → Quyết định #91 |

## Câu hỏi cần anh quyết
(đã chốt — xem bảng trên)
