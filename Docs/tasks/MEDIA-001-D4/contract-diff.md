# MEDIA-001-D4 — contract diff

## Phân loại

Công cụ báo **BREAKING** cho 3 operation vì `logoStorageKey` là field response luôn có mặt (nằm trong `required`).
Thực chất là **additive**: field mới `nullable: true`, không xoá/đổi field cũ, không đổi request, không đổi
status code. Cùng trường hợp `logoId` ở Gate 1 vòng 1. Chưa deploy production nên không tạo `v2` (backend/CLAUDE.md).
`media` và `identity`: không đổi.

**Field mới** — `ShopDto.logoStorageKey: string | null`: storage key **tương đối** của phái sinh `320x96,inside`
của logo shop (vd. `shops/{shopId}/{guid}.webp`), `null` khi `logoId` null HOẶC không tìm thấy phái sinh. KHÔNG phải
URL và không có tiền tố `/media/` (chỉ `mediaUrl()`/`resolveImage()` thêm). `logoId` giữ nguyên. Áp dụng cho
`POST /shops`, `GET /shops/{shopId}`, `PATCH /shops/{shopId}`. `GET /shops` (`ShopSummaryDto`) không đổi.

**Auth:** không đổi (POST/GET/PATCH giữ nguyên policy/filter; `ShopId` chỉ từ route/TenantContext, #21.4).

## ⚠️ BREAKING / REMOVED
- **BREAKING** `POST /shops` (shop.v1.json) — chưa deploy production thì cứ sửa BE + regen FE, KHÔNG tạo v2:
  - + field 'logoStorageKey' (required)
- **BREAKING** `GET /shops/{shopId}` (shop.v1.json) — chưa deploy production thì cứ sửa BE + regen FE, KHÔNG tạo v2:
  - + field 'logoStorageKey' (required)
- **BREAKING** `PATCH /shops/{shopId}` (shop.v1.json) — chưa deploy production thì cứ sửa BE + regen FE, KHÔNG tạo v2:
  - + field 'logoStorageKey' (required)

## NEW_ENDPOINT
(không có)

## ADDITIVE
(không có)

## UNCHANGED
19 operation không đổi.

## Giả định tôi đã tự đặt (không hỏi)

1. `logoStorageKey` là storage key **tương đối**, không phải URL (08 §5: chỉ `mediaUrl()`/`resolveImage()` thêm `/media/`).
2. Không tìm thấy phái sinh (chưa sinh, bị soft delete, lệch cấu hình) -> `null`, **không ném** và không log lỗi.
3. `ShopSummaryDto` (`GET /shops`) **giữ nguyên**, không mang logo.
4. Một preset header cứng `320x96,inside` (`LogoPresets.Header`); test khẳng định catalog `For("Shop")` chứa nó.
5. Nhiều dòng thoả điều kiện -> lấy dòng đầu theo `CreatedAt`, rồi `Id`.
6. `logoStorageKey` luôn có mặt trong response (trong `required`, có thể `null`), cùng cách `logoId`.
7. Bản Library đã soft delete vẫn resolve (A11/#72), cô lập tenant bằng `ShopId` của chính dòng phái sinh.
8. Mỗi lần trả `ShopDto` thêm 1 query nhỏ theo index `ix_media_derivative`; bỏ qua khi `LogoId` null.

## Câu hỏi cần anh quyết

1. `ShopSummaryDto` (`GET /shops`, dùng cho shop switcher) có nên mang logo luôn không? Nếu có: thêm `logoStorageKey`
   vào danh sách (cần gom truy vấn theo lô để tránh N+1) hay để switcher chỉ hiện tên?
2. Chấp nhận phân loại "BREAKING" của công cụ là additive thực chất (field nullable) để promote `shop.v1` không?

## Người duyệt đã quyết

| # | Mục | Quyết định | Ngày |
|---|---|---|---|
