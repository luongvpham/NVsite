# MEDIA-001-D4 — changelog: lệch so với thiết kế

D4 (Quyết định #88): `ShopDto` **và `ShopSummaryDto`** trả sẵn `logoUrl` = `/media/{storageKey}` của phái sinh
`320x96,inside`, để Portal hiện logo bằng `<img src={shop.logoUrl}>` mà không cần tra thêm. Tài liệu đã cập nhật (sửa
tại chỗ): `DesignIdeal/08-media-asset-design.md` §0 + §3.6 + §5, `DECISIONS.md`, `00-INDEX.md`, `02`, `05` (dải số
#69–#86 và #88), `backend/docs/modules/{shop,media}.md`, `Docs/tasks/MEDIA-001/brief.md`.

**Thay đổi sau khi người duyệt lần 1 (2026-09-29):** đổi `logoStorageKey` (key tương đối) thành `logoUrl` (có tiền tố
`/media/`); thêm `logoUrl` cho `ShopSummaryDto` (`GET /shops`) với truy vấn theo lô; thêm `ImagePaths.MediaUrl` +
`MediaPathPrefix` làm nguồn duy nhất của scheme URL. Bản này THAY thế phần "key tương đối, không bao giờ URL có tiền tố" của #88 ban đầu.

**Đọc kèm:** `Docs/tasks/MEDIA-001-D4/contract-diff.md`.

---

## Lệch có chủ đích (giữ nguyên, không phải bug)

### 1. Port ở Shop, adapter ở Media (đảo chiều so với `IShopLogoWriter`)

- **Vì sao:** `Shop` không được reference `Media` (#1). `IShopLogoReader` khai ở `Application/Shop/Interfaces`,
  adapter `Vsite.Infrastructure.Media.ShopLogoReader` (Media dependsOn Shop). `ModuleBoundaryTests` không đổi.
- Port trả URL đã dựng (adapter gọi `ImagePaths.MediaUrl`) nên handler Shop không biết scheme URL và không lặp logic.

### 2. Không tìm thấy phái sinh -> `null`, không ném

- Tránh làm hỏng `GET /shops/{id}` hay `GET /shops` chỉ vì thiếu ảnh; FE tự xử lý fallback.

### 3. Cô lập tenant trong CÙNG query, không đọc dòng nguồn

- `IgnoreQueryFilters()` + `ShopId` + `!IsDeleted` + `SourceAssetId` + `Preset`; bản Library soft delete vẫn resolve (A11).

### 4. Bản theo lô: join với `Shop`, một câu SQL

- `GetLogoUrlsAsync(shopIds)` join `Shop (Id, LogoId)` với `MediaAsset` nên cặp (ShopId, SourceAssetId) được ép trong query;
  vì vậy port nhận danh sách `ShopId` (không nhận cặp `(ShopId, LogoId)` như phác thảo ban đầu — `LogoId` lấy từ chính bảng `Shop`).
  Adapter (Infrastructure.Media) đọc `db.Shops`: hợp lệ vì Media dependsOn Shop. Chứng minh 1 lệnh SQL: `ShopLogoBatchSqlCountTests`.

### 5. Preset cứng một hằng

- `LogoPresets.Header = "320x96,inside"`; test `Derivative_preset_catalog_for_Shop_contains_the_header_logo_preset` bắt lệch cấu hình.

### 6. Route mount và URL dùng chung một hằng

- `ImagePaths.MediaPathPrefix` (`/media`) do `Program.cs` dùng để mount và `ImagePaths.MediaUrl` dùng để dựng URL;
  `ImagePathsMediaUrlTests` kiểm định dạng và kiểm `Program.cs` dùng đúng hằng (kiểm bằng đọc source), còn `ShopLogoTests`
  GET đúng `logoUrl` và nhận 200.

---

## Chưa làm xong / còn hở

### 1. Chưa promote contract, chưa sync FE

- Staging `shop.v1.json` có field mới (`ShopDto.logoUrl`, `ShopSummaryDto.logoUrl`); promote + `pnpm gen:api` + FE dùng
  `<img src={shop.logoUrl}>` là bước sau khi người duyệt xác nhận lại. Dòng sha256 trong `Docs/tasks/MEDIA-001/brief.md` do controller cập nhật sau promote.

### 2. Test

- Handler/adapter (không Docker): `ShopLogoReaderTests`, `ShopDtoLogoHandlerTests`, `ImagePathsMediaUrlTests`.
- Docker: `ShopLogoTests` (GET/PATCH/GET /shops), `ShopLogoBatchSqlCountTests` (1 lệnh SQL).
