# MEDIA-001-D4 — changelog: lệch so với thiết kế

D4 (Quyết định #88): `ShopDto` trả sẵn `logoStorageKey` = storage key tương đối của phái sinh `320x96,inside`,
để Portal hiện logo shop bằng `mediaUrl(shop.logoStorageKey)` mà không cần tra thêm. Tài liệu đã cập nhật (sửa
tại chỗ): `DesignIdeal/08-media-asset-design.md` §0 + §3.6, `DECISIONS.md`, `00-INDEX.md`, `02`, `05` (dải số
#69–#86 và #88), `backend/docs/modules/{shop,media}.md`, `Docs/tasks/MEDIA-001/brief.md`.

**Đọc kèm:** `Docs/tasks/MEDIA-001-D4/contract-diff.md`.

---

## Lệch có chủ đích (giữ nguyên, không phải bug)

### 1. Port ở Shop, adapter ở Media (đảo chiều so với `IShopLogoWriter`)

- **Vì sao:** `Shop` không được reference `Media` (#1). `IShopLogoReader` khai ở `Application/Shop/Interfaces`,
  adapter `Vsite.Infrastructure.Media.ShopLogoReader` (Media dependsOn Shop). `ModuleBoundaryTests` không đổi.

### 2. Không tìm thấy phái sinh -> `null`, không ném

- Tránh làm hỏng `GET /shops/{id}` chỉ vì thiếu ảnh; FE tự xử lý fallback.

### 3. Cô lập tenant trong CÙNG query, không đọc dòng nguồn

- `IgnoreQueryFilters()` + `ShopId` + `!IsDeleted` + `SourceAssetId` + `Preset`; bản Library soft delete vẫn resolve (A11).

### 4. Preset cứng một hằng

- `LogoPresets.Header = "320x96,inside"`; test `Derivative_preset_catalog_for_Shop_contains_the_header_logo_preset` bắt lệch cấu hình.

---

## Chưa làm xong / còn hở

### 1. Chưa promote contract, chưa sync FE

- Staging `shop.v1.json` có field mới; promote + `pnpm gen:api` + FE dùng `mediaUrl(shop.logoStorageKey)` là bước sau khi người duyệt. Dòng sha256 trong `Docs/tasks/MEDIA-001/brief.md` do controller cập nhật sau promote.

### 2. `ShopSummaryDto` chưa mang logo

- Câu hỏi mở cho người duyệt (xem contract-diff).

### 3. Test

- Handler/adapter: `ShopLogoReaderTests` (7), `ShopDtoLogoHandlerTests` (4) — không cần Docker.
- Endpoint (Docker, đã chạy xanh): `ShopLogoTests` thêm 2 test (null trước logo; sau PUT logo: GET + PATCH trả key, đọc được ở `/media/{key}`).
