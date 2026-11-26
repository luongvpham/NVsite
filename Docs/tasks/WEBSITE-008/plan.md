# WEBSITE-008 — Job của builder (Bước 8d)

> **Đã chốt 2026-11-26 — ĐÃ CODE (nhánh `feature/WEBSITE-008`)**
>
> ⚠️ Tiền đề FK `Restrict` của finding 10 / R6 / §3 Flow 4 **sai**: FK OG/favicon là `SET NULL`, chỉ `Shop.LogoId` là `Restrict`. Sửa ở
> [`changelog.md`](changelog.md) §1 — đọc đó trước, đừng tin các đoạn plan nói FK chặn xoá ảnh đang dùng.
>
> **Lane B (không chạm contract).** Chỉ BE, một session S1; có migration (`MediaAsset.OrphanedAt`) và đi qua
> Website + Media nên vẫn có plan. Không có màn hình ⇒ không `data-needs.md`. Contract `listPublications` không ghi trần
> ⇒ không Gate 1. Cổng: Gate 2.
>
> **Người thực thi dùng skill:** `superpowers:subagent-driven-development` hoặc
> `superpowers:executing-plans`. Mỗi bước có checkbox `- [ ]`.

**Mục tiêu:** dữ liệu website shop không phình mãi: mỗi website chỉ giữ một số bản publish gần nhất, file ảnh
không còn ai dùng được dọn sau một thời gian chờ, menu và draft không còn rác trỏ tới trang đã xoá. Mọi job
chạy nền theo lịch, theo từng shop (#116).
**Spec:** `DesignIdeal/step.md` §8d · `backend/docs/modules/media.md` §5 (dọn file) · `backend/CLAUDE.md` §Job nền
· **Quyết định liên quan:** #117, #118 (mới), #116, #55, #72, #71, #21, #1, #115

## 1. Yêu cầu (đã chốt với người 2026-11-25)

- Người dùng / vai trò: không có người bấm — job nền theo lịch. Người vận hành thấy job ở dashboard Hangfire (8a).
- Điểm chạm: 4 job — (1) prune `SitePublication`, (2) dọn file media mồ côi, (3) dọn mục menu mồ côi,
  (4) dọn draft của trang đã xoá mềm.
- Xong nghĩa là (step.md §8d): website có hơn 20 bản thì còn đúng 20 và bản current vẫn còn; ảnh không còn bản
  publish nào tham chiếu thì file bị dọn, ảnh còn được tham chiếu thì không.
- **Không làm trong task này:** UI/màn hình · đổi contract · xoá cứng `Page` đã xoá mềm · khôi phục trang · quét
  storage tìm file không có record (MEDIA-001 #10) · dọn ảnh Listing/Product (thư mục entity, M/10) · nguồn "ảnh đang dùng"
  của `Service`/`ServiceGroup` (Bước 9 tự cài thêm nguồn) · sửa `DeletePageHandler` để xoá draft ngay

## 2. Hiện trạng (khảo sát 2026-11-25)

| Khả năng | Trạng thái | Ở đâu | Ghi chú |
|---|---|---|---|
| Khung fan-out theo shop (#116) | 🟡 | [ShopFanOut.cs:13](../../../backend/src/Vsite.Infrastructure/Jobs/ShopFanOut.cs#L13), [ShopJobRunner.cs:14](../../../backend/src/Vsite.Infrastructure/Jobs/ShopJobRunner.cs#L14), [IShopJob.cs:7](../../../backend/src/Vsite.Application/Common/Jobs/IShopJob.cs#L7) | Code có, nhưng **chưa đăng ký DI ở production** — chỉ test dựng ([ShopJobIsolationTests.cs:188](../../../backend/tests/IntegrationTests/Jobs/ShopJobIsolationTests.cs#L188)). 8d là job thật đầu tiên dùng nó |
| Đăng ký job + lịch | ✅ | [HangfireSetup.cs:49](../../../backend/src/Vsite.Infrastructure/Jobs/HangfireSetup.cs#L49), [RecurringJobs.cs:12](../../../backend/src/Vsite.Infrastructure/Jobs/RecurringJobs.cs#L12) | 3 job Identity chạy `0 3 * * *`; tham số cấu hình đi vào job qua tham số `RunAsync` |
| Allowlist `IgnoreQueryFilters()` | ✅ | [IgnoreQueryFiltersAllowlistTests.cs:21](../../../backend/tests/ArchitectureTests/IgnoreQueryFiltersAllowlistTests.cs#L21) | Khoá số lần gọi mỗi file |
| Bản publish + con trỏ current | ✅ | [SitePublication.cs:18](../../../backend/src/Vsite.Domain/Website/Entities/SitePublication.cs#L18), [Website.cs:55](../../../backend/src/Vsite.Domain/Website/Entities/Website.cs#L55) | Không xoá mềm; FK `Restrict` từ `CurrentPublicationId` nên DB tự chặn xoá bản current |
| Prune bản publish | ❌ | — | Chưa có đường xoá cứng nào. Danh sách trả ≤ 50 ([ListPublicationsHandler.cs:14](../../../backend/src/Vsite.Application/Website/Queries/ListPublications/ListPublicationsHandler.cs#L14)) |
| Cache site công khai chịu được bản bị xoá | ✅ | [GetPublishedSiteHandler.cs:56](../../../backend/src/Vsite.Application/Website/Queries/GetPublishedSite/GetPublishedSiteHandler.cs#L56) | Con trỏ Redis trỏ tới bản đã mất → tự đọc lại DB. Prune bản không-current an toàn |
| Xoá file trên storage | 🟡 | [IObjectStorage.cs:20](../../../backend/src/Vsite.Application/Common/Imaging/IObjectStorage.cs#L20) | `DeleteAsync` có (LocalDisk + S3), chưa nghiệp vụ nào gọi |
| Định nghĩa "ảnh mồ côi" + grace period | ❌ | [media.md:162](../../../backend/docs/modules/media.md#L162) | Thiết kế: quét tham chiếu (tree, snapshot, entity nghiệp vụ) → xoá mềm → sau grace period xoá file + record. Chưa có cấu hình retention |
| Nguồn tham chiếu ảnh | 🟡 | [PageDraftReferenceSource.cs:16](../../../backend/src/Vsite.Infrastructure/Website/PageDraftReferenceSource.cs#L16), [SitePublicationReferenceSource.cs:28](../../../backend/src/Vsite.Infrastructure/Website/SitePublicationReferenceSource.cs#L28) | Có cho endpoint cảnh báo `references` (hỏi theo **một** ảnh Library). Job cần hỏi ngược: "tập id đang dùng của cả shop" |
| Menu bỏ qua mục mồ côi | ✅ (lúc đọc) / ❌ (dọn) | [NavigationItems.cs:72](../../../backend/src/Vsite.Application/Website/Navigation/NavigationItems.cs#L72) | Bỏ lúc đọc, lúc publish và lúc render. Chưa dọn trong DB |
| Draft của trang đã xoá | ❌ | [DeletePageHandler.cs:27](../../../backend/src/Vsite.Application/Website/Commands/DeletePage/DeletePageHandler.cs#L27) | Xoá trang chỉ xoá mềm `Page`, draft nằm lại (WEBSITE-001 #13) |

### Phát hiện khi khảo sát

1. **Khung fan-out chưa nối vào production.** `ShopFanOut`/`ShopJobRunner` không được đăng ký DI ngoài test;
   `ShopJobRunner` resolve job bằng `GetRequiredService` nên mỗi job con phải đăng ký. 8d phải nối lần đầu.
2. **Tài liệu nói "ảnh clone đã xoá mềm" nhưng code không có trạng thái đó.** Chỉ ảnh Library bị xoá mềm
   (`SoftDeleteFromLibrary`, [MediaAsset.cs:211](../../../backend/src/Vsite.Domain/Media/Entities/MediaAsset.cs#L211)). Clone/Direct mồ côi là clone
   **không còn ai tham chiếu**, không phải clone bị đánh dấu xoá.
3. **Ranh giới module:** Media phụ thuộc `[Shop]`, Website phụ thuộc `[Shop, Media]`
   (`Docs/architecture/dependency-map.json`). Job dọn ảnh cần đọc tree + snapshot (Website) → hoặc đặt ở Website,
   hoặc Media khai interface để Website cài (như `PageDraftReferenceSource`).
4. **Validator chặn id ảnh đã xoá mềm** ([MediaReferenceValidator.cs:56](../../../backend/src/Vsite.Infrastructure/Media/MediaReferenceValidator.cs#L56)):
   nếu job đánh dấu một clone là mồ côi trong lúc editor vẫn giữ nó (undo), autosave kế tiếp bị từ chối.
5. **Job sửa menu sẽ làm editor dính 409** — `navigation_config.Version` = xmin; `Replace()` còn bật `IsDirty`.
6. Các comment đã giả định có prune: `SitePublicationReferenceSource` ("≈20 bản sau prune"),
   `backend/docs/modules/website.md:57` ("trần thành 20 khi có prune"). Contract `listPublications` không ghi trần → không chạm contract.
7. Nợ liên quan được dọn luôn: MEDIA-001 "Chưa làm xong" #3 (job dọn file + grace period). **Không** dọn được #10
   (file logo mồ côi do race): file đó không có record, job này quét theo record — cần quét storage, để task khác.

## 3. Flow (phác 2026-11-25)

Lịch: mỗi ngày, sau 3 job Identity (`0 3 * * *`). Mỗi job là job cha fan-out theo shop (#116); job con chạy trong scope của một shop.

### Flow 1: Prune bản publish (Website)
1. Job cha enqueue mỗi shop một job con                ❌ chưa nối `ShopFanOut` vào DI production (phát hiện 1)
2. Đọc website của shop + `CurrentPublicationId`        ✅ [Website.cs:55](../../../backend/src/Vsite.Domain/Website/Entities/Website.cs#L55)
3. Giữ = bản current + 19 bản `Version` lớn nhất khác   ❌ (người chốt: **đúng 20, gồm current**)
4. Xoá cứng phần còn lại (set-based, trong scope shop)  ❌ — FK `Restrict` là lưới cuối nếu lỡ chạm bản current
5. Site công khai không vỡ                              ✅ [GetPublishedSiteHandler.cs:56](../../../backend/src/Vsite.Application/Website/Queries/GetPublishedSite/GetPublishedSiteHandler.cs#L56)
- Nhánh: ≤ 20 bản → không làm gì. Chưa có website → không làm gì. Chủ shop rollback về bản X đúng lúc job xoá X →
  rollback gặp FK/không tìm thấy → phải trả 404 có `error_code`, không 500 (cần kiểm [RollbackPublicationHandler.cs:26](../../../backend/src/Vsite.Application/Website/Commands/RollbackPublication/RollbackPublicationHandler.cs#L26)).

### Flow 2: Dọn draft của trang đã xoá (Website)
1. Tìm `PageDraft` có `Page` đã xoá mềm                 ❌
2. Xoá cứng draft                                       ❌ — xoá qua EF thì interceptor biến thành xoá mềm, nên phải `ExecuteDelete`
- Người chốt: chờ **7 ngày** sau khi trang bị xoá mềm; cùng lúc gỡ `Page.OgImageId` (phát hiện 10).

### Flow 3: Dọn mục menu mồ côi (Website)
1. Đọc `NavigationConfig` của website                   ✅ [NavigationConfig.cs:13](../../../backend/src/Vsite.Domain/Website/Entities/NavigationConfig.cs#L13)
2. Bỏ qua nếu menu được sửa trong 24h qua               ❌ (người chốt: **chỉ dọn menu yên 24h**; dùng `UpdatedAt ?? CreatedAt`)
3. Bỏ mục trỏ trang không còn, **cha bị bỏ thì bỏ cả con** — đúng ngữ nghĩa lúc đọc ([NavigationItems.cs:67-72](../../../backend/src/Vsite.Application/Website/Navigation/NavigationItems.cs#L67-L72)) để
   chủ shop không thấy menu đổi. **Không** nối thêm trang mới như `Merge` làm.  ❌ cần hàm thuần riêng
4. Lưu, **không bật `IsDirty`**                         ❌ `Replace()` luôn bật dirty ([NavigationConfig.cs:47-55](../../../backend/src/Vsite.Domain/Website/Entities/NavigationConfig.cs#L47-L55)) → cần method domain mới
- Nhánh: không có mục mồ côi → không ghi (không đổi xmin, không gây 409).

### Flow 4: Dọn ảnh mồ côi (Media, cần dữ liệu Website)
1. Gom **tập id ảnh đang được dùng** của shop:
   - id trong tree draft của trang còn sống             🟡 có walker ([PageDraftReferenceSource.cs:38](../../../backend/src/Vsite.Infrastructure/Website/PageDraftReferenceSource.cs#L38)), nhưng đang hỏi theo một ảnh
   - id trong `snapshot.media` của các bản còn lại       🟡 [SitePublicationReferenceSource.cs:28](../../../backend/src/Vsite.Infrastructure/Website/SitePublicationReferenceSource.cs#L28)
   - FK nghiệp vụ: `Page.OgImageId`, `Website.DefaultOgImageId`/`FaviconAssetId`, `Shop.LogoId` ❌
2. Ứng viên = Clone, Direct, Library **đã xoá mềm**, Derivative có bản gốc không còn dùng. Library còn sống
   **không bao giờ** là ứng viên (ảnh của chủ shop trong thư viện).
3. Lần quét 1: ứng viên không được dùng, tạo > 7 ngày → **đánh dấu mồ côi** (người chốt: **7 ngày**)   ❌
4. Lần quét sau: đã đánh dấu > 7 ngày và vẫn không được dùng → xoá record + file (thứ tự bên dưới)   ❌
   Được dùng lại trong lúc chờ → bỏ đánh dấu.
- Thứ tự (sửa ở pha 5): **một transaction mỗi ảnh — xoá record → xoá file → commit.** FK còn giữ (vd. `Page.OgImageId`
  của trang đã xoá chưa purge) ⇒ xoá record lỗi ⇒ rollback, file còn nguyên, giữ đánh dấu, lần sau thử lại. File xoá xong
  mà commit hỏng ⇒ record trỏ file đã mất nhưng không ai dùng, lần sau xoá nốt (`DeleteAsync` idempotent).
- Prune (Flow 1) chạy trước Flow 4 trong ngày, để ảnh của bản vừa prune bắt đầu tính giờ.

## 4. Đã chốt trong session lập plan (2026-11-25)

| # | Chốt | Ai chốt |
|---|---|---|
| #118 | Prune giữ **đúng 20 bản, gồm bản current** (current + 19 bản `Version` lớn nhất khác) | người |
| #117 | Grace ảnh mồ côi **7 ngày**, hai bước: đánh dấu → quá 7 ngày vẫn không dùng thì xoá file + record. Ảnh tạo < 7 ngày không xét | người |
| — | Menu: chỉ dọn menu **không sửa trong 24h**, không bật `IsDirty`, không có mục mồ côi thì không ghi | người |
| — | Job dọn ảnh ở **module Media**; Media khai interface nguồn "ảnh đang dùng", Website cài (mẫu `IMediaReferenceSource`) | người |
| — | Đánh dấu bằng **cột mới `MediaAsset.OrphanedAt`** (migration), không dùng `IsDeleted` | người |
| — | Draft của trang đã xoá: xoá cứng khi trang đã xoá mềm **> 7 ngày** (file mới vào allowlist `IgnoreQueryFilters()`, `ShopId` tường minh) | người |
| — | Lane B, chỉ BE, một session, không Gate 1 | người |

### Phát hiện thêm ở pha 5

8. **Rollback về bản vừa bị prune trả 500.** Handler nạp bản publish, rồi `SaveChanges` gặp FK `23503` (bản đã bị
   xoá cứng giữa chừng) — chỉ bắt lỗi concurrency ([RollbackPublicationHandler.cs:60-67](../../../backend/src/Vsite.Application/Website/Commands/RollbackPublication/RollbackPublicationHandler.cs#L60-L67)) → T2 bắt thêm FK → 404.
9. **`IShopJob` không nhận tham số** ([IShopJob.cs:7](../../../backend/src/Vsite.Application/Common/Jobs/IShopJob.cs#L7)), nên "tham số cấu hình đi vào job qua `RunAsync`"
   (`backend/CLAUDE.md` §Job nền) không dùng được cho job theo shop. Cấu hình đi qua options ở `Application/{Module}/Options/`
   (tiền lệ `AuthOptions`, bind ở [DependencyInjection.cs:62](../../../backend/src/Vsite.Infrastructure/DependencyInjection.cs#L62)). T9 sửa câu đó trong `backend/CLAUDE.md`.
10. **Trang đã xoá mềm vẫn giữ FK `Page.OgImageId`** (`Restrict`) → ảnh OG của nó không bao giờ xoá được. T3 gỡ luôn
    `OgImageId` của trang đã xoá > 7 ngày cùng lúc purge draft.

## 6. Ràng buộc toàn cục

| Ràng buộc | Giữ bằng | Ở task |
|---|---|---|
| #116: job con chỉ chạm shop của scope | ca "shop B không đổi" trong mọi test job | T2, T3, T4, T7, T8 |
| #21.2 + #116 ngoại lệ (b): `IgnoreQueryFilters()` chỉ ở file allowlist, `ShopId` tường minh | `IgnoreQueryFiltersAllowlistTests` | T3 (T7 nếu cần) |
| #1 Media không đọc bảng Website | `ModuleBoundaryTests` (interface ở Media, cài ở `Infrastructure/Website`) | T6 |
| Bản current không bao giờ bị prune | test + FK `Restrict` ([WebsiteConfiguration.cs:77](../../../backend/src/Vsite.Infrastructure/Persistence/Configurations/Website/WebsiteConfiguration.cs#L77)) | T2 |
| `Library` còn sống không bao giờ bị đánh dấu/xoá | test + `DomainException` trong `MarkOrphaned` | T5, T7 |
| Không sửa contract / generated | review: diff không có `contracts/`, `packages/api-sdk/src/generated` | review |
| Migration snake_case, không pending | cột `orphaned_at` + kiểm pending model changes | T5 |

## 7. Review focus

| # | Tình huống | Hành vi đúng | Test ở |
|---|---|---|---|
| R1 | Rollback về bản cũ (ngoài 20 bản mới nhất), rồi job chạy | Còn đúng 20 bản gồm bản current | T2 |
| R2 | Job xoá bản X đúng lúc chủ shop rollback về X | Rollback trả 404 có `error_code`, không 500 | T2 |
| R3 | Chủ shop vừa sửa menu (< 24h) | Job không ghi menu ⇒ `Version` không đổi, không 409 | T4 |
| R4 | Ảnh bị đánh dấu, rồi chủ shop undo đưa ảnh về draft | Autosave không bị chặn; lần quét sau bỏ đánh dấu | T7 |
| R5 | Một snapshot của shop đọc lỗi | Job ảnh của shop đó **dừng, không xoá gì** (ném ⇒ Hangfire retry) | T6, T7 |
| R6 | Ảnh mồ côi vẫn bị FK giữ (OG của trang đã xoá < 7 ngày) | Rollback transaction, file còn nguyên, vẫn đánh dấu | T7 |
| R7 | Library đã xoá mềm nhưng đang là `Shop.LogoId` | Không đụng bản gốc lẫn phái sinh | T6, T7 |
| R8 | Clone vừa tạo (picker) chưa kịp vào draft | Tạo < 7 ngày ⇒ không xét | T7 |

## 9. Session S1: Backend

Nhánh `feature/WEBSITE-008` từ `master`. Kiểm chung: `dotnet test backend/tests/ArchitectureTests` và
`dotnet test backend/vsite.sln --filter "FullyQualifiedName~{Lớp test}"` (cần Docker). Thời gian "now" lấy qua
`TimeProvider` (hoặc tiện ích thời gian sẵn có trong repo — kiểm khi làm) để test điều khiển được.

### T1: Nối khung fan-out + options
- `HangfireSetup.AddJobs`: `AddScoped<ShopFanOut>()`, `AddScoped<ShopJobRunner>()`.
- Options (bind trong `DependencyInjection.AddInfrastructure`, `ValidateOnStart`):
  - `Vsite.Application/Website/Options/BuilderRetentionOptions.cs` — `Jobs:BuilderRetention`: `PublicationsToKeep = 20` (≥ 1),
    `DeletedPageGrace = 7.00:00:00`, `NavigationQuietPeriod = 1.00:00:00` (≥ 0).
  - `Vsite.Application/Media/Options/MediaCleanupOptions.cs` — `Jobs:MediaCleanup`: `OrphanGrace = 7.00:00:00` (≥ 0).
- [ ] Test trước: `backend/tests/IntegrationTests/Jobs/ShopFanOutTests.cs` — host production resolve được `ShopFanOut`/`ShopJobRunner` (DI của `Program`, không tự đăng ký); options mặc định đúng; `PublicationsToKeep = 0` ⇒ khởi động lỗi
- [ ] FAIL → hiện thực → PASS
- [ ] Commit `feat(jobs): đăng ký ShopFanOut/ShopJobRunner + options job builder (WEBSITE-008, #116)`

### T2: Prune bản publish (#118)
- `Vsite.Application/Website/Jobs/PublicationPruner.cs`: website của shop (`ShopId` từ `ITenantContext`, tường minh trong predicate);
  không có ⇒ return. `keep` = current ∪ (`PublicationsToKeep − 1` bản `Version` lớn nhất khác; không có current thì `PublicationsToKeep`).
  `ExecuteDeleteAsync` phần còn lại. Trả số bản đã xoá.
- `ListPublicationsHandler.MaxItems` 50 → 20 + sửa summary.
- `RollbackPublicationHandler`: bắt `DbUpdateException` FK violation (`23503`) ⇒ `NotFoundException("Publication", id)`; helper nhận diện FK đặt cạnh `UniqueViolation`.
- [ ] Test trước: `backend/tests/IntegrationTests/Website/PublicationPrunerTests.cs`
  - [ ] 25 bản, current = mới nhất ⇒ còn `Version` 6..25
  - [ ] 25 bản, rollback về `Version` 1 ⇒ còn bản 1 + 7..25 (R1)
  - [ ] ≤ 20 bản ⇒ không xoá; chưa có website ⇒ không lỗi
  - [ ] Shop A, B mỗi shop 25 bản, chạy cho A ⇒ B còn 25 (#116)
  - [ ] Publish sau prune ⇒ `Version` = max + 1
  - [ ] `GET /api/site` sau prune vẫn trả bản current
- [ ] Test trước: rollback tới bản bị xoá cứng sau khi handler đã nạp ⇒ 404 (R2)
- [ ] FAIL → hiện thực → PASS
- [ ] Commit `feat(website): prune bản publish giữ 20 bản gồm current (WEBSITE-008, #118)`

### T3: Purge draft của trang đã xoá (#118)
- `Vsite.Application/Website/Jobs/DeletedPagePurger.cs` — **file mới vào allowlist** (2 lời gọi `IgnoreQueryFilters()`): tập trang
  `ShopId == shopId && IsDeleted && DeletedAt < now − DeletedPageGrace` ⇒ (1) `ExecuteDeleteAsync` `PageDrafts` của tập đó (`Remove` sẽ
  thành xoá mềm), (2) `ExecuteUpdateAsync` `OgImageId = null` (phát hiện 10).
- [ ] Test trước: `backend/tests/IntegrationTests/Website/DeletedPagePurgerTests.cs`
  - [ ] Trang xoá 8 ngày ⇒ draft mất, `OgImageId` null; xoá 6 ngày ⇒ còn; trang sống ⇒ không đụng
  - [ ] Trang đã xoá ở shop B ⇒ không đụng (#116)
- [ ] `IgnoreQueryFiltersAllowlistTests`: thêm `Vsite.Application/Website/Jobs/DeletedPagePurger.cs` = (2, lý do)
- [ ] FAIL → hiện thực → PASS
- [ ] Commit `feat(website): purge draft + OG của trang đã xoá quá grace (WEBSITE-008, #118)`

### T4: Dọn mục menu mồ côi (#118)
- `NavigationItems.DropOrphans(items, existingPageIds)` — hàm thuần; tách phần bỏ-mục của `MergeItem` để `Merge` dùng lại (hai đường
  không lệch). Không nối trang mới.
- `NavigationConfig.PruneOrphans(string itemsJson)` — đổi `Items`, **không** đổi `IsDirty`.
- `Vsite.Application/Website/Jobs/NavigationOrphanCleaner.cs`: chỉ menu có `(UpdatedAt ?? CreatedAt) < now − NavigationQuietPeriod`;
  kết quả khác thì `PruneOrphans` + save; concurrency ⇒ bỏ lượt (log).
- [ ] Test trước: `NavigationItemsTests` — `DropOrphans` bỏ mục trỏ trang không còn kèm con; giữ `custom`, giữ `hidden`; không thêm trang
- [ ] Test trước: `backend/tests/IntegrationTests/Website/NavigationOrphanCleanerTests.cs`
  - [ ] Menu yên 25h có mục mồ côi ⇒ đã bỏ, `IsDirty` giữ giá trị cũ
  - [ ] Menu sửa 1h trước ⇒ `Version` không đổi (R3); không có mục mồ côi ⇒ `Version` không đổi
  - [ ] Shop B không đổi (#116)
- [ ] FAIL → hiện thực → PASS
- [ ] Commit `feat(website): dọn mục menu trỏ trang đã xoá (WEBSITE-008, #118)`

### T5: `MediaAsset.OrphanedAt` (#117)
- Domain: `DateTimeOffset? OrphanedAt { get; private set; }`, `MarkOrphaned(at)` (đã đánh dấu thì giữ mốc cũ), `ClearOrphaned()`;
  `MarkOrphaned` trên `Library` chưa xoá mềm ⇒ `DomainException`.
- Configuration: cột `orphaned_at`, index `ix_media_asset_orphaned` `(shop_id, orphaned_at) WHERE orphaned_at IS NOT NULL`.
  Migration `AddMediaAssetOrphanedAt`.
- [ ] Test trước: `MediaAssetTests` (mark giữ mốc, clear, Library sống ném) · `MediaDbConstraintTests` (cột + index)
- [ ] FAIL → hiện thực → PASS
- [ ] Commit `feat(media): MediaAsset.OrphanedAt + migration (WEBSITE-008, #117)`

### T6: Nguồn "ảnh đang dùng" (#117)
- `Vsite.Application/Media/Interfaces/IMediaUsageSource.cs`: `Task<IReadOnlySet<Guid>> CollectUsedAssetIdsAsync(CancellationToken)`;
  shop từ `ITenantContext`; **ném** khi không đọc được (R5), không trả rỗng.
- Cài ở `Vsite.Infrastructure/Website/`:
  - `PageDraftUsageSource` — id ảnh trong tree draft của trang sống (walker như [PageDraftReferenceSource.cs:38](../../../backend/src/Vsite.Infrastructure/Website/PageDraftReferenceSource.cs#L38))
  - `SitePublicationUsageSource` — key `snapshot.media` của mọi bản còn lại (dùng chung hàm đọc key với [SitePublicationReferenceSource.cs:70](../../../backend/src/Vsite.Infrastructure/Website/SitePublicationReferenceSource.cs#L70))
  - `SeoImageUsageSource` — `Page.OgImageId` (trang sống), `Website.DefaultOgImageId`, `Website.FaviconAssetId`
- `Shop.LogoId` đọc thẳng trong job (Media dependsOn Shop). Đăng ký cạnh [DependencyInjection.cs:133-137](../../../backend/src/Vsite.Infrastructure/DependencyInjection.cs#L133-L137).
- [ ] Test trước: `backend/tests/IntegrationTests/Website/MediaUsageSourceTests.cs` — từng nguồn đúng id; draft trang đã xoá không tính; GUID hoa/thường khớp; shop B không lọt; snapshot hỏng ⇒ ném
- [ ] `ModuleBoundaryTests` xanh
- [ ] FAIL → hiện thực → PASS
- [ ] Commit `feat(media): nguồn ảnh đang dùng cho job dọn mồ côi (WEBSITE-008, #117)`

### T7: `MediaOrphanCleanupJob` (#117)
- `Vsite.Application/Media/Jobs/MediaOrphanCleanupJob.cs : IShopJob`:
  1. `used` = hợp mọi `IMediaUsageSource` ∪ `Shop.LogoId`; thêm phái sinh của bản gốc ∈ `used` hoặc là `Library` sống.
  2. Ứng viên = `Kind ∈ {Clone, Direct, Derivative}` hoặc (`Library` ∧ `IsDeleted`). Đọc Library đã xoá mềm cần bỏ bộ lọc ⇒ nếu
     cần `IgnoreQueryFilters()` thì thêm file vào allowlist, `ShopId` tường minh.
  3. ∈ `used` và đang đánh dấu ⇒ `ClearOrphaned`. ∉ `used`, `CreatedAt < now − grace`, chưa đánh dấu ⇒ `MarkOrphaned(now)`.
  4. Đã đánh dấu, `OrphanedAt < now − grace`, ∉ `used` ⇒ mỗi ảnh một transaction: xoá record → `IObjectStorage.DeleteAsync` → commit.
     FK violation / storage lỗi ⇒ rollback, log Warning, sang ảnh khác (R6).
  Theo lô (vd. 500 record).
- [ ] Test trước: `backend/tests/IntegrationTests/Media/MediaOrphanCleanupJobTests.cs` (storage giả ghi lại key đã xoá)
  - [ ] Clone không dùng, tạo 8 ngày ⇒ đánh dấu, file còn; 8 ngày sau ⇒ record + file mất
  - [ ] Clone tạo 6 ngày ⇒ không đánh dấu (R8)
  - [ ] Còn trong snapshot / draft ⇒ không đánh dấu; đánh dấu rồi được dùng lại ⇒ bỏ đánh dấu (R4)
  - [ ] Library sống không dùng ⇒ không đụng; Library đã xoá đang là logo ⇒ không đụng cả phái sinh (R7)
  - [ ] Library đã xoá không dùng ⇒ đánh dấu rồi xoá, kèm phái sinh
  - [ ] FK còn giữ ⇒ record + file còn, vẫn đánh dấu (R6); storage ném ⇒ record còn
  - [ ] Một nguồn ném ⇒ không đánh dấu/xoá gì (R5)
  - [ ] Shop B không đổi (#116)
- [ ] FAIL → hiện thực → PASS
- [ ] Commit `feat(media): job dọn ảnh mồ côi hai bước (WEBSITE-008, #117)`

### T8: Lịch + nối đầu-cuối
- `Vsite.Application/Website/Jobs/WebsiteRetentionJob.cs : IShopJob` — `PublicationPruner` → `DeletedPagePurger` → `NavigationOrphanCleaner`
  (mỗi bước tự lưu, chạy lại an toàn), log số dòng mỗi bước. Đăng ký DI các job + bước.
- `RecurringJobs.Register`: `website.retention` `30 3 * * *` ⇒ `ShopFanOut.EnqueuePerShop<WebsiteRetentionJob>`;
  `media.orphan-cleanup` `0 4 * * *` ⇒ `ShopFanOut.EnqueuePerShop<MediaOrphanCleanupJob>`.
- [ ] Test trước: `RecurringJobsTests` — đủ 5 id, đúng cron; chạy `ShopJobRunner.RunAsync<WebsiteRetentionJob>(A)` và `<MediaOrphanCleanupJob>(A)` qua DI thật ⇒ dữ liệu B không đổi
- [ ] FAIL → hiện thực → PASS
- [ ] Commit `feat(jobs): lịch website.retention + media.orphan-cleanup (WEBSITE-008, #117, #118)`

### T9: Kết thúc S1 — tài liệu (DoD 9) + Gate 2
- [ ] `Docs/tasks/WEBSITE-008/changelog.md` (lệch có chủ đích / chưa làm xong)
- [ ] `backend/docs/modules/media.md` §5 thay đoạn "Ai thật sự dọn file" bằng #117; `website.md`: prune/draft/menu, `listPublications` ≤ 20
- [ ] `backend/CLAUDE.md` §Job nền: sửa câu "tham số qua `RunAsync`" (phát hiện 9) + bảng cấu hình `Jobs:BuilderRetention:*`, `Jobs:MediaCleanup:*`
- [ ] `DECISIONS.md` #117/#118 → ✅ + test giữ; `00-INDEX.md` §3 dòng 8d, §4 đóng MEDIA-001 #3 (còn #10)
- [ ] `MEDIA-001` #3, `WEBSITE-001` #13, `WEBSITE-007` "job dọn mục mồ côi" trỏ WEBSITE-008; sửa comment "≈20 bản sau prune"
- [ ] Nợ Docker (nếu có) → `Docs/DOCKER-TEST-DEBT.md` · `pnpm check:docs`
- [ ] Review toàn nhánh (`change-reviewer`, model mạnh — chạm tenant + xoá dữ liệu) → Gate 2

## 10. Giả định tôi đã tự đặt

1. Ba việc của Website gộp **một** job theo shop (`WebsiteRetentionJob`, 03:30: prune → draft → menu); job ảnh riêng lúc 04:00 để thấy kết quả prune cùng ngày.
2. Cấu hình qua options ở Application (phát hiện 9), section `Jobs:BuilderRetention` / `Jobs:MediaCleanup`.
3. Grace draft trang đã xoá = 7 ngày như ảnh, nhưng là khoá cấu hình riêng.
4. Purge trang đã xoá gỡ luôn `OgImageId` (phát hiện 10); không xoá cứng dòng `Page`.
5. Phái sinh được coi là đang dùng khi bản gốc đang dùng hoặc là `Library` sống; bản gốc mất ⇒ ứng viên.
6. Nguồn "ảnh đang dùng" lỗi ⇒ job ảnh của shop đó dừng hẳn.
7. `listPublications` hạ trần 50 → 20 (contract không ghi trần nên không đổi contract).
8. Job ghi menu gặp concurrency ⇒ bỏ lượt, không retry ngay.
