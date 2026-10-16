# WEBSITE-001 — Website → Theme → Page → PageDraft (Bước 5a, backend)

> **ĐÃ DUYỆT 2026-10-15** — trừ một việc của người: thêm key `reservedShopSitePaths` vào
> `config/reserved-routes.json` (hook chặn agent ghi file này) **trước S0-T1**. Xem §4.
>
> **Lane C.** Module mới (`Website`), chạm contract (module OpenAPI mới + `media.v1` thêm nguồn tham chiếu),
> chạm config dùng chung (`config/`) và codegen (`gen-registry`). Cổng: Gate 1 (#89) → Gate 2.
> Tuần tự **S0 → S1** (S2 FE = Bước 5b, **task sau**, không thuộc plan này).
>
> **Người thực thi dùng skill:** `superpowers:subagent-driven-development` hoặc
> `superpowers:executing-plans`. Mỗi bước có checkbox `- [ ]`.

**Mục tiêu:** Chủ shop `Hosted` có một website trắng (trang chủ + Contact + NotFound), sửa được cây component
của từng trang và theme qua API; BE tự chặn mọi cây sai (kể cả khi bỏ qua FE) và chặn hai tab ghi đè nhau.
**Nhu cầu dữ liệu:** [`data-needs.md`](data-needs.md) · **Spec:** `05` §1–§8, §11, §26 · `07` §11 ·
`step.md` 5a · **Quyết định:** #21 #33 #42 #46 #60 #67 #71 #76 #77 #89 #92 #94 #97 #98.

## 1. Yêu cầu (đã chốt với người)
- **Vai trò:** **chỉ `Owner`** của shop `Hosted` (#100). Manager/Staff/Accountant/`Customer` → 403. Admin không chạm.
- **Điểm chạm:** 10 endpoint dưới `/api/shops/{shopId}/website…` (§9 T10) + mở rộng `getAssetReferences` của Media
  + **vá phân quyền Media** — mọi endpoint `/api/shops/{shopId}/media/**` thành Owner-only (T10b).
- **Xong nghĩa là** (`step.md` 5a): hai tab cùng sửa một trang → tab cũ nhận **409**. Cây chứa id bản Library,
  id ảnh của shop khác, `binding.source = "Review"`, hoặc richText có `<script>` → **BE từ chối**, dù FE bị bỏ qua.
  Test tích hợp chạy thật trên Docker (máy này **có** Docker 29.8.0, đã kiểm). Có `changelog.md`.
- **Không làm trong task này:**
  - Editor / Operations Engine / mọi màn FE (5b). Không `Orval gen`, không sửa `apps/*`.
  - `SitePublication`, publish/rollback, `Website.CurrentPublicationId` (cột + FK ghép thêm ở 8b, additive).
  - `NavigationConfig` (Bước 7). `WebsiteTemplate` + `Website.SeedTemplateId` (Bước 11).
  - `*_translation`, overlay `i18n` (#98 chỉ yêu cầu `DefaultLocale`/`Locales` + chặn slug ở 5a).
  - API sửa `Locales`/`DefaultLocale`/SEO mặc định/tracking (§12 đề xuất 8). Hangfire, Product, Service.
  - Dọn handler Media cũ sang `ITenantContext` (ghi nợ). (Phân quyền Media **thì có làm** — T10b.)
  - `GET /api/shops/{shopId}` (ShopEndpoints) vẫn mở cho mọi member, kể cả `Customer` — ngoài phạm vi, ghi nợ.

## 2. Hiện trạng (khảo sát 2026-10-13)

| Khả năng | Trạng thái | Ở đâu | Ghi chú bằng lời thường |
|---|---|---|---|
| Module `Website` khai ở dependency-map | ✅ | [dependency-map.json:20](../../architecture/dependency-map.json#L20) | `dependsOn: [Shop, Media]` — được đọc `Shop`/`Media`; **Media/Shop không được** đọc ngược Website |
| Entity `Website`/`Theme`/`Page`/`PageDraft` | ❌ | — | Chưa có dòng code nào; `DbSet` chưa có ([AppDbContext.cs:34-46](../../../backend/src/Vsite.Infrastructure/Persistence/AppDbContext.cs#L34)) |
| Base class tenant | ✅ | [TenantAuditableEntity.cs:5](../../../backend/src/Vsite.Domain/Common/TenantAuditableEntity.cs#L5) | Có `ShopId` (`init`) + audit + soft-delete; Global Query Filter tự áp |
| Mẫu FK ghép `(X, ShopId)` | ✅ | [ShopLogoConfiguration.cs:23-27](../../../backend/src/Vsite.Infrastructure/Persistence/Configurations/Media/ShopLogoConfiguration.cs#L23), [MediaAssetConfiguration.cs:71](../../../backend/src/Vsite.Infrastructure/Persistence/Configurations/Media/MediaAssetConfiguration.cs#L71) | `HasAlternateKey(Id, ShopId)` làm đích; FK chỉ áp khi cột nullable ≠ NULL (MATCH SIMPLE) |
| `IMediaReferenceValidator` | 🟡 có, **chưa ai gọi** | [MediaReferenceValidator.cs:26-72](../../../backend/src/Vsite.Infrastructure/Media/MediaReferenceValidator.cs#L26) | Một câu `COUNT`; đọc `ITenantContext.ShopId`; tree nhận `Kind ∈ {Clone, Direct}`, business nhận `Library`; lỗi → 422 `MEDIA_INVALID_IMAGE_REFERENCE` |
| `ITenantContext` + filter tenant | ✅ | [ITenantContext.cs:22](../../../backend/src/Vsite.Domain/Abstractions/ITenantContext.cs#L22), [ShopMembershipEndpointFilter.cs:78-89](../../../backend/src/Vsite.Api/Tenancy/ShopMembershipEndpointFilter.cs#L78) | Filter tra membership rồi set context từ route `{shopId}`; Query Filter fail-closed khi null |
| Quy ước ShopId handler | 🟡 hai nguồn | [UploadToSlotHandler.cs:29](../../../backend/src/Vsite.Application/Media/Commands/UploadToSlot/UploadToSlotHandler.cs#L29) vs validator | Đã chốt: Website dùng `ITenantContext` |
| Endpoint `references` | 🟡 chỉ `Shop.LogoId` | [GetReferencesHandler.cs:15-27](../../../backend/src/Vsite.Application/Media/Queries/GetReferences/GetReferencesHandler.cs#L15), [MediaReferencesDto.cs:14-20](../../../backend/src/Vsite.Application/Media/Dtos/MediaReferencesDto.cs#L14) | Enum `MediaReferenceKind` chỉ có `ShopLogo`; có TODO chờ Bước 5 |
| Map lỗi toàn cục | 🟡 | [AppExceptionHandler.cs:17](../../../backend/src/Vsite.Api/ExceptionHandling/AppExceptionHandler.cs#L17) | Chỉ xử lý `AppException`; `ConflictException` 409 có sẵn. **`DbUpdateConcurrencyException` và 23505 → 500** |
| Concurrency `xmin` | ❌ | — | Chưa entity nào dùng |
| BE đọc config ở repo root | ✅ mẫu | [Vsite.Api.csproj:39-58](../../../backend/src/Vsite.Api/Vsite.Api.csproj#L39), [Program.cs:56-65](../../../backend/src/Vsite.Api/Program.cs#L56), [ReservedRoutesProvider.cs:14-27](../../../backend/src/Vsite.Infrastructure/Configuration/ReservedRoutesProvider.cs#L14) | `<None Link>` + provider singleton + `File.ReadAllText`, fail-fast ngay sau `Build` |
| BE đọc `sanitize-profiles.json` | ❌ | [sanitize-profiles.json](../../../config/sanitize-profiles.json) | Chỉ FE đọc ([sanitize-html.ts:2](../../../packages/builder-components/src/sanitize-html.ts#L2)); BE **không có** thư viện sanitize HTML ([Directory.Packages.props](../../../backend/Directory.Packages.props)) |
| BE đọc `binding-sources.json` | ❌ | [binding-sources.json](../../../config/binding-sources.json) | `["Product","ProductCategory","Service","ServiceGroup","MediaAsset","Shop"]` — không có `Review` |
| `props-schemas.json` cho BE | 🟡 | `packages/builder-components/generated/props-schemas.json` (gitignore) | Chỉ test đọc ([PropsSchemaEquivalenceTests.cs:19-85](../../../backend/tests/ComponentSchemaTests/PropsSchemaEquivalenceTests.cs#L19)); `JsonSchema.Net 9.4.0` đã pin nhưng chưa ở project prod |
| Luật op cho BE (`allowedInPageKinds`, `editableInSystemPage`, `acceptsChildren`, `maxPerPage`) | 🟡 chỉ `.ts` | [op-rules.ts](../../../packages/builder-components/generated/op-rules.ts) | BE **không đọc được `.ts`** → cần artifact JSON (S0) |
| `config/theme-tokens.json`, `config/locales.json` | ❌ | — | `config/` mới có 4 file |
| Zod/JSON Schema cho vỏ Tree/Tokens | ❌ | — | grep `schemaVersion` ở `packages/` = 0 hit |
| Fixture XSS dùng chung hai phía | ❌ | — | `07` §10 mô tả `xss-richtext.json` "chạy cả Node và .NET" nhưng **file không tồn tại** |
| Docker/Testcontainers | ✅ | [PostgresFixture.cs:10-30](../../../backend/tests/IntegrationTests/Common/PostgresFixture.cs#L10), [MediaTenantIsolationTests.cs:15](../../../backend/tests/IntegrationTests/Media/MediaTenantIsolationTests.cs#L15) | Mẫu test tenant isolation DB + HTTP có sẵn |

### Phát hiện khi khảo sát

1. **`props-schemas.json` không đủ để BE validate "toàn tree".** Nó chỉ kiểm shape từng prop
   (xem `ServiceGrid.source` chỉ có `filters`, **không** enum `binding.source`; `richText` chỉ có `maxLength`,
   không biết profile nào; không `additionalProperties: false`). BE còn cần biết **prop nào là ảnh / richText
   (profile gì) / binding (sources gì)** và luật op. → S0 sinh thêm artifact `tree-rules.json`.
   `CLAUDE.md` của package xác nhận đây là chủ ý ("mọi prop optional … để dành cho Operations Engine (Bước 5)").
2. **Media không được gọi ngược sang Website.** Mở rộng `references` (MEDIA-001 #2) nếu viết thẳng trong
   `GetReferencesHandler` sẽ tạo phụ thuộc Media→Website (cấm, `ModuleBoundaryTests`). → Media khai port
   `IMediaReferenceSource` ở `Application/Media/Interfaces`, Website implement (cùng khuôn `IShopLogoReader`).
3. **Tree không bao giờ chứa id Library (#71)**, nên "ảnh Library X đang dùng ở trang nào" thực ra là
   "clone của X nằm trong tree nào" — phải join `SourceAssetId`, không so thẳng `assetId`.
4. **Không có danh sách route System page ở đâu cả** ngoài bảng `05` §4, mà `05` §4 đòi Slug không trùng
   route System page → thiếu nguồn duy nhất (#24 tinh thần). → đề xuất `config/system-pages.json` (§12 G3).
5. **Tên entity `Website` trùng tên module** — `backend/CLAUDE.md` cảnh báo phải alias như `Shop`. Dùng
   `using WebsiteEntity = Vsite.Domain.Website.Entities.Website;`.
6. Comment ở [IMediaReferenceValidator.cs](../../../backend/src/Vsite.Application/Media/Interfaces/IMediaReferenceValidator.cs)
   nói `IsInLibrary` nhưng code kiểm theo `Kind` — **chỉ lệch chữ**, không sửa (ngoài phạm vi), ghi changelog.
7. Không có test nào chứng minh "validator thật sự chạy trong pipeline request" — T7 phải có test request-level
   (đóng nợ MEDIA-001 #1).
8. **(review 2026-10-15) `RequireShopMembership` nhận MỌI role còn hiệu lực, kể cả `Customer`**
   ([UserShopMembershipService.cs:15](../../../backend/src/Vsite.Infrastructure/Identity/UserShopMembershipService.cs#L15)).
   Khách đăng ký ở domain shop có membership `Customer` ([VerifyEmailHandler.cs:90](../../../backend/src/Vsite.Application/Identity/Auth/Commands/VerifyEmail/VerifyEmailHandler.cs#L90));
   cùng email đăng ký ở `vsite.vn` (#29) là có token global → qua `RequireGlobalScope` + filter membership →
   gọi được Media hôm nay, và Website nếu không chặn. → #100: Website + Media chỉ Owner.
9. **`/media/*` phục vụ trên mọi domain shop** → trang slug `media` bị che. Cùng loại: `api`, đường asset
   của `apps/web`. `reservedPaths` không dùng thẳng được (chặn oan `blog`/`about`/`help`) → cần danh sách riêng.

## 3. Flow

### Flow 1: Tạo website trắng
```
1. Portal gọi POST /shops/{shopId}/website            ❌ endpoint mới (T10)
2. Filter kiểm Owner + set ITenantContext             ✅ RequireShopOwner — ShopMembershipEndpointFilter.cs:78 (mọi endpoint Website, #100)
3. Shop phải Kind = Hosted                            ❌ → 422 WEBSITE_REQUIRES_HOSTED_SHOP (Shop.Kind: Shop.cs:54)
4. Đã có website → 409 WEBSITE_ALREADY_EXISTS         ❌ (UNIQUE ShopId ở DB chặn race → 23505 → 409 chung, T8)
5. Tạo 1 transaction: Website(vi,{vi}) + Theme(tokens mặc định)
   + Page "" (Composable) + Page lien-he (System/Contact) + Page NotFound (System, KHÔNG có route công khai, NoIndex)
   + 3 PageDraft, id node sinh server-side            ❌ (T6)
6. Trả WebsiteDto + id trang chủ                      ❌
```
Nhánh lỗi: shop `ExternalOnly` → 422; shop không phải của mình → 403 (filter); thiếu `Hosted`… đã nêu. Tạo bù
khi sau này thêm System page mới (ProductListing…): **ngoài 5a** nhưng schema/seed phải cho phép gọi lại
(`EnsureSystemPages`, T6 viết idempotent — chỉ thêm cái thiếu, không đụng cái có).

### Flow 2: Mở trang & autosave
```
1. GET  …/pages/{pageId}/draft  → {tree, version, isDirty, updatedAt, page.kind}   ❌ T10
2. PUT  …/pages/{pageId}/draft  {tree, version}
   a. Khớp version?  không → 409 DRAFT_CONFLICT (+ currentVersion)                  ❌ T9
   b. schemaVersion > hiện hành → 422; cũ → upcaster                                 ❌ T5
   c. Giới hạn: kích thước body, độ sâu, số node                                     ❌ T4
   d. Vỏ cây theo JSON Schema sinh từ Zod (#97)                                      ❌ S0 + T4
   e. Từng node: type/variant tồn tại · id duy nhất · allowedInPageKinds ·
      acceptsChildren/allowedChildTypes/maxPerPage · props theo props-schemas (#60) ·
      prop lạ → từ chối · key `i18n` → từ chối (chưa bật #98)                        ❌ T4
   f. Binding: source ∈ binding-sources.json & ∈ sources khai ở prop (Review bị loại) ❌ T4
   g. richText: sanitize theo profile của prop. Phần bị cắt có thứ NGUY HIỂM
      (script/iframe/object/embed/style, on*, javascript:/data:/vbscript:) → 422
      RICHTEXT_UNSAFE; còn lại → thay bằng bản sạch rồi lưu, không báo lỗi            ❌ T4/T3
   h. Ảnh: gom imageId → IMediaReferenceValidator (id Library / shop khác / đã xoá
      / sai Kind → 422 MEDIA_INVALID_IMAGE_REFERENCE)                                 ❌ T7
   i. System page: so với bản đang lưu — cùng id/type/variant/thứ tự/children;
      chỉ prop trong editableInSystemPageProps được đổi                               ❌ T5b
   j. Ghi; xmin lệch lúc SaveChanges (race) → DbUpdateConcurrencyException → 409      ❌ T9
3. Trả {version mới, isDirty} — không trả lại tree
```
Thứ tự **c→i** chạy trước khi chạm DB trừ (h) và so sánh System (i) cần bản đang lưu; lỗi trả **hết một lần**
(`errors[]`, tối đa 50) để Inspector tô đỏ nhiều ô, không phải sửa từng cái.

### Flow 3: Page metadata
`GET list` ✅ mới · `POST` (Composable) · `PATCH` · `DELETE` (chỉ `Composable` và không phải `Slug = ""`,
còn lại → 409 `PAGE_NOT_DELETABLE`, kiểm ở handler, không dựa FK — `05` §4). Luật Slug: `^[a-z0-9]+(-[a-z0-9]+)*$`,
≤120, `""` chỉ cho trang chủ; không trùng mã trong `locales.json` (**kể cả locale chưa bật**, #98); không trùng
**mọi** route System page trong `system-pages.json` (kể cả loại chưa seed); không trùng `reservedShopSitePaths`
(`config/reserved-routes.json` — `media`, `api`…); → 422 `PAGE_SLUG_RESERVED`. Trùng trong website → 409 `PAGE_SLUG_TAKEN`. `ogImageId` qua validator
(bản Library). Page xoá mềm, unique `(WebsiteId, Slug)` **lọc** `NOT is_deleted` (cho dùng lại slug).

### Flow 4: Theme
`GET`/`PUT` `…/website/theme`: tokens theo schema (`theme-tokens.json` + JSON Schema vỏ), `schemaVersion` ở gốc;
ghi cả tài liệu; `UpdatedAt` đóng dấu. Sai màu/font ngoài whitelist → 422 `THEME_INVALID` (`errors[]` cùng dạng).
Concurrency Theme: **không** (một người/lần — ghi cuối thắng; §12 G7).

### Flow 5: Xoá ảnh khỏi Library → cảnh báo đang dùng
```
getAssetReferences → [ShopLogo?] + mỗi IMediaReferenceSource (Website: PageDraft)   ❌ T11
Website source: lấy clone có SourceAssetId = assetId (cùng shop) ∩ imageId trong mọi PageDraft.Tree của shop
```

## 4. Đã chốt trong session lập plan

| # | Chốt |
|---|---|
| **#99** (đã cấp số 2026-10-15) | **Handler tenant-scoped lấy `ShopId` từ `ITenantContext` duy nhất.** Command chỉ mang id con. Ngoại lệ: lệnh tạo aggregate gốc (`CreateWebsite`) nhận `shopId` tường minh. Handler Media/Shop cũ không đổi (nợ dọn riêng). T13 chuyển nội dung sang `backend/docs/modules/website.md` |
| **#100** (đã cấp số 2026-10-15) | **Website và Media chỉ `Owner`** — mọi endpoint `/api/shops/{shopId}/website…` và `/api/shops/{shopId}/media/**`, đọc lẫn ghi, gắn `RequireShopOwner`. Lý do: `RequireShopMembership` nhận cả `Customer` (§2 phát hiện 8). Mở cho Manager/Staff là quyết định sau, khi có phân quyền nhân viên (Phase 4) |
| richText (chi tiết #67) | Chỉ **từ chối** khi phần bị cắt là thứ nguy hiểm (`script`/`iframe`/`object`/`embed`/`style`, `on*`, `javascript:`/`data:`/`vbscript:`) → 422 `RICHTEXT_UNSAFE`; mọi khác biệt khác → **lưu bản sạch** (data-needs câu 5) |
| `NotFound` | **Không có route công khai.** `Page.Slug` = `NULL` ⟺ `SystemType = NotFound` (CHECK); `NoIndex = true`. Renderer (8b) dùng trang này khi trả 404, tra theo `SystemType`. Lệch `05` §4 (Slug NOT NULL) — ghi changelog |
| `reservedShopSitePaths` | **Người** thêm vào `config/reserved-routes.json` (hook chặn agent), đề xuất `["media","api","assets","static"]` — đường dẫn bị chiếm trên **domain shop**. Slug trang không được trùng. Test `ReservedRoutesTests` mở rộng đọc key mới |
| #97 (chi tiết) | `config/locales.json` = `["vi","en"]`. `config/theme-tokens.json` = `{ "colorKeys": ["primary","secondary","background","surface","text","textMuted","border"], "colorPattern": "^#[0-9a-fA-F]{6}$", "fonts": ["Be Vietnam Pro","Inter","Roboto","Montserrat","Playfair Display","Lora"], "scale": ["compact","comfortable","spacious"], "radius": ["none","sm","md","lg","full"], "shadow": ["none","soft","medium","strong"], "sectionGap": ["sm","md","lg","xl"] }` (danh sách font là đề xuất của tôi, bạn sửa được). Shape vỏ viết Zod → JSON Schema trong `packages/builder-components` |
| — | System page seed: `Contact` (`lien-he`), `NotFound` (không route), mỗi trang tree `Page › RichText`; trang chủ `""` tree rỗng |

## 5. Trình tự session

| Session | Nội dung | Vì sao thứ tự này | Cổng ra |
|---|---|---|---|
| **S0** | Config + codegen: `locales.json`, `theme-tokens.json`, `system-pages.json`, Zod vỏ → JSON Schema, artifact `tree-rules.json`, fixture dùng chung | BE đọc artifact của S0; không có thì T4 phải tự viết danh sách thứ hai (cấm, #17) | `pnpm gen:registry && pnpm test` xanh, `check-additive` không đổi manifest nào |
| **S1** | Backend (T1–T13) | Theo luật "BE → sync contract → FE" | Test Docker xanh → `contract-sync` → **Gate 1** → changelog |
| ~~S2~~ | 5b editor | Task khác (`WEBSITE-002`) | — |

Registry gate (duyệt `registry-diff`) **không cần** nếu S0 không đổi manifest/`registry.lock.json` (giả định G8) —
S0 chỉ thêm artifact sinh ra + config.

## 6. Ràng buộc toàn cục
- **Tenant (#21, #99):** mọi bảng mới có `ShopId`; mọi FK tới entity thuộc shop là FK ghép `(X, ShopId) → (Id, ShopId)` (#92),
  gồm **cả** `Website.DefaultOgImageId`/`FaviconAssetId`, `Page.OgImageId` → `media_asset (Id, ShopId)` (#76).
  Không nhận `ShopId` từ body. Ownership validate trong câu query (`WHERE PageId = … ` + Global Filter), không load rồi so.
  `IgnoreQueryFilters()` **không** dùng ở Website (allowlist `IgnoreQueryFiltersAllowlistTests`).
- **Module (#1):** Website chỉ đọc `Shop`, `Media` (Application interface + `db`). Media **không** đọc Website (T11 dùng port).
- **Enum string (#19)**; mọi endpoint `.WithName()` camelCase, `[ProducesResponseType]` đủ status, ProblemDetails + `error_code`.
- **Generated không sửa tay** (`generated/**`); S0 sửa `gen-registry.ts` rồi chạy `pnpm gen:registry`. Hook chặn ghi `contracts/openapi/*.json`.
- **DB:** snake_case số ít (`website`, `theme`, `page`, `page_draft`); ràng buộc `ck_*`/`ix_*`/`ux_*`; FK tới `app_user`/`shop` ghim `HasConstraintName`;
  unique **không** tự lọc soft-delete — chọn rõ từng cái (§8 T3). Migration **mới** (không gộp lại).
- **Fail-fast khi khởi động:** thiếu/hỏng bất kỳ config/artifact nào (`locales`, `theme-tokens`, `sanitize-profiles`, `binding-sources`, `tree-rules`, schema) → app không lên.

## 7. Review focus — lỗi dễ lọt khi spec im lặng

| # | Tình huống | Hành vi đúng | Test ở |
|---|---|---|---|
| R1 | Shop A `PUT` draft với `imageId` của clone thuộc shop B | 422 `MEDIA_INVALID_IMAGE_REFERENCE`; không lộ id đó có tồn tại | T7 (HTTP) |
| R2 | Member shop A gọi `…/pages/{pageId của shop B}/draft` | 404 (Query Filter), không 403/500, không lộ tồn tại | T10 tenant isolation |
| R3 | Tree có nút `Review` ở `binding.source`, hoặc `Review` chỉ nằm sâu trong `list.items[].binding` | 422; walker phải đi **mọi** độ sâu, kể cả `group`/`list` | T4 |
| R4 | richText `<img src=x onerror=…>`, `javascript:`/`data:` ở `href`, `<a target=_blank>` thiếu `rel`, **entity-encoded** `&lt;script&gt;`, nested `<scr<script>ipt>` | `<script>`/`onerror`/`javascript:`/`data:` → 422 `RICHTEXT_UNSAFE`; `target=_blank` thiếu `rel` → lưu bản đã ép `rel=noopener noreferrer` (không lỗi); chuỗi đã encode **không** bị coi là script (không false-positive) | T3 (fixture chung với FE) |
| R5 | HTML Lexical thô (`class`, `style`, `<span style="white-space: pre-wrap">`, `<h1>` ngoài whitelist) — FE quên sanitize | **200**, lưu bản sạch (bỏ `class`/`style`/`span`, giữ chữ); `GET draft` trả bản sạch. Idempotent `sanitize(sanitize(x)) == sanitize(x)`. Phân loại "nguy hiểm" dựa trên **thứ bị cắt** (hook của sanitizer), không so chuỗi | T3 |
| R16 | User có membership `Customer` (hoặc Manager/Staff) ở shop A + token global gọi bất kỳ endpoint Website **hoặc Media** của shop A | **403** (`WEBSITE_OWNER_REQUIRED` / `MEDIA_OWNER_REQUIRED`), đọc lẫn ghi | T10, T10b (HTTP, Docker) |
| R6 | System page: PUT đổi `variant`, thêm/xoá/đảo node, đổi prop **không** `editableInSystemPage`, đổi id | 422 theo từng loại lỗi; đổi `content` của RichText thì qua | T5b |
| R7 | Hai tab: A đọc v5; B lưu → v6; A lưu v5. Và race thật: hai `PUT` cùng version song song | A nhận 409 `DRAFT_CONFLICT`; race → đúng **một** thành công, còn lại 409 (không 500) | T9 (Docker) |
| R8 | Body khổng lồ / cây sâu 10.000 tầng / 100.000 node / 1 `richText` 50 MB | 413/422 trước khi validate sâu; không stack overflow (walker **lặp**, không đệ quy không giới hạn) | T4 |
| R9 | `Slug` = `en`, `EN`, `vi`, `lien-he`, `san-pham`, `danh-muc`, `dat-lich`, `media`, `api`, `""` lần hai, ký tự Unicode/`../`/`%2f` | `en`/`vi`/route System (kể cả chưa seed)/`reservedShopSitePaths` → 422 `PAGE_SLUG_RESERVED`; `blog`/`about` **qua**; trùng → 409; sai khuôn → 422 | T8 |
| R10 | Xoá/đổi `Kind` của trang chủ & System page; tạo trang `Kind = System` qua API | `PAGE_NOT_DELETABLE`; API tạo trang **không** nhận `kind`/`systemType` | T8 |
| R11 | `CreateWebsite` gọi hai lần song song / shop `ExternalOnly` / `GET website` khi chưa tạo | một thành công, còn lại 409; 422; `404 WEBSITE_NOT_FOUND`. (5a không có API xoá website; `UNIQUE(shop_id)` không lọc xoá mềm — có chủ đích, khôi phục website là việc của admin) | T6, T10 (Docker) |
| R12 | `schemaVersion` = 999, thiếu, hoặc là chuỗi `"1"` | 422 `SCHEMA_VERSION_UNSUPPORTED`/`TREE_INVALID`, không 500 | T5 |
| R13 | Xoá trang → slug dùng lại; FK `PageDraft` khi page xoá mềm | Slug dùng lại được (unique lọc `NOT is_deleted`); không draft mồ côi hiện ra ở `references` | T3, T11 |
| R14 | `references` của ảnh Library chỉ bị dùng qua **clone** trong tree | Báo `PageDraft` kèm `pageId`; ảnh dùng ở **shop khác** không bị báo | T11 |
| R15 | 23505 từ **bất kỳ** unique nào (không chỉ slug) | 409 chung `CONFLICT_UNIQUE`, **không rò tên constraint/SQL** ra `detail` | T9 |

## 8. Sơ đồ file

```
config/
  locales.json                                  [S0, MỚI]
  theme-tokens.json                             [S0, MỚI]
  system-pages.json                             [S0, MỚI — G3]
packages/builder-components/
  meta/envelope-schema.ts                       [S0, MỚI — Zod vỏ Tree + Tokens, dựng từ config]
  scripts/gen-registry.ts                       [S0, SỬA — sinh 3 artifact mới]
  generated/tree-rules.json                     [S0, SINH — op-rules + prop-kind map (image/richText+profile/binding+sources) + acceptsChildren…]
  generated/tree-envelope.schema.json           [S0, SINH]
  generated/theme-tokens.schema.json            [S0, SINH]
  tests/fixtures/tree/*.json                    [S0, MỚI — bộ fixture dùng chung hai phía: valid-*, invalid-*, xss-richtext, system-*]
backend/src/
  Vsite.Domain/Website/{Entities,Enums}/        [T2] Website Theme Page PageDraft · PageKind SystemType WebsiteStatus
  Vsite.Application/Website/
    Interfaces/                                 [T1,T3,T4] IWebsiteConfigCatalogs, IRichTextSanitizer, ITreeValidator, IDocumentUpcaster
    Validation/                                 [T4,T5] PageTreeValidator, TreeWalker (lặp), SystemPageGuard, SchemaVersioning
    Commands/{CreateWebsite,CreatePage,UpdatePage,DeletePage,SavePageDraft,UpdateTheme}/
    Queries/{GetWebsite,ListPages,GetPageDraft,GetTheme}/
    Dtos/
  Vsite.Application/Media/Interfaces/IMediaReferenceSource.cs   [T11, MỚI — port]
  Vsite.Application/Media/Queries/GetReferences/                [T11, SỬA]  + MediaReferenceKind.PageDraft
  Vsite.Infrastructure/Website/                 [T1,T3,T11] catalogs, sanitizer, PageDraftReferenceSource
  Vsite.Infrastructure/Persistence/Configurations/Website/      [T3]
  Vsite.Infrastructure/Persistence/Migrations/…AddWebsiteModule [T3]
  Vsite.Api/Website/WebsiteEndpoints.cs         [T10]
  Vsite.Api/ExceptionHandling/ConcurrencyAndUniqueExceptionHandler.cs [T9, MỚI]
  Vsite.Api/{Program.cs, Vsite.Api.csproj}      [T1,T10] link config + AddOpenApi("website") + MapWebsiteEndpoints
backend/tests/{IntegrationTests/Website/, ArchitectureTests/, ComponentSchemaTests/}
backend/docs/modules/website.md                 [T13]
Docs/tasks/WEBSITE-001/{contract-diff.md, brief.md, changelog.md, review.md}
```

## 9. Session S0: Config + codegen (không đụng backend)

### S0-T1: Config dùng chung
`config/locales.json`, `config/theme-tokens.json` (nội dung §4), `config/system-pages.json`
(`{ "Contact": {"routes":["lien-he"],"phase":2,"seedOnCreate":true}, "NotFound": {"routes":[],"phase":2,"seedOnCreate":true}, "ProductListing": {"routes":["san-pham","danh-muc"],"phase":2,"seedOnCreate":false}, "ProductDetail": {"routes":["san-pham"],…}, "ProductSearch": {"routes":["tim-kiem"],…}, "Booking": {"routes":["dat-lich"],"phase":3,"seedOnCreate":false} }` — G3).
**Khai đủ mọi `SystemType` của `05` §4 ngay bây giờ**, kể cả loại chưa seed: slug shop tạo hôm nay không được chiếm route của tương lai. `routes` là **đoạn đầu** của đường dẫn (`danh-muc/{x}` → `danh-muc`). `routes[0]` là route chính (slug của trang khi seed).
- [ ] **Điều kiện trước:** người đã thêm `reservedShopSitePaths` vào `config/reserved-routes.json` (§4)
- [ ] Test trước: mở rộng [`packages/builder-components/tests/config-files.test.ts`](../../../packages/builder-components/tests/config-files.test.ts) — shape từng file, đủ mọi `SystemType`, mã locale không trùng route System hay `reservedShopSitePaths`, `colorPattern` biên dịch được
- [ ] FAIL → tạo file → PASS (`pnpm --filter @vsite/builder-components test`)
- [ ] Commit `feat(config): locales, theme-tokens, system-pages (#97, #98)`

### S0-T2: Zod vỏ + JSON Schema sinh ra
`meta/envelope-schema.ts` dựng Zod từ `theme-tokens.json` + `locales.json`; `gen-registry.ts` sinh `tree-envelope.schema.json`
(gốc `{schemaVersion:int, id, type:"Page", children[]}`, node `{id,type,variant?,props?,children?,i18n?}`, `additionalProperties:false`) và `theme-tokens.schema.json`.
- [ ] Test trước: `tests/envelope-schema.test.ts` chạy **cả Zod lẫn JSON Schema** trên `tests/fixtures/tree/*.json`, khẳng định hai bên đồng ý (khuôn `schema-equivalence.test.ts`, #60)
- [ ] FAIL → hiện thực → PASS
- [ ] Commit `feat(builder-components): vỏ Tree/Tokens Zod → JSON Schema (#97)`

### S0-T3: Artifact `tree-rules.json`
Mỗi `type` → `acceptsChildren`, `allowedChildTypes`, `allowedInPageKinds`, `maxPerPage`, `editableInSystemPageProps`,
`variants[]`, và **bản đồ prop**: `props.<tên>.kind` (+ `profile` cho richText, `sources` cho binding, `itemProps` cho list/group đệ quy). Nguồn: chính manifest (cùng chỗ sinh `op-rules.ts`).
- [ ] Test trước: `gen-registry.test.ts` — artifact khớp `op-rules.ts`; manifest giả có `richText.profile = x` → artifact có `profile: x`; test đột biến chạy trên bản sao tạm (`VSITE_REGISTRY_DIR`, **không** sửa `registry/` thật)
- [ ] FAIL → PASS · `check-additive` + `registry.lock.json` **không đổi**
- [ ] Commit `feat(builder-components): artifact tree-rules.json cho BE (#60)`

### S0-T4: Bộ fixture dùng chung (hai phía)
`tests/fixtures/tree/`: `valid-home.json`, `valid-system-contact.json`, `invalid-library-image-id.json`, `invalid-binding-review.json`
(+ biến thể lồng trong `list`), `invalid-unknown-prop.json`, `invalid-i18n-key.json`, `invalid-system-structure-*.json`, `xss-richtext.json`
(nhiều vector R4, mỗi vector kèm `expect: "reject"|"clean"|"pass"`), `valid-richtext-reformatted.json` (R5).
- [ ] Test FE: `sanitize-html.test.ts` chạy `xss-richtext.json` (đóng nợ BOOTSTRAP-002 "thiếu test `data:` URL")
- [ ] Commit `test(builder-components): fixture Tree/XSS dùng chung FE+BE`

## 10. Session S1: Backend

### T1: Nạp config + artifact vào BE (fail-fast)
`Vsite.Api.csproj`: `<None Link>` cho `locales`, `theme-tokens`, `system-pages`, `sanitize-profiles`, `binding-sources` + 3 artifact `generated/` (target `Ensure…` như [csproj:52-58](../../../backend/src/Vsite.Api/Vsite.Api.csproj#L52) — thiếu artifact thì build dừng, thông báo `pnpm gen:registry`). Catalog singleton đọc một lần, resolve ngay sau `Build` ([Program.cs:99-100](../../../backend/src/Vsite.Api/Program.cs#L99)). Thêm `JsonSchema.Net` vào `Vsite.Infrastructure`.
- [ ] Test trước: `…/Website/WebsiteConfigCatalogTests` — đọc đúng; thiếu file → ném rõ tên file; `locales` chứa `vi`; `binding-sources` không chứa `Review`
- [ ] FAIL → PASS · Commit `feat(website): nạp config + artifact codegen, fail-fast (#97)`

### T2: Domain — entity có hành vi, không setter public
`Website`(`TenantAuditableEntity`: `Status`, `DefaultLocale`, `Locales`, SEO mặc định, `DefaultOgImageId?`, `FaviconAssetId?`, `TrackingSnippets`) ·
`Theme`(PK=`WebsiteId`, `PresetKey?`, `Tokens`) · `Page`(`Kind`, `SystemType?`, `Slug`, `Title`, SEO, `OgImageId?`, `NoIndex`, `IsEnabled`, `SortOrder`) ·
`PageDraft`(PK=`PageId`, `Tree`, `IsDirty`). Invariant trong entity (ném `DomainException`): `Kind = System ⇒ SystemType ≠ null`,
`Composable ⇒ SystemType = null`; `DefaultLocale ∈ Locales`; `Slug = "" ⇒ Composable`. `Tree`/`Tokens` giữ là `string` JSON (jsonb) — validate ở Application, không ở entity.
- [ ] Test trước: `WebsiteEntityTests`, `PageEntityTests`
- [ ] Commit `feat(website): entity Website/Theme/Page/PageDraft (#92, #98)`

### T3: Persistence + migration `AddWebsiteModule`
- `website`: UNIQUE `(shop_id)` (1:1) + `UNIQUE (id, shop_id)` (đích FK ghép); `locales text[]`; `ck_website_default_locale` = `default_locale = ANY(locales)`; FK ghép `(default_og_image_id, shop_id)` và `(favicon_asset_id, shop_id)` → `media_asset (id, shop_id)`, `SET NULL (col)` (#92).
- `theme` / `page`: FK ghép `(website_id, shop_id)` → `website (id, shop_id)`. `page`: `UNIQUE (id, shop_id)`; **unique có lọc** `ux_page_slug` `(website_id, slug) WHERE NOT is_deleted`; `ux_page_system_type` `(website_id, system_type) WHERE system_type IS NOT NULL AND NOT is_deleted`; `ck_page_kind_system_type`; `slug` **nullable** + `ck_page_slug_not_found` = `(slug IS NULL) = (system_type = 'NotFound')` (§4); FK ghép `(og_image_id, shop_id)` → `media_asset`.
- `page_draft`: FK ghép `(page_id, shop_id)` → `page (id, shop_id)`; `Tree` jsonb; **`xmin` làm concurrency token** (`UseXminAsConcurrencyToken` / `IsRowVersion` map `xid`).
- Thêm `DbSet` ở `AppDbContext` **và** `IAppDbContext`; tên FK tới `shop` ghim `HasConstraintName`.
- [ ] Test trước (Docker): `WebsiteDbConstraintTests` — FK ghép chặn trỏ chéo shop (website/page/draft/og image); CHECK `default_locale`; unique slug lọc soft-delete (xoá rồi tạo lại OK); unique system type; không có 2 website/shop. `WebsiteTenantIsolationTests` (khuôn [MediaTenantIsolationTests](../../../backend/tests/IntegrationTests/Media/MediaTenantIsolationTests.cs#L15)): shop A không thấy dữ liệu shop B, tenant null → rỗng
- [ ] FAIL → entity config + `dotnet ef migrations add AddWebsiteModule …` → PASS
- [ ] Commit `feat(website): bảng website/theme/page/page_draft + FK ghép + xmin (#92)`

### T4: `PageTreeValidator` — validate TOÀN cây
Walker **lặp** (stack tường minh) có giới hạn cứng (`MaxBodyBytes` 1 MB, `MaxDepth` 20, `MaxNodes` 2000 — G6, để trong `appsettings`). Thứ tự: giới hạn → vỏ (JSON Schema) → từng node (§Flow 2 e–g) → gom `imageId`/lỗi. Trả `ValidationResult { errors[{path,code,message}], imageIds }`; không ném lỗi ở lần đầu thấy.
`IRichTextSanitizer` (Infrastructure): thư viện **`HtmlSanitizer` (Ganss.Xss, MIT)** cấu hình **từ `sanitize-profiles.json`** (tags, attrs, scheme `https/http/mailto/tel`, ép `rel=noopener noreferrer` — G4). Trả `{ clean, unsafe: bool }`: `unsafe` bật qua **sự kiện** `RemovingTag`/`RemovingAttribute`/`RemovingAtRule`/URL bị loại của thư viện khi thứ bị cắt thuộc danh sách nguy hiểm (§4) — **không** so chuỗi trước/sau. `unsafe` → lỗi `RICHTEXT_UNSAFE` vào `errors[]`; không `unsafe` → validator **thay** giá trị prop bằng `clean` trước khi lưu. Danh sách thẻ/thuộc tính/scheme nguy hiểm để ở `sanitize-profiles.json` (khoá mới `dangerous`), FE đọc cùng file để cảnh báo sớm (#97).
Hệ quả: `PUT draft` **có thể lưu khác** bản gửi lên → response thêm cờ `sanitized: true` để 5b biết tải lại tree nếu cần.
- [ ] Test trước: `PageTreeValidatorTests` chạy **đúng `tests/fixtures/tree/*.json` của S0** (một test theory duyệt cả thư mục: mỗi fixture có `expect`); thêm ca R3, R8, R12; `RichTextSanitizerTests` (R4, R5, idempotent)
- [ ] FAIL → hiện thực → PASS · Commit `feat(website): validate toàn cây — allowedInPageKinds, props, binding, richText (#60, #67)`

### T5: `schemaVersion` + khung upcaster (#94) · T5b: bảo vệ System page
T5: `IDocumentUpcaster` cho `Tree` và `Tokens` (hiện hành = 1, chưa có bước nâng; **khung** + test bằng upcaster giả v0→v1). Đọc: nâng mọi bản cũ lên hiện hành; ghi: lưu hiện hành; `> hiện hành` → 422.
T5b: `SystemPageGuard.Compare(stored, incoming, editableProps)` — cùng tập id, type, variant, thứ tự, cha-con; prop khác `editableInSystemPageProps` phải bằng nhau (so JSON canonical).
- [ ] Test trước: `SchemaVersioningTests`, `SystemPageGuardTests` (R6, R12) · Commit `feat(website): upcaster khung (#94) + khoá cấu trúc System page (#46)`

### T6: Tạo website trắng
`CreateWebsiteCommand(ShopId)` — **ngoại lệ có chủ đích của #99**. Handler: kiểm shop tồn tại + `Hosted` (đọc `db.Shops`), tạo đủ bộ trong **một** `SaveChanges`; id node `c_` + nanoid 5 ký tự sinh server-side; seed theo `system-pages.json` (`seedOnCreate`); `Theme` tokens mặc định **lấy từ `theme-tokens.json` → khoá `defaults`** (G2). `EnsureSystemPages` idempotent cho tương lai.
- [ ] Test trước: `CreateWebsiteHandlerTests` (R11, đủ 3 page + 3 draft, draft đều qua `PageTreeValidator`), integration race hai request (Docker)
- [ ] Commit `feat(website): tạo website trắng + System page seed (#46, #98)`

### T7: Nối `IMediaReferenceValidator` vào lưu draft + metadata trang
`SavePageDraftHandler` gọi validator với `treeImageIds` (từ T4) ; `UpdatePage`/`CreatePage` với `businessImageIds = [ogImageId]`. Validator đọc `ITenantContext` (đã đúng quy ước #99). **Test request-level** — đóng MEDIA-001 #1 và `08` §9 test 6/7.
- [ ] Test trước (Docker): `PageDraftMediaReferenceTests` — id Library trong tree → 422; id clone của shop khác → 422; id đã xoá mềm → 422; id Clone hợp lệ → 200 (R1)
- [ ] Commit `feat(website): nối IMediaReferenceValidator vào lưu draft/page (#71, #77)`

### T8: API metadata Page + luật Slug
Handlers `CreatePage/UpdatePage/DeletePage/ListPages/GetWebsite`. `SlugRules`: khuôn (§Flow 3), không trùng `locales.json` (so **không phân biệt hoa/thường**), không trùng slug trong `system-pages.json`, trùng trong website → 409 `PAGE_SLUG_TAKEN` (kiểm trước để không ra 500, như [CreateShopHandler.cs:23](../../../backend/src/Vsite.Application/Shop/Commands/CreateShop/CreateShopHandler.cs#L23), **và** bắt 23505 ở T9 cho race).
- [ ] Test trước: `SlugRulesTests` (R9), `DeletePageHandlerTests` (R10), `CreatePageHandlerTests`
- [ ] Commit `feat(website): API metadata Page + chặn Slug trùng locale/System (#98)`

### T9: Concurrency `PageDraft` + map lỗi chung
`SavePageDraftHandler`: nạp draft (`xmin` = token đang giữ) → so `request.Version`; lệch → `ConflictException("DRAFT_CONFLICT")` kèm `currentVersion`; `SaveChanges` ném `DbUpdateConcurrencyException` → cũng `DRAFT_CONFLICT`. `Version` trong DTO = `xmin` (uint32 → `long`) — opaque với FE (G5). **Handler toàn cục mới** `ConcurrencyAndUniqueExceptionHandler` (đăng ký cạnh [Program.cs:47-49](../../../backend/src/Vsite.Api/Program.cs#L47)): `DbUpdateConcurrencyException` → 409 `CONCURRENCY_CONFLICT`; `PostgresException 23505` → 409 `CONFLICT_UNIQUE` **không lộ tên constraint** (R15). Đóng REFACTOR-DB-001 #1, #2.
- [ ] Test trước: unit handler + integration (Docker): hai client, A lưu sau B với version cũ → 409 (R7); hai `PUT` song song cùng version → đúng một 200; 23505 bất kỳ → 409 không rò SQL
- [ ] Commit `feat(website): concurrency PageDraft bằng xmin + 409 chung (REFACTOR-DB-001 #1,#2)`

### T10: Endpoints + OpenAPI document `website`
`AddOpenApi("website", …3 transformer)` ([Program.cs:42-45](../../../backend/src/Vsite.Api/Program.cs#L42)); `api.MapWebsiteEndpoints()`; **mọi** route gắn `.RequireShopOwner("WEBSITE_OWNER_REQUIRED", …)` (#100), kể cả `GET`. Thêm một architecture/integration test: mọi endpoint của document `website` **và** `media` có `ShopOwnerRequirement` — quên là RED (#17). `operationId`: `createWebsite`, `getWebsite`, `listPages`, `createPage`, `updatePage`, `deletePage`, `getPageDraft`, `savePageDraft`, `getTheme`, `updateTheme`.
- [ ] Test trước: `WebsiteEndpointTests` (Docker, khuôn [LibraryEndpointTests](../../../backend/tests/IntegrationTests/Media/LibraryEndpointTests.cs#L109)): 401/403/404/422/409 từng endpoint; `ShopScopedRouteFilterTests` + `ApiRoutePrefixTests` tự bắt route thiếu marker/operationId; **tenant isolation HTTP** (R2)
- [ ] Commit `feat(website): endpoints /api/shops/{shopId}/website… + OpenAPI document website`

### T10b: Vá phân quyền Media — chỉ Owner (#100)
Mọi endpoint `RequireShopMembership()` trong [MediaEndpoints.cs](../../../backend/src/Vsite.Api/Media/MediaEndpoints.cs) (dòng 74, 104, 118, 129, 140, 153, 180, 190) → `.RequireShopOwner("MEDIA_OWNER_REQUIRED", …)` — `error_code` này **đã có** trong contract `media.v1`. Thêm `403` vào `[ProducesResponseType]` nơi còn thiếu. **Đổi hành vi** cho Manager/Staff đang dùng được → ghi changelog "lệch có chủ đích".
- [ ] Test trước (Docker): R16 cho từng endpoint Media — user `Customer` + token global → 403; Manager → 403; Owner → như cũ. Test hiện có dùng member không phải Owner phải sửa lại fixture (không phải nới quyền)
- [ ] Commit `fix(media): mọi endpoint media chỉ Owner — chặn Customer có token global (#100)`

### T11: Endpoint `references` quét thêm tree
`IMediaReferenceSource` (Application/Media/Interfaces) — `Task<IReadOnlyList<MediaReferenceDto>> FindAsync(Guid assetId, ct)`; `GetReferencesHandler` nhận `IEnumerable<IMediaReferenceSource>`. `PageDraftReferenceSource` (Infrastructure/Website): clone `SourceAssetId = assetId` (cùng shop) ∩ `imageId` trong draft của shop (walker T4, **không** SQL jsonb thủ công). `MediaReferenceKind.PageDraft`; `MediaReferenceDto` thêm `pageId?`, `pageTitle?` (**đổi contract `media.v1`**, additive).
- [ ] Test trước: `PageDraftReferenceSourceTests` (R13, R14), cập nhật `GetReferencesHandlerTests`; `ModuleBoundaryTests` vẫn xanh (Media không tham chiếu Website)
- [ ] Commit `feat(media): references quét thêm PageDraft qua IMediaReferenceSource (MEDIA-001 #2)`

### T12: Kết thúc code S1
- [ ] `dotnet test backend/tests/ArchitectureTests` + `dotnet test backend/vsite.sln` **trên Docker thật** — dán số pass/fail vào changelog
- [ ] `pnpm contract:export website media` → skill `contract-sync` → `contract-diff.md` → **Gate 1 (#89)**: chỉ-thêm + không câu hỏi → promote; có `REMOVED`/câu hỏi → dừng chờ bạn. Lưu ý `MediaReferenceDto` đổi shape → báo trước, đừng để classifier gắn BREAKING rồi bất ngờ
- [ ] `brief.md` (viết **sau** promote)

### T13: Đồng bộ tài liệu (không có bước này = CHƯA XONG)
- [ ] `Docs/tasks/WEBSITE-001/changelog.md` (lệch có chủ đích vs chưa làm xong) · đọc `Docs/DOCKER-TEST-DEBT.md` trước khi báo xong
- [ ] `DECISIONS.md`: #99/#100 đã cấp số — chuyển "Định nghĩa tại" sang `backend/docs/modules/website.md` + trạng thái enforce; cập nhật dòng #92/#94/#97/#98/#71/#77/#42/#67 trạng thái; ghi chú `#21` tham chiếu #99
- [ ] `00-INDEX` §4: thêm nợ "`GET /api/shops/{shopId}` còn mở cho `Customer`" và "brief 5b: Theme không có concurrency (G7), `PUT draft` có thể trả `sanitized: true`"
- [ ] Banner `STATUS` `05` (Website/Page/PageDraft đã code, lệch…) và `08` (validator đã nối, `references` quét tree) → `pnpm gen:doc-index` → `pnpm check:docs`
- [ ] `backend/docs/modules/website.md` · `00-INDEX.md` §3 (5a ✅) và §4 (xoá các dòng REFACTOR-DB-001 #1/#2, REFACTOR-BE-001 #3 phần Website, MEDIA-001 #1/#2 phần tree; thêm nợ dọn handler Media sang #99) · `step.md` 5a

## 11. Đóng task (trước Gate 2)
- [ ] `change-reviewer` với **model mạnh nhất** (chạm tenant/bảo mật/contract — theo `CLAUDE.md`) · `review.md` kèm "Ma sát quy trình"
- [ ] Máy có Docker: chạy `dotnet test backend/vsite.sln` lần cuối; mục Docker debt phải rỗng

## 12. Giả định tôi đã tự đặt (chưa hỏi, cần duyệt)

| # | Giả định | Vì sao | Nếu sai thì |
|---|---|---|---|
| G1 | ~~Đề xuất 4–8~~ → **đã chốt** 2026-10-15 (data-needs "Câu hỏi đợt 2"); câu 5 đổi thành "chỉ từ chối thứ nguy hiểm" | — | — |
| G2 | Thêm khoá **`defaults`** (tokens mặc định) vào `theme-tokens.json` | Tokens mặc định phải có nguồn duy nhất (#17); bạn chỉ chốt 7 khoá | Đặt `defaults` ở file riêng `theme-default.json` |
| G3 | File mới **`config/system-pages.json`** (`routes[]`/`seedOnCreate`/`phase` cho **mọi** `SystemType`) | `05` §4 đòi Slug không trùng route System page nhưng chưa có nguồn; FE 8b cũng cần | Viết hằng C# + hằng TS (hai danh sách — trái #17/#24) |
| G4 | Thư viện sanitize BE: **`HtmlSanitizer` (Ganss.Xss, MIT)** — dependency mới, kéo theo AngleSharp | Cấu hình được hoàn toàn từ whitelist JSON; chưa có gì trong `Directory.Packages.props`. Sau vụ ImageSharp, tôi **không tự thêm** thư viện bảo mật mà không báo | Tự viết bộ lọc trên AngleSharp/`System.Xml` — tốn công, dễ lỗ hổng hơn |
| G5 | `version` = `xmin` đổi sang `long`, FE coi là chuỗi/số opaque | Đúng `05` §5 "xmin + version đi trong contract" | Cột `Version int` tự tăng (thêm 1 cột, bỏ phụ thuộc xmin) |
| G6 | Giới hạn: body 1 MB, sâu 20, 2000 node, `errors[]` ≤ 50 | Spec im lặng; `05` §5 nói tree "vài trăm KB" | Chỉnh trong `appsettings`, không đổi code |
| G7 | `Theme` **không** có concurrency token (ghi cuối thắng) | Một panel, ghi hiếm; spec chỉ yêu cầu `PageDraft` | Thêm `xmin` cho `theme` (thêm 1 migration nhỏ) |
| G8 | S0 **không** cần Registry gate | Không đổi manifest, không đổi `registry.lock.json` — chỉ thêm artifact sinh ra + config | Nếu bạn muốn duyệt cả artifact mới như Registry → thêm cổng trước S1 |
| G9 | ~~Owner cho ghi khó đảo, còn lại member~~ → **thay bằng #100**: mọi endpoint Website + Media chỉ Owner | — | — |
| G10 | `Page` xoá mềm, unique slug **lọc** `NOT is_deleted` (khác `shop.slug` giữ toàn cục) | Shop xoá rồi tạo lại cùng slug là thường; slug trang không phải URL toàn hệ | Giữ toàn cục + 409 nếu trùng trang đã xoá |
| G11 | `IsDirty` = `true` ngay từ lúc seed và sau mỗi `PUT` (chưa có publish nên "khác bản publish" luôn đúng) | 8b mới đặt lại `false` | Seed `false` |
| G12 | `Page.SortOrder` do server cấp (cuối danh sách) khi tạo; không có endpoint đổi thứ tự trong 5a | Spec `05` §4 chỉ nói cột tồn tại | Thêm `PATCH sortOrder` |

## 13. Verification tổng
- `pnpm gen:registry && pnpm build && pnpm test` xanh; `git diff --stat packages/builder-components/registry registry.lock.json` = rỗng (G8).
- `dotnet test backend/tests/ArchitectureTests` xanh (Media không đọc Website; Domain không EF).
- `dotnet test backend/vsite.sln` xanh **trên Docker thật**; có test cho cả 4 câu "Xong khi": 409 hai tab (R7), id Library (R1 + T7), id shop khác (R1), `Review` (R3), `<script>` (R4).
- `pnpm contract:export website media` → `contract-diff.md` đã qua Gate 1; `check-no-drift` xanh.
- `pnpm check:docs` xanh; `Docs/tasks/WEBSITE-001/changelog.md` tồn tại; `Docs/DOCKER-TEST-DEBT.md` rỗng.
