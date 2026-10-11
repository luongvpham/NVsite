# vsite — Registry Quyết Định (#1–#95)

> **File này là CHỈ MỤC, không phải nội dung.** Mỗi dòng cho bạn biết quyết định đó nói gì trong một
> câu, nó được **định nghĩa đầy đủ ở đâu**, và nó **đã thành code chưa**. Đọc dòng ở đây trước, rồi
> mở đúng một chỗ — đừng quét cả `02` (74 KB) để tìm một quyết định.
>
> **Cách mở nhanh:** cột "Định nghĩa tại" ghi pattern grep được nguyên văn.
> Ví dụ `02 §Quyết định #21` → `grep -n "### Quyết định #21" DesignIdeal/02-tech-stack-and-decision.md`.

---

## ⚠️ Đọc trước khi tra

**Không phải quyết định nào cũng nằm trong `02`.** `02` định nghĩa **#1–#39** và **#68**.
**#40–#58** định nghĩa tại `05` §0, **#59–#67** định nghĩa tại `07` §0, **#69–#86** và **#88** định nghĩa tại `08` §0, **#87** tại `backend/CLAUDE.md` §OpenAPI, **#89** tại `ai-agent-development-workflow.md` §6, **#90** tại `backend/docs/modules/identity.md` §Quyết định #90, **#91** tại `backend/CLAUDE.md` §OpenAPI, **#92**, **#94**, **#95** tại `05` §0, **#93** tại `04` §4.5.
Không chép các quyết định đó sang `02` — cố tình không làm. Chép sang `02` sẽ tạo bản
sao thứ hai để lệch nhau; thay vào đó registry này trỏ thẳng tới nơi định nghĩa duy nhất.

→ Phát biểu đúng: **`DECISIONS.md` là chỉ mục quyết định; nội dung nằm ở file mà cột "Định nghĩa tại" trỏ tới.**

## Cấp số cho quyết định mới

Số kế tiếp = **số lớn nhất trong bảng dưới + 1**. Cấp số **tại file này trước**, rồi mới viết nội
dung ở file chuyên đề. Không cấp số rải rác ở file con nữa — đó chính là lý do `#68` bị viết xong
trước khi `#40`–`#67` kịp có chỗ đứng.

## Ký hiệu trạng thái

| Ký hiệu | Nghĩa |
|---|---|
| ✅ **enforce** | Đã code **và** có cơ chế máy chặn vi phạm (test / lock file / codegen hard-fail). Cột ghi rõ cơ chế nào |
| ✅ đã code | Đã hiện thực hoá, nhưng vi phạm chỉ bị bắt bằng code review |
| 📐 thiết kế | Đã chốt trên giấy, **chưa có code**. Bước triển khai ghi theo `step.md` |
| ⏳ Phase 2+ | Chốt rồi nhưng cố ý hoãn |
| 🔁 superseded | Đã bị quyết định sau thay thế — **đừng làm theo** |

---

## Bảng tra

