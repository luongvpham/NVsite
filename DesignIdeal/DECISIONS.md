# vsite — Registry Quyết Định (#1–#86)

> **File này là CHỈ MỤC, không phải nội dung.** Mỗi dòng cho bạn biết quyết định đó nói gì trong một
> câu, nó được **định nghĩa đầy đủ ở đâu**, và nó **đã thành code chưa**. Đọc dòng ở đây trước, rồi
> mở đúng một chỗ — đừng quét cả `02` (74 KB) để tìm một quyết định.
>
> **Cách mở nhanh:** cột "Định nghĩa tại" ghi pattern grep được nguyên văn.
> Ví dụ `02 §Quyết định #21` → `grep -n "### Quyết định #21" DesignIdeal/02-tech-stack-and-decision.md`.

---

## ⚠️ Đọc trước khi tra

**Không phải quyết định nào cũng nằm trong `02`.** `02` định nghĩa **#1–#39** và **#68**.
**#40–#58** định nghĩa tại `05` §0, **#59–#67** định nghĩa tại `07` §0, **#69–#86** định nghĩa tại `08` §0.
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
| **#21** | **Tenant security invariants** — 5 mục, xem #21.1–#21.5 dưới bảng | `02 §Quyết định #21` | ✅ enforce — `TenantIsolationTests.cs` ⚠️ **chưa chạy thật, cần Docker** |
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
| **#53** | Pipeline ảnh dùng chung (magic bytes, strip EXIF, webp, ≤1600px); file đúng kích thước **sinh sẵn lúc đặt/upload — không có image proxy runtime**; URL = `{domain bất kỳ}/media/{đường dẫn}`, `media` là reserved route, đọc không kiểm tenant; luôn qua `resolveImage()` | `05` §0 · chi tiết `08` §1, §3, §5 | 📐 Bước 4 |
| **#54** | `Service` **không** có `System` page riêng — hiển thị qua component bind trên trang `Composable` | `05` §0 · `06` §6 | 📐 Bước 9 |
| **#55** | **Không có bảng `MediaVariant`** — mỗi file `MediaAsset` là một record (`Preset` + `SourceAssetId`); ảnh Listing/Product không có bảng. Dọn: job quét tham chiếu (MediaAsset, Bước 8) / xoá thư mục entity (Listing/Product). Quota MediaAsset = `SourceAssetId IS NULL` | `05` §0 · chi tiết `08` §2, §4, §8 | 📐 Bước 4 |
| **#56** | Ảnh sản phẩm (`Product.ImageUrls`) và `media_assets` là **hai hệ độc lập**; dùng ảnh sản phẩm trong builder phải "Thêm vào thư viện" (tạo bản Library) rồi mới chọn → clone (#71). Builder picker **không bao giờ** thấy ảnh sản phẩm trực tiếp | `05` §0 · §20 | ⏳ Bước 10 |
| **#57** | Tỉ lệ ảnh sản phẩm do **danh mục** quyết định (`ShopProductCategory.ImageRatio`); file full crop theo tỉ lệ **lúc upload**; đổi tỉ lệ thì ảnh cũ giữ nguyên, không có job sinh lại | `05` §0 · §13 | ⏳ Bước 10 |
| **#58** | **Không** hỗ trợ `srcset` responsive ở Phase 2; mỗi vị trí một preset cố định ~2× độ rộng CSS; hoãn được nhờ chữ ký `resolveImage()` giữ nguyên (#74) | `05` §0 | 📐 |
| **#59** | Manifest là file `.ts` object literal thuần — không import runtime, không hàm, không điều kiện | `07` §0 · §4 | ✅ enforce — `meta/manifest-schema.ts` (Zod) |
| **#60** | BE **không** port Zod sang C#; codegen sinh `props-schemas.json` (JSON Schema 2020-12) | `07` §0 · §11 | ✅ enforce — `tests/schema-equivalence.test.ts` |
| **#61** | Props khai ở tầng `type`; `variant` chỉ khai `usesProps` / `requiresProps` | `07` §0 · §4.2 | ✅ enforce — `meta/manifest-schema.ts` |
| **#62** | `registry.lock.json` commit vào repo; CI so lock, breaking → fail build | `07` §0 · §5 | ✅ enforce — `scripts/check-additive.ts` |
| **#63** | `kind` của prop là **tập đóng 12 giá trị**; người viết component không được phát minh kind mới | `07` §0 · §4.3 | ✅ enforce — `meta/prop-kinds.ts` |
| **#64** | Mọi prop `kind: "image"` **bắt buộc** khai `preset` nằm trong `config/image-presets.json`; whitelist là lint build-time + bảng kích thước cho pipeline BE | `07` §0 · §7.1 | ✅ enforce — `gen-registry.ts` hard-fail + `tests/config-files.test.ts` ⚠️ config chưa khớp 9 preset của #78, sửa ở Bước 4 |
| **#65** | Prop `kind: "binding"` khai `sources` tường minh; codegen **hard-fail** nếu chứa `"Review"` | `07` §0 · §7.4 | ✅ enforce — `gen-registry.ts` + `config/binding-sources.json` + test |
| **#66** | Nhãn Inspector là tiếng Việt thuần ở Phase 2, không dùng i18n key | `07` §0 | ✅ đã code |
| **#67** | Sanitize `richText` — hai profile `inline` / `basic` | `07` §0 · §7.2 | ✅ **ĐÃ CHỐT + đã code** — `config/sanitize-profiles.json` + `src/sanitize-html.ts`. ⚠️ `07` §7.2 vẫn ghi "cần xác nhận" — **nhãn đó sai, bỏ qua** |
| **#68** | Component Registry manifest triển khai ở Bước 2, **trước** Identity | `02 §Quyết định #68` | ✅ đã xong — BOOTSTRAP-002 |
| **#69** | **Một bảng `MediaAsset`**; `Preset IS NULL` ⟺ `IsInLibrary = true` (CHECK hai chiều); không có cột `Purpose` | `08` §0 · §2 | 📐 Bước 4 |
| **#70** | Hai chế độ upload vào slot: tick thư viện → bản Library + clone; không tick → một record crop đúng preset, **không giữ gốc**. Không có "lưu vào thư viện" sau upload; focal point chọn trước khi crop | `08` §0 · §3.2, §3.3 | 📐 Bước 4 |
| **#71** | Lấy ảnh từ Library **luôn clone**; tree **không bao giờ** chứa id `IsInLibrary = true`; clone chỉ sinh từ bản Library, không từ clone | `08` §0 · §3.4 | 📐 Bước 4 |
| **#72** | Clone **không** cascade delete theo node; dọn theo retention `SitePublication` | `08` §0 · §4 | 📐 Bước 4 (không xoá) · Bước 8 (job dọn) |
| **#73** | **Ảnh dữ liệu nghiệp vụ** (logo, Service, ServiceGroup, ProductCategory, OG, favicon) = bản Library + bộ phái sinh sinh sẵn lúc upload; preset suy ra bằng codegen từ manifest bind nguồn + tập cố định của bề mặt vsite. Không áp cho ảnh Listing/Product (#79) | `08` §0 · §3.6 | 📐 Bước 4 |
| **#74** | `resolveImage(imageId, preset)` **giữ chữ ký**; `preset` là assertion — lệch thì cảnh báo, vẫn render | `08` §0 · §6 | 📐 Bước 4 |
| **#75** | **File ảnh bất biến** — cấm ghi đè, áp cho mọi ảnh (`StorageKey` lẫn file Listing/Product); mọi thay đổi sinh đường dẫn mới | `08` §0 · §5 | 📐 Bước 4 |
| **#76** | Tham chiếu ảnh vô hướng từ module khác dùng **FK ghép `(ImageId, ShopId) → media_assets (Id, ShopId)`**; code đọc qua Public Contract (#1) | `08` §0 · §2.2 | 📐 Bước 4 |
| **#77** | Tham chiếu `MediaAsset` không có FK (tree, `Page.OgImageId`) kiểm ở handler trong **một** câu query: cùng `ShopId`, chưa xoá, đúng loại record | `08` §0 · §3.5 | 📐 Bước 4 |
| **#78** | **9 preset** cho ảnh `MediaAsset` chốt ở `08` §7; ảnh Listing/Product không đi qua whitelist preset | `08` §0 · §7 | 📐 Bước 4 — `config/image-presets.json` đang có 6 preset cũ |
| **#79** | Ảnh **Listing/Product** không dùng `MediaAsset`: `ImageUrls text[]` = đường dẫn file full, `[0]` = đại diện; thư mục theo entity; `thumb_` mọi ảnh, `fthumb_` chỉ ảnh đại diện (sinh khi cần). Listing 640×480/160×120 (4:3); Product rộng 600/160 theo tỉ lệ danh mục | `08` §0 · §8 | 📐 Listing (Phase 1) · ⏳ Product (Bước 10) |
| **#80** | Ảnh variant = `ProductVariant.ImageUrls text[]` (rỗng = dùng ảnh product); file chung thư mục sản phẩm, nhiều variant dùng chung đường dẫn | `08` §0 · §8.3 | ⏳ Bước 10 |
| **#81** | Kiểm quyền lúc **ghi** đường dẫn ảnh (prefix thư mục của đúng entity dưới `shops/{shopId}/` + file tồn tại), **không** kiểm lúc đọc | `08` §0 · §8.4 | 📐 Listing (Phase 1) · ⏳ Product (Bước 10) |
| **#82** | Pipeline ảnh + object storage nằm ở namespace **dùng chung `Imaging`** (không thuộc module nào, thêm vào `SharedSegments`); upload logo là endpoint của `Media`, ghi `Shop.LogoId` qua Public Contract của `Shop`. `Media` giữ phase 2 | `08` §0 · §3.1 | 📐 Bước 4 |
| **#83** | Object storage sau một interface, **hai** implementation `LocalDisk` + `S3` chọn bằng setting `Storage:Provider` (mặc định `LocalDisk`); `/media/*` do .NET phục vụ qua cùng interface | `08` §0 · §5 | 📐 Bước 4 |
| **#84** | Thư viện xử lý ảnh: **ImageSharp** (Six Labors Split License — miễn phí khi doanh thu năm < 1M USD, vượt thì mua license) | `08` §0 | 📐 Bước 4 |
| **#85** | Giới hạn upload **10 MB / 25 MP** (số pixel kiểm từ header, trước decode); chỉ nhận JPEG/PNG/WebP; **HEIC bị từ chối** với `error_code` riêng + hướng dẫn ở FE | `08` §0 · §3.1 | 📐 Bước 4 |
| **#86** | Prop `binding` thêm field additive **`imagePresets`** (preset theo từng source); codegen sinh bộ phái sinh vào `packages/builder-components/generated/`, hợp với tập cố định của bề mặt vsite khai trong `config/image-presets.json` | `08` §0 · §3.6 | 📐 Bước 4 |

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
