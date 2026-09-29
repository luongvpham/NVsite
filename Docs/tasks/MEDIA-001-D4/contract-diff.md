# MEDIA-001-D4 — contract diff

## Phân loại (bản cuối, sau khi người duyệt lần 1)

Công cụ báo **BREAKING** vì `logoUrl` là field response luôn có mặt (nằm trong `required`). Thực chất là **additive**:
field mới `nullable: true`, không xoá/đổi field cũ, không đổi request, không đổi status code. Người duyệt đã chấp nhận
phân loại này (quyết định #2 bên dưới). Chưa deploy production nên không tạo `v2` (backend/CLAUDE.md).
`media` và `identity`: không đổi.

**Field mới** — `logoUrl: string | null` trên:
- `ShopDto` — `POST /shops`, `GET /shops/{shopId}`, `PATCH /shops/{shopId}`;
- `ShopSummaryDto` — `GET /shops` (danh sách cho shop switcher).

Ngữ nghĩa: `"/media/" + storageKey` của phái sinh `320x96,inside` của logo shop (vd. `/media/shops/{shopId}/{guid}.webp`),
đường dẫn **tương đối theo domain** (dùng được trên mọi host). `null` khi `logoId` null HOẶC không tìm thấy phái sinh.
`logoId` giữ nguyên trên `ShopDto`. DB vẫn chỉ lưu key tương đối. Không còn field `logoStorageKey`.

⚠️ Công cụ `contract:diff` KHÔNG liệt kê `GET /shops` (response là mảng, nó không đi vào schema phần tử) — nhưng
staging `shop.v1.json` có `ShopSummaryDto.logoUrl` (required, nullable). Đã kiểm bằng Grep, không phải bỏ sót.

**Auth:** không đổi (POST/GET/PATCH giữ nguyên policy/filter; `ShopId` chỉ từ route/TenantContext, #21.4;
`GET /shops` vẫn chỉ liệt kê shop mà chính user là thành viên `Active`).

## ⚠️ BREAKING / REMOVED (đầu ra công cụ)
- **BREAKING** `POST /shops` (shop.v1.json) — chưa deploy production thì cứ sửa BE + regen FE, KHÔNG tạo v2:
  - + field 'logoUrl' (required)
- **BREAKING** `GET /shops/{shopId}` (shop.v1.json) — chưa deploy production thì cứ sửa BE + regen FE, KHÔNG tạo v2:
  - + field 'logoUrl' (required)
- **BREAKING** `PATCH /shops/{shopId}` (shop.v1.json) — chưa deploy production thì cứ sửa BE + regen FE, KHÔNG tạo v2:
  - + field 'logoUrl' (required)

## NEW_ENDPOINT
(không có)

## ADDITIVE
(không có)

## UNCHANGED
19 operation không đổi (không tính `GET /shops`, xem cảnh báo ở trên).

## Giả định tôi đã tự đặt (không hỏi)

1. URL là đường dẫn **tương đối theo domain** `/media/{storageKey}` (không có scheme/host), khớp route `/media/*` phục vụ trên mọi host.
2. Không tìm thấy phái sinh (chưa sinh, bị soft delete, lệch cấu hình) -> `null`, **không ném** và không log lỗi.
3. Một preset header cứng `320x96,inside` (`LogoPresets.Header`); test khẳng định catalog `For("Shop")` chứa nó.
4. Nhiều dòng thoả điều kiện -> lấy dòng đầu theo `CreatedAt`, rồi `Id`.
5. `logoUrl` luôn có mặt trong response (trong `required`, có thể `null`), cùng cách `logoId`.
6. Bản Library đã soft delete vẫn resolve (A11/#72), cô lập tenant bằng `ShopId` của chính dòng phái sinh.
7. `GET /shops` tra logo **một câu SQL** cho cả danh sách (join `Shop (Id, LogoId)` với `MediaAsset`), không N+1; shop không có logo/phái sinh -> `null`.
8. Port `IShopLogoReader.GetLogoUrlsAsync` nhận danh sách `ShopId` (không nhận cặp `(ShopId, LogoId)`): `LogoId` lấy từ bảng `Shop` ngay trong query để cặp được ép bằng join.
9. Một nguồn duy nhất của scheme URL ở BE: `ImagePaths.MediaPathPrefix` + `ImagePaths.MediaUrl`; `Program.cs` mount `/media` bằng cùng hằng.
10. `ShopDto` (một shop) thêm 1 query nhỏ theo index `ix_media_derivative`, bỏ qua khi `LogoId` null.

## Câu hỏi cần anh quyết

1. `ShopSummaryDto` (`GET /shops`, dùng cho shop switcher) có nên mang logo luôn không? (đã trả lời bên dưới)
2. Chấp nhận phân loại "BREAKING" của công cụ là additive thực chất (field nullable) để promote `shop.v1` không? (đã trả lời bên dưới)

## Người duyệt đã quyết

| # | Mục | Quyết định | Ngày |
|---|---|---|---|
| 1 | 1 | có thêm logo | 2026-09-29 |
| 2 | 2 | chấp nhận | 2026-09-29 |

## Bổ sung sau khi duyệt lần 1 (2026-09-29)

Bản người duyệt xem lần đầu mô tả `ShopDto.logoStorageKey` (key tương đối, không có `/media/`) và chưa đụng `ShopSummaryDto`.
Sau đó người duyệt yêu cầu ba thay đổi, đều đã làm; contract khác so với bản đã duyệt lần đầu ở các điểm sau:

1. **Đổi tên và đổi giá trị:** `ShopDto.logoStorageKey` -> `ShopDto.logoUrl`, giá trị từ `shops/…/x.webp` thành `/media/shops/…/x.webp` (có tiền tố).
   Thay thế phần "key tương đối, API không trả URL có tiền tố" của #88 ban đầu (đã viết đè trong `DECISIONS.md` và `08` §0/§3.6/§5).
2. **Thêm `ShopSummaryDto.logoUrl`** (`GET /shops`) — trước đây `ShopSummaryDto` giữ nguyên. Handler tra theo lô một câu SQL.
3. **Thêm `ImagePaths.MediaPathPrefix` / `ImagePaths.MediaUrl`** (không đổi contract HTTP) làm nguồn duy nhất của prefix `/media`.

Cần xác nhận lại trước khi promote `shop.v1`:

| # | Mục | Quyết định | Ngày |
|---|---|---|---|