| # | Tóm tắt | Định nghĩa tại | Trạng thái |
|---|---|---|---|
| **#1** | Modular Monolith, không microservices; module không reference project module khác | `02 §Quyết định #1` | ✅ enforce — `ModuleBoundaryTests.cs` |
| **#2** | PostGIS trước, Elasticsearch sau | `02 §Quyết định #2` | 🔁 Phần "ES sau" bị **#26** thay (ES dùng từ MVP). PostGIS vẫn đúng |
| **#3** | Auth tự phát JWT, KHÔNG dùng OIDC / OpenIddict | `02 §Quyết định #3` | ✅ đã code — IDENTITY-001 |
| **#4** | Session tách biệt giữa vsite và mini-website của shop | `02 §Quyết định #4` | ✅ đã code — audience trong JWT |
| **#5** | Một user record toàn cục, token audience theo từng shop | `02 §Quyết định #5` | 🔁 Tinh chỉnh bởi **#29** — đọc #29 trước |
| **#6** | Social login qua callback cố định + handoff code | `02 §Quyết định #6` | 📐 `ExternalLogin` entity đã có, luồng provider chưa code |
| **#7** | Multi-tenancy hỗ trợ cả 3 chế độ domain | `02 §Quyết định #7` | ✅ đã code (thu hẹp) — `TenantResolutionMiddleware` resolve theo Host; custom domain chờ #9 |
| **#8** | Reserved slug list là bắt buộc | `02 §Quyết định #8` | ✅ enforce — `ReservedRoutesTests.cs` |
| **#9** | Caddy On-Demand TLS cho custom domain | `02 §Quyết định #9` | 📐 |
| **#10** | SEO: 1 primary domain + 301 + canonical | `02 §Quyết định #10` | 📐 |
| **#11** | `builder-renderer` phải nhận `basePath`, không tự dựng URL | `02 §Quyết định #11` | 📐 ⚠️ **Chưa có cách enforce** — xem `BOOTSTRAP-002/changelog.md` mục lệch #3 |
| **#12** | Component Tree lưu JSON, không lưu HTML | `02 §Quyết định #12` | 📐 Shape đã chốt ở `05` §6; lưu trữ chờ Bước 5 |
| **#13** | Tách biệt Business Data / Presentation / Editor | `02 §Quyết định #13` | 📐 Bước 5 |
| **#14** | AI sinh Operations, không sinh HTML/Tree | `02 §Quyết định #14` | ⏳ Phase 3 |
| **#15** | Pipeline xử lý AI Chat | `02 §Quyết định #15` | ⏳ Phase 3 |
| **#16** | Component Targeting: Selection Context ưu tiên | `02 §Quyết định #16` | ⏳ Phase 3 |
| **#17** | Component Registry: một manifest, codegen phần còn lại | `02 §Quyết định #17` | ✅ enforce — hiện thực hoá đầy đủ ở `07`, xong tại BOOTSTRAP-002 |
| **#18** | Contract-first: OpenAPI snapshot commit vào repo | `02 §Quyết định #18` | ✅ enforce — `contracts/contract.lock` |
| **#19** | Quy ước bắt buộc để codegen không vỡ (enum string, không TS `enum`…) | `02 §Quyết định #19` | ✅ đã code |
| **#20** | State ownership: TanStack Query vs Zustand | `02 §Quyết định #20` | 📐 |
| **#21** | **Tenant security invariants** — 5 mục, xem #21.1–#21.5 dưới bảng | `02 §Quyết định #21` | ✅ enforce — `TenantIsolationTests` + `MediaTenantIsolationTests` (Postgres thật, Testcontainers) · `IgnoreQueryFiltersAllowlistTests` (#90) |
| **#22** | FE 2 app (`web`, `portal`) thay vì 3, chia theo rendering strategy | `02 §Quyết định #22` | ✅ đã code |
| **#23** | SSR cho `apps/web` (TanStack Start); `apps/portal` giữ CSR; renderer phải isomorphic | `02 §Quyết định #23` | ✅ enforce một phần — ESLint `no-restricted-globals`; **thiếu** snapshot SSR↔CSR |
| **#24** | Reserved routes: một nguồn JSON duy nhất dùng chung FE + BE | `02 §Quyết định #24` | ✅ enforce — `config/reserved-routes.json` + `ReservedRoutesTests.cs` |
| **#25** | Portal URL: `admin.vsite.vn/*` | `02 §Quyết định #25` | ✅ đã code |
| **#26** | Elasticsearch dùng ngay từ MVP (thay phần "sau" của #2) | `02 §Quyết định #26` | 📐 |
| **#27** | Cấu trúc JWT claims | `02 §Quyết định #27` | ✅ đã code — ⚠️ `ownerShopIds` **chỉ** để render UI, không dùng làm căn cứ cho phép (#21.5) |
| **#28** | Identity model: credential riêng theo shop, merge theo email/SĐT | `02 §Quyết định #28` | 🔁 **SUPERSEDED bởi #29** — bỏ hẳn, không merge. Đừng làm theo |
| **#29** | Identity chốt lại: email là identity key, token shop bị giới hạn năng lực | `02 §Quyết định #29` · chi tiết `03` | ✅ đã code — IDENTITY-001 |
| **#30** | Đăng ký: `User` chỉ sinh sau khi verify, qua `PendingRegistration` | `02 §Quyết định #30` · chi tiết `03` §5 | ✅ đã code |
| **#31** | Phân giải Role theo domain; Portal lấy `ShopId` từ route | `02 §Quyết định #31` · chi tiết `03` §7 | ✅ đã code |
| **#32** | Ranh giới năng lực của token `shop:{shopId}` | `02 §Quyết định #32` · chi tiết `03` §7.1 | ✅ enforce — `TokenScopeTests` ⚠️ **chưa chạy thật, cần Docker** |
| **#33** | Page model: `Composable` vs `System` | `02 §Quyết định #33` | 📐 Bước 5 |
| **#34** | Navigation là dữ liệu derived + manual overlay | `02 §Quyết định #34` | 📐 Bước 7 |
| **#35** | `ServiceCategory` toàn cục; đánh giá neo `Listing` | `02 §Quyết định #35` · chi tiết `04` §3 | 📐 |
| **#36** | Anti-pattern cấm kế thừa từ VSite .NET Framework 4.8 | `02 §Quyết định #36` | ✅ đã chép vào root `CLAUDE.md` theo yêu cầu của chính #36 ⚠️ bản chép **thiếu** mục "7 pattern ĐƯỢC giữ lại" |
| **#37** | `Shop.Kind`: `Hosted` vs `ExternalOnly`; hai luồng doanh thu | `02 §Quyết định #37` · chi tiết `04` §2.1 | ✅ đã code — `ShopKind.cs` |
| **#38** | Listing opt-in, không auto-map `Service`→`Listing` | `02 §Quyết định #38` · chi tiết `04` | 📐 |
| **#39** | Chuẩn hoá đánh số Phase + đóng mâu thuẫn liên tài liệu — 5 mục, xem #39.1–#39.5 dưới bảng | `02 §Quyết định #39` | ✅ |
| **#40** | `Product` là module **riêng**, không gộp với `Service`; không chung enum, không chung bảng | `05` §0 | ⏳ Bước 10 |
| **#41** | Đơn vị publish là **toàn site** (`SitePublication` bất biến), không publish từng trang | `05` §0 · §7 | 📐 Bước 8 |
| **#42** | Không persist op-log; undo/redo chỉ client-side (zundo) trong một phiên | `05` §0 | 📐 Bước 5 |
| **#43** | Component manifest **additive-only**; breaking → tạo `variant` mới. Hệ quả: node không cần `schemaVersion` | `05` §0 · cơ chế ở `07` §5 | ✅ enforce — `registry.lock.json` + `check-additive.ts` |
| **#44** | Draft persist **server-side** (`PageDraft`), không chỉ Zustand | `05` §0 · §5 | 📐 Bước 5 |
| **#45** | Template là **seed copy một lần**, không phải reference | `05` §0 · §10 | 📐 Bước 11 |
| **#46** | `System` page **có** Component Tree thật, khoá ở Operations Engine | `05` §0 · §11 | 📐 Bước 5 |
| **#47** | Form trên website shop POST tới module `Lead`; builder không sở hữu submission | `05` §0 | 📐 |
| **#48** | `Product` **không** lên marketplace; không giỏ hàng / đơn hàng / thanh toán ở Phase 2 | `05` §0 | 📐 |
| **#49** | Tồn kho là tuỳ chọn (`Product.TrackInventory`), mặc định tắt | `05` §0 · §17 | ⏳ Bước 10 |
| **#50** | Attribute `Text` không filter, không thống kê — chỉ hiển thị | `05` §0 · §14 | ⏳ Bước 10 |
| **#51** | Xoá attribute/option là **xoá mềm** (`IsArchived`) | `05` §0 · §14 | ⏳ Bước 10 |
| **#52** | Tối đa **3 trục variant** mỗi sản phẩm | `05` §0 · §19 | ⏳ Bước 10 |
| **#53** | Pipeline ảnh dùng chung (magic bytes, strip EXIF, webp, ≤1600px); file đúng kích thước **sinh sẵn lúc đặt/upload — không có image proxy runtime**; URL = `{domain bất kỳ}/media/{đường dẫn}`, `media` là reserved route, đọc không kiểm tenant; luôn qua `resolveImage()` | `05` §0 · chi tiết `08` §1, §3, §5 | ✅ đã code — pipeline + phục vụ `/media/*` có test (`ImageSharpImageProcessorTests`, `MediaFileMiddlewareTests`, `ReservedRoutesTests`); ⚠️ vế "luôn qua `resolveImage()`" chưa có lint (nợ ở `00-INDEX.md` §4) và Portal đọc logo qua `logoUrl` do BE trả sẵn (#88) |
| **#54** | `Service` **không** có `System` page riêng — hiển thị qua component bind trên trang `Composable` | `05` §0 · `06` §6 | 📐 Bước 9 |
| **#55** | **Không có bảng `MediaVariant`** — mỗi file `MediaAsset` là một record (`Preset` + `SourceAssetId`); ảnh Listing/Product không có bảng. Dọn: job quét tham chiếu (MediaAsset, Bước 8) / xoá thư mục entity (Listing/Product). Quota MediaAsset = chỉ bản gốc (`Kind` Library/Direct) — clone/phái sinh không tính | `05` §0 · chi tiết `08` §2, §4, §8 | ✅ enforce — `CK ck_media_library_preset` + `MediaDbConstraintTests`; quota theo `Kind IN (Library, Direct)` (REFACTOR-DB-001, trước là `SourceAssetId IS NULL` — sai khi FK `SET NULL`): `LibraryHandlerTests.Usage_unchanged_after_clone_but_increases_by_SizeBytes_after_direct_upload` + `…Usage_ignores_clone_whose_source_was_hard_deleted`. 📐 Bước 8: job dọn tham chiếu |
| **#56** | Ảnh sản phẩm (`Product.ImageUrls`) và `media_assets` là **hai hệ độc lập**; dùng ảnh sản phẩm trong builder phải "Thêm vào thư viện" (tạo bản Library) rồi mới chọn → clone (#71). Builder picker **không bao giờ** thấy ảnh sản phẩm trực tiếp | `05` §0 · §20 | ⏳ Bước 10 |
| **#57** | Tỉ lệ ảnh sản phẩm do **danh mục** quyết định (`ShopProductCategory.ImageRatio`); file full crop theo tỉ lệ **lúc upload**; đổi tỉ lệ thì ảnh cũ giữ nguyên, không có job sinh lại | `05` §0 · §13 | ⏳ Bước 10 |
| **#58** | **Không** hỗ trợ `srcset` responsive ở Phase 2; mỗi vị trí một preset cố định ~2× độ rộng CSS; hoãn được nhờ chữ ký `resolveImage()` giữ nguyên (#74) | `05` §0 | 📐 |
| **#59** | Manifest là file `.ts` object literal thuần — không import runtime, không hàm, không điều kiện | `07` §0 · §4 | ✅ enforce — `meta/manifest-schema.ts` (Zod) |
| **#60** | BE **không** port Zod sang C#; codegen sinh `props-schemas.json` (JSON Schema 2020-12) | `07` §0 · §11 | ✅ enforce — `tests/schema-equivalence.test.ts` |
| **#61** | Props khai ở tầng `type`; `variant` chỉ khai `usesProps` / `requiresProps` | `07` §0 · §4.2 | ✅ enforce — `meta/manifest-schema.ts` |
| **#62** | `registry.lock.json` commit vào repo; CI so lock, breaking → fail build; lock phải tồn tại và bằng đúng snapshot hiện tại, chỉ cập nhật bằng `pnpm registry:lock` (từ chối khi vi phạm) | `07` §0 · §5 | ✅ enforce — `scripts/check-additive.ts` (CI + pre-commit) + `lock-snapshot.test.ts` + `gen-registry.test.ts` (TOOLING-001) |
| **#63** | `kind` của prop là **tập đóng 12 giá trị**; người viết component không được phát minh kind mới | `07` §0 · §4.3 | ✅ enforce — `meta/prop-kinds.ts` |
| **#64** | Mọi prop `kind: "image"` **bắt buộc** khai `preset` nằm trong `config/image-presets.json`; whitelist là lint build-time + bảng kích thước cho pipeline BE | `07` §0 · §7.1 | ✅ enforce — `gen-registry.ts` hard-fail + `tests/config-files.test.ts`; `config/image-presets.json` đã đủ 9 preset (`ImagePresetCatalogTests.Reads_Real_ImagePresets_File_With_Nine_Presets`), shape `{ presets, surfaces }` |
| **#65** | Prop `kind: "binding"` khai `sources` tường minh; codegen **hard-fail** nếu chứa `"Review"` | `07` §0 · §7.4 | ✅ enforce — `gen-registry.ts` + `config/binding-sources.json` + test |
| **#66** | Nhãn Inspector là tiếng Việt thuần ở Phase 2, không dùng i18n key | `07` §0 | ✅ đã code |
| **#67** | Sanitize `richText` — hai profile `inline` / `basic` | `07` §0 · §7.2 | ✅ **ĐÃ CHỐT + đã code** — `config/sanitize-profiles.json` + `src/sanitize-html.ts`. ⚠️ `07` §7.2 vẫn ghi "cần xác nhận" — **nhãn đó sai, bỏ qua** |
| **#68** | Component Registry manifest triển khai ở Bước 2, **trước** Identity | `02 §Quyết định #68` | ✅ đã xong — BOOTSTRAP-002 |
| **#69** | **Một bảng `MediaAsset`**; `Preset IS NULL` ⟺ `IsInLibrary = true` (CHECK hai chiều); không có cột `Purpose` (ảnh dùng để làm gì) | `08` §0 · §2 | ✅ enforce — `CK ck_media_library_preset` + `MediaDbConstraintTests` (hai chiều) + `MediaAssetTests`. REFACTOR-DB-001 thêm cột `Kind` (Library/Direct/Clone/Derivative) — vai trò **kỹ thuật** của record, không phải `Purpose`; cần vì Clone và Derivative trùng bộ `IsInLibrary`/`Preset`/`SourceAssetId`. Enforce: `ck_media_asset_kind`, `ux_media_asset_derivative` + `MediaDbConstraintTests` |
| **#70** | Hai chế độ upload vào slot: tick thư viện → bản Library + clone; không tick → một record crop đúng preset, **không giữ gốc**. Không có "lưu vào thư viện" sau upload; focal point chọn trước khi crop | `08` §0 · §3.2, §3.3 | ✅ enforce — `UploadHandlerTests.UploadToSlot_without_SaveToLibrary_creates_exactly_one_Direct_record` + `…with_SaveToLibrary_creates_library_plus_derived_clone` |
| **#71** | Lấy ảnh từ Library **luôn clone**; tree **không bao giờ** chứa id `IsInLibrary = true`; clone chỉ sinh từ bản Library, không từ clone | `08` §0 · §3.4 | 📐 Bước 4 — nửa đã code: clone chỉ từ bản Library (`Clone_of_a_clone_id_returns_NotFound`, `MediaAssetTests`); vế "tree không chứa id Library" chờ nối `IMediaReferenceValidator` vào handler lưu draft (Bước 5) |
| **#72** | Clone **không** cascade delete theo node; dọn theo retention `SitePublication` | `08` §0 · §4 | ✅ enforce (phần Bước 4: không xoá) — `LibraryHandlerTests.Delete_library_then_list_excludes_it_but_clone_still_resolves_and_file_still_opens` + `MediaDbConstraintTests.Hard_delete_of_library_asset_sets_clone_SourceAssetId_to_null`. 📐 Bước 8: job dọn theo retention `SitePublication` |
| **#73** | **Ảnh dữ liệu nghiệp vụ** (logo, Service, ServiceGroup, ProductCategory, OG, favicon) = bản Library + bộ phái sinh sinh sẵn lúc upload; preset suy ra bằng codegen từ manifest bind nguồn + tập cố định của bề mặt vsite. Không áp cho ảnh Listing/Product (#79) | `08` §0 · §3.6 | ✅ enforce cho nguồn `Shop` (logo) — `ShopLogoHandlerTests` + `ShopLogoTests` + `DerivativePresetCatalog_For_Shop_Matches_Artifact`. Nguồn Service/ServiceGroup/ProductCategory/OG/favicon dùng lại cơ chế khi các module đó ra đời (Bước 9+) |
| **#74** | `resolveImage(imageId, preset)` **giữ chữ ký**; `preset` là assertion — lệch thì cảnh báo, vẫn render | `08` §0 · §6 | ✅ đã code — `resolveImage()` thật + cảnh báo lệch preset (`packages/builder-components/src/context.test.tsx`); chữ ký không đổi |
| **#75** | **File ảnh bất biến** — cấm ghi đè, áp cho mọi ảnh (`StorageKey` lẫn file Listing/Product); mọi thay đổi sinh đường dẫn mới | `08` §0 · §5 | ✅ enforce — `IObjectStorage.PutAsync` không ghi đè: `ObjectStorageContractTests.Put_twice_same_key_throws_and_keeps_original_content` (LocalDisk + S3/LocalStack) + `MediaDbConstraintTests.Unique_index_rejects_duplicate_StorageKey`. Phần file `Listing`/`Product` chưa có |
| **#76** | Tham chiếu ảnh vô hướng từ module khác dùng **FK ghép `(ImageId, ShopId) → media_assets (Id, ShopId)`**; code đọc qua Public Contract (#1) | `08` §0 · §2.2 | ✅ enforce — FK ghép `Shop(LogoId, Id) → MediaAsset(Id, ShopId)` + `MediaDbConstraintTests.Composite_FK_rejects_LogoId_pointing_to_another_shops_asset`. Mới có `Shop.LogoId`; các cột `ImageId` khác theo module tương ứng |
| **#77** | Tham chiếu `MediaAsset` không có FK (`imageId` trong tree JSONB) kiểm ở handler trong **một** câu query: cùng `ShopId`, chưa xoá, đúng loại record | `08` §0 · §3.5 | 📐 Bước 4 — `IMediaReferenceValidator` + `MediaReferenceValidatorTests` đã có, CHƯA nối vào handler nào (Bước 5) |
| **#78** | **9 preset** cho ảnh `MediaAsset` chốt ở `08` §7; ảnh Listing/Product không đi qua whitelist preset | `08` §0 · §7 | ✅ enforce — `config/image-presets.json` đủ 9 preset + `ImagePresetCatalogTests.Reads_Real_ImagePresets_File_With_Nine_Presets` + `gen-registry` hard-fail preset ngoài whitelist |
| **#79** | Ảnh **Listing/Product** không dùng `MediaAsset`: `ImageUrls text[]` = đường dẫn file full, `[0]` = đại diện; thư mục theo entity; `thumb_` mọi ảnh, `fthumb_` chỉ ảnh đại diện (sinh khi cần). Listing 640×480/160×120 (4:3); Product rộng 600/160 theo tỉ lệ danh mục | `08` §0 · §8 | 📐 Listing (Phase 1) · ⏳ Product (Bước 10) — mới có hàm quy ước tên C# `ImagePaths.Thumb`/`FeaturedThumb` (Bước 4); bản TS chưa có |
| **#80** | Ảnh variant = `ProductVariant.ImageUrls text[]` (rỗng = dùng ảnh product); file chung thư mục sản phẩm, nhiều variant dùng chung đường dẫn | `08` §0 · §8.3 | ⏳ Bước 10 |
| **#81** | Kiểm quyền lúc **ghi** đường dẫn ảnh (prefix thư mục của đúng entity dưới `shops/{shopId}/` + file tồn tại), **không** kiểm lúc đọc | `08` §0 · §8.4 | 📐 Listing (Phase 1) · ⏳ Product (Bước 10) |
| **#82** | Pipeline ảnh + object storage nằm ở namespace **dùng chung `Imaging`** (không thuộc module nào, thêm vào `SharedSegments`); upload logo là endpoint của `Media`, ghi `Shop.LogoId` qua Public Contract của `Shop`. `Media` giữ phase 2 | `08` §0 · §3.1 | ✅ enforce — `Imaging` trong `SharedSegments` (`ModuleBoundaryTests`); logo là endpoint `Media` ghi `Shop.LogoId` qua `IShopLogoWriter` (`ShopLogoHandlerTests`) |
| **#83** | Object storage sau một interface, **hai** implementation `LocalDisk` + `S3` chọn bằng setting `Storage:Provider` (mặc định `LocalDisk`); `/media/*` do .NET phục vụ qua cùng interface | `08` §0 · §5 | ✅ enforce — `ObjectStorageContractTests` chạy trên LocalDisk **và** S3 (LocalStack, không phải MinIO) + `MediaFileMiddlewareTests`/`MediaFileServingTests` (`/media/*` qua cùng interface). Chọn bằng `Storage:Provider`, mặc định `LocalDisk` |
| **#84** | Thư viện xử lý ảnh: **ImageSharp** (Six Labors Split License — miễn phí khi doanh thu năm < 1M USD, vượt thì mua license) | `08` §0 | ✅ đã code — `SixLabors.ImageSharp` 3.1.12 trong `Vsite.Infrastructure`; điều khoản license là việc pháp lý, không có máy chặn |
| **#85** | Giới hạn upload **10 MB / 25 MP** (số pixel kiểm từ header, trước decode); chỉ nhận JPEG/PNG/WebP; **HEIC bị từ chối** với `error_code` riêng + hướng dẫn ở FE | `08` §0 · §3.1 | ✅ enforce — `ImageSharpImageProcessorTests.Over_max_bytes_is_rejected` + `Over_max_pixels_is_rejected_before_decode` + `Heic_is_rejected_with_specific_code` (`MEDIA_HEIC_UNSUPPORTED`); hướng dẫn HEIC ở FE (`media-validation.test.ts`) |
| **#86** | Prop `binding` thêm field additive **`imagePresets`** (preset theo từng source); codegen sinh bộ phái sinh vào `packages/builder-components/generated/`, hợp với tập cố định của bề mặt vsite khai trong `config/image-presets.json` | `08` §0 · §3.6 | ✅ enforce — `gen-registry.test.ts` (fail khi key ngoài `sources` / preset ngoài whitelist) + artifact `derivative-presets.json` + `DerivativePresetCatalog_For_Shop_Matches_Artifact` |
| **#87** | Property object nullable trong OpenAPI luôn xuất dạng `{ allOf: [$ref], nullable: true }`, không phải object trùng `XDto2`. Chặn bằng transformer dùng chung, đăng ký cho mọi document module | `backend/CLAUDE.md` §OpenAPI | ✅ enforce — `DuplicateNullableSchema*Transformer` + test |
| **#88** | `ShopDto` **và `ShopSummaryDto`** (`GET /shops`) trả sẵn **`logoUrl`** = `"/media/" + storageKey` của phái sinh `320x96,inside` của logo (đường dẫn tương đối theo domain, dùng được trên mọi host; `null` nếu chưa có logo hoặc chưa có phái sinh). **Field DTO tên `*Url` mà Portal nhận do BE trả sẵn có tiền tố `/media/`**; DB vẫn chỉ lưu key tương đối; nội dung ảnh trong builder vẫn đi qua `resolveImage()`. Prefix + hàm dựng URL nằm ở MỘT chỗ BE (`ImagePaths.MediaUrl`, cùng hằng với route mount `/media`). `Shop` đọc qua port do chính `Shop` khai báo (`IShopLogoReader`, có bản tra theo lô một câu SQL cho `GET /shops`), adapter nằm ở `Media` (Shop không reference Media). Endpoint `GET …/library/{assetId}/derivatives` vẫn giữ cho các nguồn ảnh nghiệp vụ khác | `08` §0 · §3.6 · §5 | ✅ enforce — `ShopLogoTests` (`GetShop_and_PatchShop_after_logo_return_prefixed_logoUrl_readable_via_media`, `ListShops_returns_logoUrl_…`) + `ShopLogoBatchSqlCountTests` (một câu SQL) + `ImagePathsMediaUrlTests` + `ModuleBoundaryTests` (Shop không reference Media) |
| **#89** | **Gate 1 gọn trước production:** `contract-diff.md` chỉ ghi endpoint/field mới, auth, giả định, câu hỏi, bảng quyết định — không phân tích nhãn `BREAKING`. Chỉ thêm và **không có câu hỏi** → promote ngay, người đọc lại sau; có câu hỏi hoặc `REMOVED` → dừng. Checklist Gate 1 bắt buộc chốt mọi "Câu hỏi mở" của `data-needs.md` và chạy thử Orval trên staging. **Đảo lại** từ lần deploy production đầu tiên (sửa cùng Rule 5) | `ai-agent-development-workflow.md` §6 | ✅ quy trình — skill `contract-sync` bước 5–6 |
| **#90** | **Vòng đời membership trong luồng auth:** chỉ `UserShop` chưa xoá mềm + `Active` (và `User` `Active`) được đăng nhập/làm mới token/reset password/vào `ownerShopIds`; refresh khi hết hiệu lực → 401 + thu hồi token cùng scope; đăng ký lại sau khi membership bị xoá mềm → **khôi phục** (Customer, Active, `Source` giữ nguyên); đang Suspended/Invited → **409** | `backend/docs/modules/identity.md` §Quyết định #90 | ✅ enforce — `MembershipLifecycleTests` (8 test) + `IgnoreQueryFiltersAllowlistTests` + helper `UserShopQueries` |
| **#91** | **Mọi endpoint API nằm dưới `/api`** (trừ `/media/*` file ảnh public và `/openapi/*` dev) và **mọi endpoint có `operationId` tường minh** đặt theo nghiệp vụ (camelCase, vd. `listShops`) — tên hàm/hook FE không còn phụ thuộc path. Lý do: API ở root đè namespace URL của slug shop và route SPA Portal | `backend/CLAUDE.md` §OpenAPI | ✅ enforce — `ApiRoutePrefixTests` (prefix + operationId bắt buộc, không trùng) |
| **#92** | **Mọi bảng thuộc shop có `ShopId`** (kể cả bảng con 1:1) và **mọi FK tới entity thuộc shop là FK ghép** `(X, ShopId) → (Id, ShopId)`; giữ khoá int identity (chấp nhận lộ bộ đếm); `ON DELETE SET NULL` trên FK ghép phải ghi cột `SET NULL (X)` | `05` §0 · áp dụng `05` §2–§19, `06` §4–§6, `04` §4.1 | 📐 Bước 5+ — mẫu đang chạy: `shop(logo_id, id) → media_asset(id, shop_id)` |
| **#93** | **Địa chỉ 2 cấp** (tỉnh + xã/phường, từ 01/07/2025) qua bảng `administrative_unit` có hiệu lực theo thời gian; **"khu vực" = quận/huyện cũ** suy ra từ phường qua bảng ánh xạ, chỉ dùng cho tìm kiếm + landing SEO (`vsite.vn/spa/quan-7`); lọc theo phường thì `noindex` | `04` §4.5 | 📐 Phase 1 (Marketplace) |
| **#94** | Document JSONB vòng đời dài (`Tree`, `Snapshot`, `Tokens`, `NavigationConfig.Items`) có **`schemaVersion` ở cấp gốc** + upcaster khi đọc; node vẫn không có version (#43) | `05` §0 · §3, §6–§8, §10 | 📐 Bước 5 |
| **#95** | `CategoryAttribute`: bỏ sentinel `CategoryId = 0` → `CategoryId` nullable (`NULL` = toàn shop) + `ShopId` + `UNIQUE NULLS NOT DISTINCT (ShopId, CategoryId, AttributeId)` | `05` §0 · §16 | 📐 Bước 10 |

