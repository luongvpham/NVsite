# REFACTOR-DB-001 — changelog

Refactor schema trước khi có dữ liệu production, rút từ đợt review kiến trúc 2026-10-08 (P0 #4, #5,
P1 `DeletedAt`, unique slug/soft delete). Nhánh tách từ `feature/REFACTOR-BE-001`.

- **Lane:** A. Contract không đổi (`contract-diff.md`: 22 operation UNCHANGED).
- **Schema:** đổi toàn bộ — migration gộp lại một `InitialSchema` mới.
- **Banner đã cập nhật:** `DesignIdeal/08` (tên bảng/index, `Kind`, `DeletedAt`) và `DesignIdeal/05`
  (tên bảng `media_asset`).

> ⚠️ **DB dev cũ phải xoá.** Migration cũ (`20260914081959_InitialSchema`, `20260926143402_AddMediaAsset`)
> đã bị thay bằng `20261008225559_InitialSchema`. DB đã áp migration cũ sẽ không migrate tiếp được —
> lỗi sẽ là `column "migration_id" does not exist`. Chạy `docker compose down -v` rồi khởi động lại
> (volume `vsite_postgres_data`).

---

## Đã làm

### 1. Toàn bộ schema chuyển sang snake_case

- **Trước:**
  - Bảng/cột dạng PascalCase có quote (`"MediaAsset"."IsInLibrary"`).
  - Tên ràng buộc lẫn hai kiểu: `PK_`/`IX_`/`FK_` của EF và `ck_`/`ix_` viết tay.
  - Tài liệu thì viết snake_case.
- **Sau:**
  - Gói `EFCore.NamingConventions 9.0.0`, bật trong `AppDbContext.OnConfiguring`. Không bật ở
    `AddDbContext`, vì test tự dựng context bằng `UseNpgsql` riêng.
  - Bảng số ít: `shop`, `user_shop`, `media_asset`, `role`, `external_login`, `refresh_token`,
    `password_reset_token`, `pending_registration`.
  - Mọi PK/FK/index tự động thành snake_case (`pk_shop`, `fk_shop_media_asset_logo_id_id`,
    `ix_shop_slug`…).
  - 4 FK nối tới `app_user`/`shop` được ghim tên bằng `HasConstraintName`
    (`fk_{bảng}_app_user_user_id`, `fk_media_asset_shop_shop_id`). Lý do: NamingConventions đặt tên
    theo thứ tự cấu hình và từng lấy tên DbSet (`users`/`shops`).
- **`User` → bảng `app_user`:** `user` là từ khoá Postgres. `SELECT * FROM user` không báo lỗi mà
  trả về `current_user`, nên là bẫy cho mọi SQL viết tay.
- **SQL viết tay** trong CHECK/filter và trong test đã chuyển sang tên không quote. Tên CHECK giữ
  nguyên (`ck_media_library_preset`, `ck_shop_external_url`, `ck_user_*`).
- **Đổi tên index:**
  - `ix_media_library` → `ix_media_asset_library`
  - `ix_media_derivative` → tách thành `ux_media_asset_derivative` và `ix_media_asset_source` (mục 2)
- **Bảng `__EFMigrationsHistory`:** giữ tên, nhưng cột thành `migration_id`/`product_version`. Đây
  là hành vi của NamingConventions, nhất quán vì mọi context đều bật cùng convention.

### 2. `MediaAsset.Kind` — tách Clone khỏi Derivative (bug thật)

