# MEDIA-001-D3 — changelog: lệch so với thiết kế

D3 thêm `GET /shops/{shopId}/media/library/{assetId}/derivatives?preset=` để Portal tra (bản Library,
preset) -> phái sinh, lấp khoảng trống contract: `Shop.LogoId` lưu id bản Library (#73) nhưng
`GET .../media/assets?ids=` chỉ tra trực tiếp theo id. Đây là phơi ra cho Portal lookup mà #73 đã định
nghĩa, không cần quyết định `#N` mới. Tài liệu đã cập nhật (sửa tại chỗ): `DesignIdeal/08-media-asset-design.md`
§3.6 + §9, `backend/docs/modules/media.md`.

**Đọc kèm:** `Docs/tasks/MEDIA-001-D3/contract-diff.md`, `Docs/tasks/MEDIA-001/plan.md`.

---

## Lệch có chủ đích (giữ nguyên, không phải bug)

### 1. Không đọc dòng nguồn — cô lập tenant bằng ShopId của dòng phái sinh

- **Vì sao:** yêu cầu bản Library đã soft delete (kể cả là `Shop.LogoId`, A11 / #72) vẫn resolve.
  `IgnoreQueryFilters()` + `ShopId == route` + `!IsDeleted` viết tay trên CHÍNH các dòng phái sinh.
- **Hệ quả:** id lạ, id của shop khác, id không có phái sinh đều cho `200 []` (không 404), không lộ
  sự tồn tại. Khác `clones` (404 khi nguồn không hợp lệ).

### 2. Không từ chối tên preset lạ

- Validator chỉ chặn `preset` > 40 ký tự (kích thước cột); tên preset không có trong catalog trả `[]`.

---

## Chưa làm xong / còn hở

### 1. Chưa có promote contract và chưa sync FE

- Staging `contracts/openapi/.staging/media.v1.json` đã có operation mới; promote + `pnpm gen:api` +
  dùng endpoint ở FE (logo shop qua `320x96,inside`) là bước riêng sau khi người duyệt.

### 2. Endpoint test cần Docker

- `GetDerivativesEndpointTests` chạy qua Testcontainers; đã chạy xanh trên máy có Docker (5 test).
