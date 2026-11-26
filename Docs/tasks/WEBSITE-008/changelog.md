# WEBSITE-008 — changelog: lệch so với thiết kế (Bước 8d, job nền của builder)

Lane B, chỉ BE, một session, không Gate 1 (không đổi contract). Quyết định: **#117** (dọn ảnh mồ côi), **#118** (giữ dữ liệu builder);
nền: #116 (job theo shop). Plan: `Docs/tasks/WEBSITE-008/plan.md`. Doc nghiệp vụ: `backend/docs/modules/website.md` mục "Job dọn dẹp",
`backend/docs/modules/media.md` §5, `backend/CLAUDE.md` §Job nền. Code: nhánh `feature/WEBSITE-008`, commit `ad99e66`..`d86d04b`.

**Kết quả:** hai job recurring theo shop — `website.retention` (03:30: prune bản publish → purge draft/OG trang xoá → dọn menu mồ côi) và
`media.orphan-cleanup` (04:00: ảnh mồ côi hai bước qua cột mới `MediaAsset.OrphanedAt`). Toàn bộ `IntegrationTests` pass (1121 pass / 1 skip) trên
Docker thật; `ArchitectureTests` pass.

## Lệch có chủ đích (giữ nguyên, không phải bug)

### 1. FK tới `media_asset` là `SET NULL`, không phải `Restrict` — plan sai tiền đề (người chốt 2026-11-26)

- **Plan nói gì:** §2 / phát hiện 10 / R6 giả định `Page.OgImageId` (và OG/favicon của Website) là FK `Restrict` ⇒ ảnh OG của trang đã xoá mềm
  sẽ chặn xoá ảnh; T3 vì thế gỡ `OgImageId` để ảnh xoá được; R6 test bằng FK thật chặn.
- **Thực tế:** `page.og_image_id`, `website.default_og_image_id`, `website.favicon_asset_id` → `media_asset` là **`ON DELETE SET NULL`**
  (migration `20261015151020_AddWebsiteModule`). FK `Restrict` duy nhất tới `media_asset` là **logo shop** (`Shop.LogoId`, InitialSchema).
- **Quyết định:** giữ hành vi, sửa tài liệu. Hệ quả: ảnh OG của trang đã xoá **không** chặn xoá; job xoá nó sau mark + grace và DB tự đặt con
  trỏ về null (lúc đó trang đã xoá ≥ 7 ngày và T3 cũng đã gỡ). Lý do T3 "gỡ `OgImageId` để ảnh xoá được" thành **không cần cho khả năng xoá**,
  nhưng giữ vì vô hại và gọn (không có tính năng khôi phục trang). Test R6 dùng một FK `RESTRICT` tạm để chứng minh đường rollback-khi-FK-giữ.
  An toàn của việc xoá ảnh OG/favicon vì thế dựa hoàn toàn vào tập "ảnh đang dùng" (`SeoImageUsageSource`), không dựa vào FK.
- **Sửa ở:** `DECISIONS.md` #117 (câu "FK còn giữ"), comment `DeletedPagePurger`, `MediaOrphanCleanupJob`, `media.md` §5.

### 2. Cấu hình job theo shop đi qua options, không qua `RunAsync` (phát hiện 9)

- **Tài liệu cũ** (`backend/CLAUDE.md` §Job nền) nói tham số cấu hình đi vào job qua `RunAsync`. `IShopJob` không nhận tham số nên không dùng được.
- **Thực thi:** options ở `Application/{Module}/Options/` (`BuilderRetentionOptions` `Jobs:BuilderRetention`, `MediaCleanupOptions` `Jobs:MediaCleanup`),
  bind bằng helper `AddJobOptions` trong `AddInfrastructure`, `ValidateOnStart`. `RunAsync` params chỉ còn cho job Identity không tenant. Đã sửa câu và thêm bảng cấu hình.

### 3. Ba việc của Website gộp một job; job ảnh riêng

- `WebsiteRetentionJob` (prune → purge → clean) 03:30; `MediaOrphanCleanupJob` 04:00 để thấy kết quả prune cùng ngày. Mỗi bước là service riêng
  (`PublicationPruner`, `DeletedPagePurger`, `NavigationOrphanCleaner`), job chỉ điều phối.

### 4. Prune xoá theo ngưỡng `Version`, không theo danh sách Id

- Predicate: `Version <` (Version nhỏ nhất của các bản non-current được giữ) và `Id != current`. Publish đồng thời (bản mới hơn) không bị chạm.
  Không có current ⇒ giữ N bản mới nhất; ít hơn N ứng viên ⇒ thoát sớm không ghi.
- Rollback chen vào khoảng giữa tính ngưỡng và xoá ⇒ FK abort + Hangfire retry, không mất dữ liệu. Rollback về bản vừa bị prune ⇒ `404 PUBLICATION_NOT_FOUND`
  (bắt FK `23503` qua `UniqueViolation.IsForeignKeyViolation`), không 500 (phát hiện 8).

