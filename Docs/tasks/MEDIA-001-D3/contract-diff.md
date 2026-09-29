# MEDIA-001-D3 — contract diff

## ⚠️ BREAKING / REMOVED
(không có)

## NEW_ENDPOINT
- GET /shops/{shopId}/media/library/{assetId}/derivatives

## ADDITIVE
(không có)

## UNCHANGED
21 operation không đổi (`shop`, `identity` không đổi; `media` chỉ thêm đúng 1 operation).

## Endpoint và auth policy

`GET /shops/{shopId}/media/library/{assetId}/derivatives?preset={optional}` -> `200 MediaAssetDto[]`
(tái dùng schema `MediaAssetDto` sẵn có, không schema mới).

- Auth: `RequireAuthorization(AuthPolicies.RequireGlobalScope)` + `.RequireShopMembership()` (member-level
  đọc; cùng khuôn `.../references`).
- Response khai: 200, 401, 403 (`SHOP_ACCESS_DENIED`), 422 (`preset` > 40 ký tự).
- Ngữ nghĩa: trả bản ghi `SourceAssetId == assetId AND ShopId == route AND !IsDeleted`; `preset` lọc
  bằng chính xác (chứa dấu phẩy, client URL-encode); bỏ `preset` thì trả tất cả, sắp `Preset`
  rồi `CreatedAt`. Bản Library nguồn đã soft delete hoặc không tồn tại vẫn resolve được (A11 / #72).
  Id lạ / của shop khác / không có phái sinh -> `200 []`, không 404.

## Giả định tôi đã tự đặt (không hỏi)

1. Query param `preset` là `string` tuỳ chọn, không bọc trong schema `nullable` (Minimal API tự sinh
   `{type: string}` không `required`) — đủ để Orval sinh `preset?: string`.
2. `preset` dài hơn 40 ký tự -> 422 (validator, khớp cột `MediaAsset.Preset`). Tên preset lạ không bị
   từ chối, trả `[]`.
3. Khi lọc `preset` là so sánh bằng chính xác, phân biệt hoa thường (Postgres mặc định), không trim.
4. Thứ tự ổn định: `Preset` rồi `CreatedAt` (không thêm khoá phụ thứ ba; hai phái sinh cùng preset
   cùng nguồn không xảy ra bình thường).
5. Không phân trang (mỗi nguồn chỉ có vài phái sinh, bị chặn bởi số preset).
6. Cô lập tenant chỉ dựa vào `ShopId` của các dòng phái sinh (không join dòng nguồn); phái sinh luôn
   mang ShopId của nguồn khi sinh (`MediaAsset.NewDerived`).
7. Chỉ dòng phái sinh `!IsDeleted` được trả; chỉ dòng nguồn được phép soft delete (A11) chứ không
   phái sinh, nên "phái sinh đã xoá" chỉ là phòng thủ.
8. Trả 200 `[]` cho id lạ (không 404) — theo chỉ dẫn, để không lộ sự tồn tại id của shop khác.

## Câu hỏi cần anh quyết

1. Có muốn `GetShop`/`ShopDto` sau này trả sẵn logo URL để FE khỏi gọi thêm endpoint này không? (Ngoài
   phạm vi D3; endpoint này vẫn cần cho các nguồn ảnh nghiệp vụ khác.)
2. Có cần cho phép `preset` nhiều giá trị (`?preset=a&preset=b`) không? Hiện chỉ một giá trị.

## Người duyệt đã quyết

| # | Mục | Quyết định | Ngày |
|---|---|---|---|
| | | | |