---

## #21.x — Tenant security invariants

Vi phạm = **lỗi bảo mật**, không phải lỗi code style. Nguyên văn tại `02 §Quyết định #21`.

| # | Invariant |
|---|---|
| **#21.1** | Mọi entity tenant-scoped **phải** có `ShopId` |
| **#21.2** | Mọi query **phải** đi qua Global Query Filter theo `TenantContext`. `PlatformAdmin` **không** được bypass ngầm — muốn admin xem dữ liệu shop thì làm **endpoint riêng, audit log riêng** (`03` §7) |
| **#21.3** | Child resource validate ownership **trong câu query**, không load rồi check ở memory |
| **#21.4** | **Không bao giờ** nhận `ShopId` từ request body — chỉ từ route hoặc `TenantContext` |
| **#21.5** | Quyền theo shop kiểm ở **Authorization Handler**, không tin claim trong token — token cũ còn hiệu lực tới hết TTL sau khi revoke |

## #39.x — Đóng mâu thuẫn liên tài liệu

| # | Nội dung |
|---|---|
| **#39.1** | "MVP" = **Phase 1**. Lộ trình `01` §8 là nguồn sự thật duy nhất cho *khi nào làm gì* |
| **#39.2** | Tên app cũ (`customer-web` / `shop-admin` / `website-builder`) bị xoá hẳn — chỉ còn `apps/web`, `apps/portal` |
| **#39.3** | Trang hồ sơ shop là `vsite.vn/shop/{slug}`, **không** phải `vsite.vn/{slug}` |
| **#39.4** | Quyền ghi `Review`: khách viết cần audience `vsite-main`; shop phản hồi cần `vsite-portal` + role `Owner`/`Manager`; shop **không** có quyền gỡ |
| **#39.5** | Một khái niệm — một chỗ định nghĩa. Hai tài liệu cùng tả một entity thì **tài liệu thiết kế chi tiết thắng** |

---

## Liên kết

- Cửa vào tài liệu, trạng thái từng file, việc đang mở: [`00-INDEX.md`](00-INDEX.md)
- Thứ tự triển khai 11 bước: [`step.md`](step.md)
- Nợ test cần Docker: [`../Docs/DOCKER-TEST-DEBT.md`](../Docs/DOCKER-TEST-DEBT.md)