- **Trước:**
  - Clone (đặt vào slot, #71/#72) và phái sinh (logo, #73) có cùng bộ
    `IsInLibrary=false, Preset, SourceAssetId`.
  - `ShopLogoReader` và `GetDerivativesHandler` lấy dòng cũ nhất khớp `(SourceAssetId, Preset)`.
  - Một clone cũ hơn cùng preset `320x96,inside` sẽ làm logo hiện crop của slot.
  - Job dọn clone không tham chiếu (#72, Bước 8) sẽ không phân biệt được hai loại.
- **Sau:**
  - Enum `MediaAssetKind { Library, Direct, Clone, Derivative }`, lưu dạng string (#19).
  - `NewDerived` tách thành `NewClone` và `NewDerivative`. `MediaAssetWriter.WriteDerivedAsync`
    tách thành `WriteCloneAsync` (slot upload, clone-from-library) và `WriteDerivativeAsync` (logo).
  - Các query đọc:
    - `ShopLogoReader` (đơn + theo lô) và `GetDerivativesHandler` lọc `Kind == Derivative`.
    - `GetUsageHandler` lọc `Kind IN (Library, Direct)` thay vì `SourceAssetId IS NULL`. Lý do: FK
      là `ON DELETE SET NULL`, nên clone mất nguồn sẽ bị tính nhầm vào quota.
  - `MediaReferenceValidator` (Public Contract cho Bước 5): tree chỉ nhận `Kind` Clone/Direct,
    business chỉ nhận Library. Phái sinh (logo) không còn lọt vào tree.
  - Ràng buộc DB:
    - `ck_media_asset_kind`: `kind` thuộc tập đóng, `(kind = 'Library') = is_in_library`, bản gốc
      (Library/Direct) không có `source_asset_id`.
    - `ux_media_asset_derivative`: unique `(source_asset_id, preset)` khi
      `kind = 'Derivative' AND NOT is_deleted`. Clone không bị ràng buộc này.
    - `ix_media_asset_source`: mọi clone/phái sinh theo nguồn (references, dọn rác).
- **Không đưa `Kind` ra `MediaAssetDto`** để giữ contract.

### 3. `DeletedAt` trên mọi `BaseAuditableEntity`

- `AppDbContext` đóng dấu khi `IsDeleted` chuyển sang true: qua `Remove()`, khi entity tự set
  `IsDeleted` (vd. `MediaAsset.SoftDeleteFromLibrary`), và khi insert sẵn ở trạng thái đã xoá. Xoá
  lần hai giữ mốc đầu. Khôi phục (`IsDeleted = false`) thì xoá giá trị.
- Khớp lại `08` §2. A3 của MEDIA-001 ("chỉ `IsDeleted`") hết hiệu lực.

### 4. Slug của shop đã xoá mềm: 409 thay vì 500

- **Trước:** unique index `shop.slug` không lọc soft delete, nhưng `CreateShopHandler`/`UpdateShopHandler`
  kiểm trùng qua Global Query Filter. Slug của shop đã xoá mềm qua được bước kiểm rồi đụng DB, trả 500.
- **Sau:** kiểm trùng dùng `IgnoreQueryFilters()` (Shop không tenant-scoped, chỉ tắt filter
  soft-delete) và trả `409 SHOP_SLUG_ALREADY_TAKEN`.
- **Không phải quyết định mới:** unique index `shop.slug` *vốn đã* giữ slug của shop đã xoá mềm. Task
  này chỉ đổi mã lỗi từ 500 sang 409. Muốn **giải phóng** slug sau khi xoá thì mới là quyết định
  mới (ảnh hưởng URL/SEO, `04` §2.1, cùng lúc với `shop_slug_history`), cần người chốt.

## Kiểm chứng

Máy không có Docker daemon, nhưng có cài PostgreSQL 16.4. Mình dựng một cluster tạm (`initdb` trong
scratchpad, cổng 55432, không đụng service Postgres của máy) và vá **tạm** `PostgresFixture` cùng 3
`*ApiFactory`: đọc connection string từ biến môi trường, thay Redis bằng
`AddDistributedMemoryCache`. Bản vá đã hoàn lại, không commit.

- **Kết quả (lần chạy cuối, sau vòng sửa theo review, migration `20261008225559_InitialSchema`):** **284 pass / 12 fail / 1 skip** trên toàn bộ IntegrationTests.
  - Cả 12 fail đều cần Docker thật: 11 test `S3ObjectStorageTests` (LocalStack) và 1 test
    `ShopLookupCacheTests` (dừng container Postgres để thử cache Redis).
  - Test endpoint 403 non-Owner của REFACTOR-BE-001 nằm trong số pass.
- **Không thay được Testcontainers ở hai điểm:** Redis thật (đã thay bằng cache in-memory) và image
  `postgres:16-alpine` (đã dùng Postgres 16.4 cài trên Windows).
- **Test mới:**
  - Postgres thật: `MediaDbConstraintTests.Check_constraint_rejects_kind_inconsistent_with_is_in_library`,
    `MediaDbConstraintTests.Unique_index_allows_many_clones_but_one_live_derivative_per_source_and_preset`.
  - InMemory: `GetDerivativesHandlerTests.Clone_with_same_source_and_preset_is_not_returned`,
    `ShopLogoReaderTests.Older_clone_with_header_preset_is_ignored_in_single_and_batch_lookup`,
    `SoftDeletedMembershipTests.CreateShop_with_slug_of_soft_deleted_shop_is_conflict`.
  - Unit: `MediaAssetTests.Each_factory_stamps_its_Kind`.
  - Sau review: `MediaReferenceValidatorTests.Derivative_id_in_tree_list_throws`,
    `LibraryHandlerTests.Usage_ignores_clone_whose_source_was_hard_deleted`,
    `AuditStampingTests.DeletedAt_is_stamped_on_every_soft_delete_path_and_cleared_on_restore`,
    `SoftDeletedMembershipTests.UpdateShop_to_slug_of_soft_deleted_shop_is_conflict`.

---

## Chưa làm / để task khác

1. **Chưa thêm concurrency token `xmin`** (đề xuất P1 của review). Lý do:
   - Không có version do client gửi lên (ETag/`If-Match` hoặc field `version` trong DTO), nên `xmin`
     chỉ bắt được race trong vài ms giữa đọc và ghi của cùng một request.
   - Gắn rộng thì làm hỏng các luồng ghi đồng thời hợp lệ (login cập nhật `User`, xoay refresh token).
   - Nên làm cùng `PageDraft` (Bước 5, autosave): thiết kế version trong contract, map
     `DbUpdateConcurrencyException` → 409.
2. **Chưa có map chung `23505 unique_violation` → 409.** Hiện chỉ slug được kiểm trước. Unique
   khác (vd. hai request tạo shop cùng slug đồng thời) vẫn ra 500 trong race hiếm.
3. **Tài liệu thiết kế cho Bước 5 trở đi** chưa sửa: `ShopId` + FK ghép cho mọi bảng con
   (`Page`/`PageDraft`/`SitePublication`/`ProductVariant`…), địa chỉ hành chính 2 cấp từ 07/2025,
   PK của `ProductAttributeValue`, `schemaVersion` cho tree/snapshot. Đây là task riêng, chỉ sửa tài
   liệu (review mục P0 #6, #7, P1).
4. **Guid v7, bảng `shop_slug_history`, outbox:** P1/P2 của review, chưa làm.

## Giả định tôi đã tự đặt

> ⚠️ **Cần người duyệt:** `DesignIdeal/DECISIONS.md` dòng #55 (công thức quota viết lại theo `Kind`,
> ý nghĩa không đổi: clone/phái sinh không tính) và dòng #69 (ghi chú cột `Kind` là vai trò kỹ thuật,
> không phải `Purpose`) đã được sửa cho khớp code.

- Chọn **snake_case** (thay vì giữ PascalCase rồi sửa tài liệu) theo khuyến nghị đã nêu khi review.
  Lý do: SQL thô, job, indexer Elasticsearch và script vận hành không phải quote mọi tên.
- **Gộp migration** thay vì thêm migration đổi tên: chưa có production. Đổi lại, DB dev phải xoá.
- Đặt tên bảng `app_user` thay vì `user`.
- Slug của shop đã xoá mềm **không** được dùng lại.