### 5. `listPublications` hạ trần 50 → 20

- Contract không ghi trần nên **không đổi contract**. Trần bằng số bản prune giữ.

### 6. Purge trang đã xoá: không xoá dòng `Page`; không đụng `UpdatedAt`

- Chỉ xoá cứng `PageDraft` + gỡ `OgImageId` bằng `ExecuteUpdate`/`ExecuteDelete` (không qua interceptor soft-delete). `IgnoreQueryFilters()` thêm một file vào allowlist
  (`IgnoreQueryFiltersAllowlistTests`), `ShopId` tường minh trong mọi predicate.

### 7. Dọn menu: dùng chung hàm với đường đọc

- `NavigationItems.DropOrphans` dùng chung với `Merge` (cùng ngữ nghĩa lúc đọc). `NavigationConfig.PruneOrphans` **không** bật `IsDirty`.
  Lần ghi của job cập nhật `UpdatedAt` ⇒ cửa sổ yên lặng 24h khởi động lại (vô hại). Concurrency ⇒ bỏ lượt, log, không retry ngay.

### 8. Nguồn "ảnh đang dùng": interface ở Media, cài ở Website

- `IMediaUsageSource` (Media) + 3 hiện thực ở `Infrastructure/Website` (`PageDraftUsageSource`, `SitePublicationUsageSource`, `SeoImageUsageSource`), mẫu `IMediaReferenceSource`;
  Media không đọc bảng Website (#1). Dùng chung `PageTreeImageIds`/`SnapshotMediaKeys` với chế độ **strict** cho đường usage: lỗi validate/snapshot thiếu media ⇒ **ném**
  thay vì trả danh sách thiếu (trả thiếu = xoá nhầm ảnh đang dùng). Thiếu tenant cũng ném.

### 9. `IAppDbContext` có thêm `BeginTransactionAsync`

- Chỉ lộ `Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken)` (không lộ `DatabaseFacade` — Application không có SQL thô vượt filter tenant; sửa sau review toàn nhánh). `AppDbContext` ủy quyền `Database.BeginTransactionAsync`; decorator test forward.
  Dùng để mở transaction theo từng ảnh trong `MediaOrphanCleanupJob`. Xoá cứng bằng `ExecuteDelete` kiểm lại `ShopId`/`Id`/`OrphanedAt < cutoff`; Library ⇒ thêm `IsDeleted`.
  File vào allowlist `IgnoreQueryFilters()` thêm một file nữa.

### 10. Đánh dấu bằng cột `OrphanedAt` (migration), không dùng `IsDeleted`

- `MediaAsset.OrphanedAt` + index `ix_media_asset_orphaned` (`ShopId, OrphanedAt`). `MediaReferenceValidator` **bỏ qua** `OrphanedAt` ⇒ undo/copy node đưa ảnh về draft
  không bị chặn (R4); lần quét sau bỏ đánh dấu. `MarkOrphaned` ném `MEDIA_ORPHAN_LIVE_LIBRARY` với Library còn sống.

## Chưa làm xong / giới hạn đã biết

1. **Cửa sổ race còn lại khi xoá ảnh.** Job gom lại tập "đang dùng" ngay trước vòng xoá (commit `7054fa9`, người chọn "đóng nếu rẻ"). Còn hở: ảnh được dùng lại **đúng trong vòng lặp xoá**,
   sau khi đã không dùng ≥ 2 × grace (đánh dấu + chờ). Hẹp, không có cách đóng rẻ hơn mà không khoá draft.
2. **Strict walker làm kẹt job ảnh của shop.** Một draft sau đó fail validation (vd. catalog component đổi) ⇒ nguồn usage ném ⇒ job ảnh của shop đó dừng, Hangfire retry tới khi sửa.
   An toàn (không xoá gì) và thấy được qua log, nhưng ảnh của shop đó không được dọn trong thời gian ấy.
3. **`OrphanGrace = 0`** được validate (≥ 0) nhưng đánh dấu ngay clone picker mới tạo chưa vào draft ⇒ đừng đặt 0 ở production (đã cảnh báo ở `backend/CLAUDE.md`).
4. **MEDIA-001 #10 còn mở:** `UploadShopLogoHandler` để lại file mồ côi khi shop biến mất giữa lúc kiểm tra và lúc ghi. File đó **không có record** nên job quét theo record không thấy.
   (MEDIA-001 #3 "job dọn mồ côi" đã xong ở đây.) Cần dọn theo storage hoặc sửa handler.
5. Truy vấn ban đầu liệt kê media của shop **không phân trang** (7 cột hẹp); đánh dấu/bỏ đánh dấu mới batch 500. Đủ cho shop nhỏ; xem lại nếu có shop hàng chục nghìn ảnh.
6. `references` của Media vẫn là `IMediaReferenceSource` riêng (cảnh báo khi xoá); chưa hợp nhất với `IMediaUsageSource`.
7. **⚠️ Quy tắc bắt buộc cho mọi task sau:** entity/JSON nào lưu id `MediaAsset` (vd. ảnh `Service`/`ServiceGroup`, bước 9) **phải đăng ký một `IMediaUsageSource` trong cùng task**.
   Không đăng ký thì `MediaOrphanCleanupJob` thấy ảnh "không dùng", đánh dấu rồi xoá cứng (Clone/Direct/Derivative và Library đã xoá mềm là ứng viên bất kể module sở hữu).
   Việc theo dõi (tuỳ chọn): test liệt kê FK tới `media_asset` từ `information_schema` và so với danh sách đã được nguồn usage phủ — FK mới chưa có nguồn thì đỏ.
8. `PublicationsToKeep = 1`: publish đồng thời dựa vào FK abort + retry (bản mới chưa là current khi job chạy ⇒ có thể bị prune ⇒ FK/predicate chặn, publish retry). Chấp nhận.
9. `ListPublicationsHandler.MaxItems = 20` là hằng cứng, còn `PublicationsToKeep` cấu hình được: đặt keep > 20 thì UI không thấy bản dư.
10. Sửa sau review toàn nhánh: nguồn usage ném ⇒ không XOÁ gì; lần thu thập thứ hai (trước vòng xoá) ném thì các cập nhật đánh dấu/bỏ đánh dấu đã lưu được giữ (không phá huỷ).

## Giả định tôi đã tự đặt

1. Ba việc Website gộp một job 03:30; job ảnh 04:00 (mục lệch 3).
2. Cấu hình qua options ở Application, section `Jobs:BuilderRetention` / `Jobs:MediaCleanup` (mục lệch 2). `PublicationsToKeep` tối thiểu 1.
3. Grace draft trang đã xoá = 7 ngày như ảnh nhưng là khoá riêng `DeletedPageGrace`.
4. Purge trang đã xoá gỡ luôn `OgImageId`, không xoá cứng dòng `Page` (mục lệch 1: không còn cần cho khả năng xoá ảnh).
5. Phái sinh được coi là đang dùng khi bản gốc đang dùng hoặc là Library sống; bản gốc mất ⇒ ứng viên.
6. Nguồn "đang dùng" lỗi ⇒ job ảnh của shop đó dừng hẳn, ném để Hangfire retry (R5).
7. `listPublications` trần 20 (không đổi contract).
8. Job ghi menu gặp concurrency ⇒ bỏ lượt, không retry ngay.
9. Không có bản current ⇒ prune giữ N bản mới nhất (không xoá hết).
10. Race "ảnh dùng lại giữa chừng" chỉ đóng bằng gom lại tập dùng trước vòng xoá, không khoá draft.

## Quy trình

- TDD lệch thứ tự ở T4 và T6 (code trước test; T6 kiểm bằng mutation). Sự cố nhỏ: implementer T3 chạy `git stash pop` — đã kiểm tra, không mất dữ liệu (stash bị bỏ `00a498f` giống HEAD).
- **Docker:** máy session này **có Docker**, mọi test Testcontainers đã chạy thật ⇒ `Docs/DOCKER-TEST-DEBT.md` **không** có mục mới (vẫn rỗng).
- Không đụng `contracts/` hay file generated.
- **Theo dõi test flaky:** lần chạy full IntegrationTests ở T7 fail một lần `JobsDashboardTests` ×2 và `ShopFanOutWiringTests` ×1, không tái hiện ở T8 (full run 1121 pass). Lần chạy full kế tiếp để ý ba test này.

## Test giữ các ràng buộc

| Ràng buộc | Test |
|---|---|
| Prune giữ 20 gồm current, rollback về bản cũ, rollback bản vừa prune ⇒ 404 | `PublicationPrunerTests`, `PublicationPrunerSiteTests` |
| Purge draft/OG trang xoá > 7 ngày, shop B không đổi | `DeletedPagePurgerTests` |
| Menu < 24h không bị ghi, không `IsDirty`, mục mồ côi bị bỏ | `NavigationOrphanCleanerTests`, `NavigationItemsTests` |
| Nguồn "đang dùng" (draft, snapshot, OG/favicon, strict) | `MediaUsageSourceTests`, `PageDraftReferenceSourceTests`, `PublicationReferenceSourceTests` |
| Hai bước, Library sống, R4–R8, tenant | `MediaOrphanCleanupJobTests`, `MediaAssetTests` |
| Lịch + e2e hai job | `RecurringJobsTests`, `WebsiteJobsEndToEndTests` |
| `IgnoreQueryFilters()` chỉ ở file allowlist; Media không đọc Website | `IgnoreQueryFiltersAllowlistTests`, `ModuleBoundaryTests` |
