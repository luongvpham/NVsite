# WEBSITE-004 — changelog

## S0-T1

`requiresProps` vào `tree-rules.json` (Bước 8b, S0, codegen).

**Thay đổi**
- `packages/builder-components/scripts/gen-registry.ts` — `generateTreeRules` nay xuất thêm
  `requiresProps: { [variantKey]: string[] }` (mảng đã sort) bên cạnh `variants` đang có. Additive,
  `schemaVersion` của `tree-rules.json` giữ nguyên 1. Nguồn là `variant.requiresProps` vốn đã có trong
  manifest (được invariant #3/§6.1 bảo đảm là tập con của `usesProps`), nên không sửa manifest nào.
- `packages/builder-components/tests/gen-registry.test.ts` — thêm field `requiresProps` vào type
  `TypeRule` và một test: `Hero.requiresProps.Hero01 = ['image','title']`, `Hero02 = ['title']`; mọi key
  của `requiresProps` khớp `variants`; mỗi mảng đã sort.

**Kiểm chứng**
- `pnpm --filter @vsite/builder-components test -- --run gen-registry` → 21/21 passed (TDD: FAIL trước
  khi hiện thực, PASS sau).
- `pnpm gen:registry` → OK, check-additive xanh. `generated/tree-rules.json` có `requiresProps` cho cả
  5 manifest. `generated/` gitignored nên không vào commit; `registry.lock.json` không đổi (không sửa
  manifest).

**Không làm**: không sửa manifest, không đụng `registry.lock.json`, không thêm artifact mới (đó là S0-T2).

## S0-T2

Vỏ `SiteSnapshot` v1 bằng Zod + JSON Schema (Bước 8b, S0, codegen, #94, #97, #60).

**Thay đổi**
- `packages/builder-components/meta/envelope-schema.ts` — thêm `buildSiteSnapshotZod(locales, themeConfig)`
  và `buildSiteSnapshotJsonSchema(locales, themeConfig)` đúng shape plan §4. `tree` của mỗi trang tái dùng
  vỏ Tree sẵn có (JSON Schema nâng `node` lên `$defs` gốc, `treeEnvelope`/`themeTokens` là `$defs` riêng,
  tham chiếu bằng `$ref`); `theme.tokens` tái dùng vỏ Tokens. `additionalProperties:false` ở mọi cấp vỏ;
  `tracking` là map mở (như `props`), `navigation.items` chỉ kiểm là mảng (NavigationConfig thật là Bước 7),
  `media[*]` = `{ storageKey, preset|null }`. Thêm instance `siteSnapshotSchema` + type `SiteSnapshot`.
- `packages/builder-components/scripts/gen-registry.ts` — ghi thêm artifact
  `generated/site-snapshot.schema.json` (cạnh `tree-envelope`/`theme-tokens`); log "12 artifact".
- `packages/builder-components/src/index.ts` — export công khai `siteSnapshotSchema` (value) + type
  `SiteSnapshot` (dòng 13–14) để Bước 8c (SSR `apps/web` parse `GET /api/site`) import được từ
  `@vsite/builder-components`. (Bổ sung sau review Critical — commit gốc thiếu bước này.)
- `packages/builder-components/tests/site-snapshot-schema.test.ts` (mới) — 31 ca khẳng định Zod ↔ JSON
  Schema đồng ý: ví dụ §4 parse được; thiếu `pages[].id` → lỗi; `slug:null` hợp lệ; key lạ ở gốc/các cấp
  → lỗi; `navigation.items` rỗng hợp lệ; `theme.tokens` sai → lỗi; `defaultLocale`/`locales` ngoài config
  → lỗi; dựng từ locales khác thì schema đổi theo (một nguồn, #17).
- `packages/builder-components/tests/gen-registry.test.ts` — thêm `site-snapshot.schema.json` vào danh sách
  `GENERATED_FILES` (kiểm determinism giữa hai lần chạy).

**Kiểm chứng**
- `pnpm --filter @vsite/builder-components test` → 535/535 passed.
- `pnpm gen:registry` → OK, 12 artifact, check-additive xanh, `registry.lock.json` không đổi.
- `pnpm --filter @vsite/builder-components typecheck` sạch · `pnpm build` → 4/4 tasks OK. Chưa có app nào
  import `SiteSnapshot`/`siteSnapshotSchema` (việc tiêu thụ là Bước 8c SSR `apps/web`); export chỉ mở sẵn
  bề mặt API gói — build xanh xác nhận export biên dịch được, KHÔNG phải xác nhận `@vsite/web` đang dùng.
- `generated/` gitignored nên `site-snapshot.schema.json` không vào commit — dựng lại bằng `pnpm gen:registry`.

**Giả định**: `defaultLocale`/`locales` ràng buộc bằng enum từ `config/locales.json` (một nguồn, #17), đồng
bộ với khoá `i18n` của vỏ Tree; `kind`/`systemType` chỉ kiểm là chuỗi (enum của chúng thuộc tầng BE, không
đưa vào vỏ cấu trúc).

## T1

Entity `SitePublication` + `Website.CurrentPublicationId` + migration (Bước 8b, S1, #41, #92).
Không chạm contract (chưa có endpoint/DTO công khai — đó là T4–T6) → không chạy contract-sync ở bước này.

**Thay đổi**
- `Vsite.Domain/Website/Entities/SitePublication.cs` (mới) — entity bất biến kế thừa `TenantEntity` (có
  `ShopId` + Global Query Filter tự động, KHÔNG audit/soft-delete). `Create(shopId, websiteId, version,
  snapshotJson, note, publishedByUserId, publishedAt, id?)` với guard `version > 0`, snapshot không rỗng,
  `note ≤ 200`. Thuộc tính get-only (`private set`): `WebsiteId`, `Version`, `Snapshot` (jsonb), `Note`,
  `PublishedAt`, `PublishedByUserId`.
- `Vsite.Domain/Website/Entities/Website.cs` (sửa) — thêm `CurrentPublicationId` (nullable, `private set`);
  `Publish(p)` (trỏ current, `Draft→Published`, `Suspended` giữ nguyên G6); `PointTo(p)` (chỉ đổi con trỏ,
  không đổi `Status`, G7); `Suspend()`/`Reinstate()` (chưa có endpoint — admin/test sau dùng). `Publish`/
  `PointTo` ném `DomainException` `SITE_PUBLICATION_WEBSITE_MISMATCH` nếu bản không thuộc website này.
- `Vsite.Domain/Website/Entities/PageDraft.cs` (sửa) — `MarkPublished()` (IsDirty=false) / `MarkDirty()`
  (IsDirty=true), không đụng `Tree`.
- `Vsite.Application/Common/Interfaces/IAppDbContext.cs` + `Vsite.Infrastructure/Persistence/AppDbContext.cs`
  — thêm `DbSet<SitePublication> SitePublications`.
- `Vsite.Infrastructure/Persistence/Configurations/Website/SitePublicationConfiguration.cs` (mới) — bảng
  `site_publication`: PK `id`; `ux_site_publication_website_version (website_id, version)`; alternate key
  `ak_site_publication_id_website_id (id, website_id)`; CHECK `ck_site_publication_version_positive`
  (`version > 0`); FK ghép `(website_id, shop_id) → website (id, shop_id)` `Restrict`; FK
  `published_by_user_id → app_user` `Restrict` (tên ghim `fk_site_publication_app_user_published_by_user_id`).
- `WebsiteConfiguration.cs` (sửa) — cột `current_publication_id` nullable + FK ghép
  `(current_publication_id, id) → site_publication (id, website_id)` `Restrict` (`fk_website_current_publication`).
  Vì `current_publication_id` nullable, MATCH SIMPLE bỏ qua khi null (chưa publish); có giá trị thì bản phải
  thuộc đúng website (chặn trỏ chéo R4), và xoá bản đang là current bị DB từ chối.
- `Persistence/Migrations/20261113035855_AddSitePublication.cs` (mới) — `dotnet ef migrations add`, không
  sửa tay (FK vòng website↔site_publication EF tự xếp đúng thứ tự; không có SET NULL theo cột nên không cần
  SQL thô như `AddWebsiteModule`). `has-pending-model-changes` → "No changes".

**Lệch có chủ đích so với `05`**: `05` chưa có `SitePublication`/`CurrentPublicationId` (banner `05` đã ghi
"8b thêm additive sau"). Mã lỗi domain mới: `SITE_PUBLICATION_VERSION_INVALID`,
`SITE_PUBLICATION_SNAPSHOT_REQUIRED`, `SITE_PUBLICATION_NOTE_TOO_LONG`, `SITE_PUBLICATION_WEBSITE_MISMATCH`.

**Kiểm chứng (Docker có sẵn, Testcontainers chạy thật)**
- `dotnet test backend/tests/IntegrationTests` lọc `WebsiteEntityTests|SitePublicationDbConstraintTests|WebsiteTenantIsolationTests`
  → 23/23 passed (TDD: viết test trước). `SitePublicationDbConstraintTests` (mới) phủ: trùng version → 23505;
  shop_id lệch → 23503 `fk_site_publication_website`; version ≤ 0 → CHECK; con trỏ trỏ bản website khác → 23503
  `fk_website_current_publication` (R4); bản đang current không xoá được / bản không current xoá được. Entity
  tests (thêm): `Create` guard, Publish/PointTo/Suspend/Reinstate, website-mismatch, MarkPublished/MarkDirty.
  Isolation (thêm): shop A không thấy `SitePublication` của B; tenant null → rỗng.
- `dotnet test backend/tests/ArchitectureTests` → 12/12 passed.
- Sửa 4 file test fake decorator IAppDbContext (Media/*) để implement `SitePublications` (forward tới inner).

**Chưa làm (các task sau trong WEBSITE-004)**: SnapshotBuilder (T2), RequiredPropsChecker (T3), lệnh/endpoint
publish (T4), list/rollback (T5), `GET /api/site` + cache (T6), references (T8), docs module (T9).

## T2

`SnapshotBuilder` + `DocumentKind.Snapshot` (Bước 8b, S1, #94, #97). Thuần, không chạm DB. Không có endpoint/DTO
công khai mới → **không chạm contract**, không chạy contract-sync ở bước này.

**Thay đổi**
- `Vsite.Application/Website/Publishing/SnapshotBuilder.cs` (mới) — `SnapshotPage(Page, string CleanTree)`,
  `SnapshotMedia(Guid Id, string StorageKey, string? Preset)`, và `static string Build(website, theme, version,
  pages, media)` dựng JSON vỏ `SiteSnapshot` v1 đúng shape plan §4. Pages xếp `SortOrder` rồi `Title`
  (`StringComparer.Ordinal`); `pages[].id`/`ogImageId`/media key = GUID dạng `D` chữ thường (`Guid.ToString()`);
  `tree` nhúng `CleanTree` nguyên văn (đã upcast/sạch do validator); `theme.tokens` + `tracking` nhúng nguyên văn
  JSON đã lưu; `navigation.items = []` (NavigationConfig thật là Bước 7).
- `Vsite.Application/Website/Versioning/DocumentVersioning.cs` (sửa) — `DocumentKind.Snapshot`,
  `DocumentSchemaVersions.Snapshot = 1`, tham số ctor `snapshotCurrent`, `Current(kind)` đổi sang `switch` (ném
  `ArgumentOutOfRangeException` cho kind lạ). `StoredDocuments.ReadCurrent` dùng chung khung — tự đọc được Snapshot,
  không cần sửa (chưa có upcaster nào).
- `Vsite.Application/Website/Interfaces/IWebsiteConfigCatalogs.cs` + `Vsite.Infrastructure/Website/WebsiteSchemaProvider.cs`
  (sửa) — thêm `ValidateSiteSnapshot(json)`. Ctor của provider thêm tham số **tuỳ chọn** `siteSnapshotPath = null`
  (nạp eager nếu có; gọi `ValidateSiteSnapshot` khi provider dựng không có schema → `InvalidOperationException`).
  Tuỳ chọn để không vỡ 3 call-site test cấu hình nhẹ (TreeFixtures, PageTreeHardeningTests, WebsiteConfigCatalogTests).
- `Vsite.Api/Program.cs` (sửa) — truyền path `site-snapshot.schema.json` (eager resolve lúc startup, fail-fast).
- `Vsite.Api/Vsite.Api.csproj` (sửa) — link `site-snapshot.schema.json` ra `generated/` + thêm vào
  `EnsureWebsiteArtifacts` (thiếu artifact → build dừng với thông báo rõ).
- `backend/tests/.../WebsiteHandlerKit.cs` (sửa) — `S.Schemas` nay dựng kèm snapshot path (dùng cho T2 và sẵn cho
  T4/T6).

**Lệch có chủ đích**: không có (vỏ snapshot là additive, `05` §7 shape đã thay ở S0-T2; `pages[].id` đã ghi
phát hiện 1 ở plan). `ValidateSiteSnapshot` chỉ dùng trong test/khởi động, KHÔNG nằm trên đường phục vụ `GET /api/site`
(FE parse bằng Zod — plan §4).

**Kiểm chứng**
- `dotnet test backend/tests/IntegrationTests` lọc `SnapshotBuilderTests|SchemaVersioningTests` → 31/31 passed
  (TDD: test trước). `SnapshotBuilderTests` thuần, **không cần Docker** (dựng entity trong bộ nhớ qua seeder thật,
  validate bằng `site-snapshot.schema.json` thật). Phủ: hợp lệ theo schema (cả khi có media); shape cấp gốc; tokens
  nhúng nguyên văn; pages có id + marker home/NotFound; thứ tự SortOrder→Title; bảng media key GUID thường + preset
  null cho Library.
- `dotnet build backend/src/Vsite.Api` → 0 error. `dotnet test backend/tests/ArchitectureTests` → 12/12 passed.

**Chưa làm (các task sau)**: RequiredPropsChecker (T3), lệnh/endpoint publish (T4), list/rollback (T5),
`GET /api/site` + cache (T6), references (T8), docs module (T9).

## T3

`RequiredPropsChecker` — kiểm prop bắt buộc theo variant khi publish (Bước 8b, S1). Thuần, không chạm DB.
Không có endpoint/DTO công khai mới → **không chạm contract**, không chạy contract-sync ở bước này.

**Thay đổi**
- `Vsite.Application/Website/Validation/TreeValidationResult.cs` (sửa) — thêm hằng
  `TreeErrorCodes.RequiredPropMissing = "REQUIRED_PROP_MISSING"`.
- `Vsite.Application/Website/Interfaces/IWebsiteConfigCatalogs.cs` (sửa) — `TreeTypeRule` thêm
  `IReadOnlyDictionary<string, IReadOnlyList<string>> RequiresProps` (khoá = variant, giá trị = tên prop bắt buộc).
  Nguồn: field `requiresProps` sinh ở S0-T1 trong `generated/tree-rules.json`.
- `Vsite.Infrastructure/Website/ConfigCatalogs.cs` (sửa) — `TreeRulesCatalog` đọc `requiresProps`; `RawType` thêm field;
  thiếu field trong file → `InvalidOperationException` nêu tên file (fail-fast như các catalog khác).
- `Vsite.Application/Website/Publishing/RequiredPropsChecker.cs` (mới) — `Check(cleanTreeJson)` → danh sách
  `TreeValidationError` mã `REQUIRED_PROP_MISSING`, `path = /…/props/{tên}`. Walker lặp (stack tường minh), chạy trên
  `CleanTree` do validator trả (không lặp lại kiểm vỏ/cú pháp). Định nghĩa "thiếu" theo plan §4: key vắng/`null`;
  `text` rỗng sau trim; `richText` không còn ký tự sau khi bỏ thẻ; `image` không có `imageId` chuỗi khác rỗng;
  `list` 0 phần tử; `binding` không có `source`; kind khác chỉ tính vắng/`null`. **Không** gọi trong `PUT draft`.

**Lệch có chủ đích**: không có (field `requiresProps` là additive từ S0-T1; checker thuần nội bộ).

**Kiểm chứng** (Docker CÓ trên máy — chạy thật, không nợ test)
- `dotnet test backend/tests/IntegrationTests --filter RequiredPropsCheckerTests|WebsiteConfigCatalogTests` → 33/33
  passed (TDD: test trước). `RequiredPropsCheckerTests` thuần, **không cần Docker** (đọc artifact/config thật):
  Hero01 thiếu image → 1 lỗi đúng path; Hero02 không cần image; text whitespace / richText `<p></p>` / list rỗng /
  binding thiếu source → thiếu; props vắng hẳn → báo từng prop; node lồng báo đúng path; tree rỗng → không lỗi;
  3 tree seed của `WebsiteSeeder` → không lỗi (R8). `WebsiteConfigCatalogTests`: `requiresProps` đọc đúng + thiếu
  field → lỗi nêu tên file.
- `dotnet test backend/tests/IntegrationTests --filter SavePageDraftHandlerTests` → 30/30 passed (Postgres thật):
  thêm case lưu draft thiếu prop bắt buộc vẫn 200 (checker không chạy ở PUT draft).
- `dotnet test backend/tests/ArchitectureTests` → 12/12 passed. `dotnet build backend/vsite.sln` → 0 error.

**Chưa làm (các task sau)**: lệnh/endpoint publish (T4), list/rollback (T5), `GET /api/site` + cache (T6),
references (T8), docs module (T9).

## Sửa sau review giữa chừng (2026-11-17, trước T4)

- **4 test cũ đỏ do T3:** `TreeRulesCatalog` bắt buộc `requiresProps` (đúng plan), nhưng luật viết tay trong
  `PageTreeHardeningTests` (2), `PageTreeValidatorTests` (1) và fixture dùng chung FE+BE
  `invalid-binding-review-in-list.json` (1) không khai field này. Burn chỉ chạy test đã lọc nên không thấy.
  Sửa ở `backend/tests/IntegrationTests/Website/TreeFixtures.cs` (`MergeRulesOverride` bổ sung `requiresProps: {}`
  khi override không khai) — không sửa fixture dùng chung. Code sản phẩm không đổi.
- **Plan T4 thêm R14:** khoá `snapshot.media` (GUID chữ thường) phải khớp `imageId` trong tree; `PUT draft` chấp nhận
  GUID chữ hoa → T4 chuẩn hoá `imageId` trong tree về chữ thường lúc publish.
- **Plan §10:** mỗi task chạy `dotnet test backend/vsite.sln` đầy đủ trước khi commit.

## T4

Lệnh publish + endpoint `POST /api/shops/{shopId}/website/publications` (`publishWebsite`) (Bước 8b, S1, #41).
**Chạm contract** (route mới + `PublicationDto` + `WebsiteDto.currentPublicationId`) — contract-sync gộp ở cuối S1 (T9,
plan §5), không chạy ở bước này.

**Thay đổi**
- `Vsite.Application/Website/Commands/PublishWebsite/` (mới) — `PublishWebsiteCommand(string? Note)`, validator
  (`Note` ≤ `SitePublication.MaxNoteLength` = 200), `PublishWebsiteHandler` theo plan T4 bước 1–7: nạp website (tracking),
  theme, trang **đang bật** + draft (tracking), mọi predicate có `ShopId` tường minh; mỗi trang `PageTreeValidator` →
  `RequiredPropsChecker` → `IMediaReferenceValidator` (ảnh tree + OG trang); OG mặc định + favicon kiểm một lần
  (`pageId = null`); có lỗi → `422 PUBLISH_BLOCKED`, không ghi gì; chuẩn hoá `imageId` trong `CleanTree` về GUID chữ
  thường (R14, chỉ thay chuỗi validator đã nhận là id ảnh, cây đã chuẩn thì giữ nguyên văn); nạp `storageKey`/`preset`;
  `Version = max + 1`; `SnapshotBuilder.Build` → `SitePublication.Create` (người publish = `IAuditActor.UserId`) →
  `website.Publish` → `MarkPublished` chỉ cho draft bẩn; một `SaveChanges`; 23505 / `xmin` lệch → `409 PUBLISH_CONFLICT`;
  log `snapshotBytes` (R13); sau commit gọi `ISitePointerCache.InvalidateAsync`.
- `Vsite.Application/Website/Publishing/PublishErrors.cs` (mới) — `PublishPageError(pageId?, pageTitle?, path, code,
  message)`, `PublishBlockedParams(errors, truncated)`, `PublishBlockedException` (trần 100 mục).
- `Vsite.Application/Website/Interfaces/ISitePointerCache.cs` (mới) + `Vsite.Infrastructure/Website/NoOpSitePointerCache.cs`
  (mới, **tạm** — T6 thay bằng bản Redis thật). Đăng ký ở section mới `AddWebsiteModule` trong `DependencyInjection.cs`.
- `Vsite.Application/Website/Dtos/WebsiteDtos.cs` + `Mappings.cs` — `PublicationDto(id, version, publishedAt,
  publishedByUserId?, note?, isCurrent)`; `WebsiteDto` thêm `CurrentPublicationId` (cuối record, additive).
- `Vsite.Api/Website/WebsiteEndpoints.cs` — route `POST /publications`, body tuỳ chọn `PublishWebsiteRequest(note?)`,
  `RequireGlobalScope` + `RequireOwner` (#100), `201` + `Location`, `401 403 404 409 422`.
- `Vsite.Api/Program.cs` — `RequiredPropsChecker` singleton (cạnh `PageTreeValidator`).
- Test: `PublishWebsiteHandlerTests.cs` (mới, 17 ca — gồm 3 ca sau review), `PublicationEndpointTests.cs` (mới, 8 ca), `WebsiteHandlerKit.cs`
  (thêm `Publish`, `RecordingSitePointerCache`, `InsertPublicationInterceptor` mô phỏng tab khác publish cùng version,
  `SoftDeleteAfterCheckMediaValidator` mô phỏng ảnh bị xoá giữa lúc kiểm và lúc nạp).

**Lệch có chủ đích / giả định tự đặt** (người rà ở pha 6)
- `Location` trỏ `/api/shops/{shopId}/website/publications/{id}` dù chưa có endpoint GET một bản (plan không có) — FE dùng body.
- `note` được trim; chỉ khoảng trắng → `null`.
- Xoá con trỏ cache thất bại **sau** khi đã commit chỉ log warning, không trả lỗi (tránh người dùng bấm lại sinh bản thừa;
  con trỏ cũ hết hạn theo TTL 5' — T6 phải giữ TTL này).
- Mọi 23505 / `DbUpdateConcurrencyException` lúc lưu publish → `PUBLISH_CONFLICT` (Application không đọc được tên
  constraint; lần ghi này không chạm unique nào khác).
- Trang có tree sai cấu trúc thì không kiểm tiếp `requiresProps`/ảnh của trang đó (không có `CleanTree`).
- Draft đang **sạch** lúc publish đọc rồi bị lưu trước lúc ghi: không có UPDATE nên không kiểm `xmin`; bản publish mang nội
  dung lúc đọc, draft giữ `IsDirty = true` — nhất quán (draft khác bản đang chạy), không mất dữ liệu. Chủ đích.
- Thêm ngoài checklist: test publish đồng thời trùng version → 409; test HTTP shape `params.errors[]` của `PUBLISH_BLOCKED`;
  body rỗng → 201; chưa có website → 404.

**Kiểm chứng** (Docker CÓ — chạy thật, không nợ test)
- TDD: test viết trước, đỏ vì thiếu kiểu (biên dịch) → hiện thực → `PublishWebsiteHandlerTests|PublicationEndpointTests|
  ShopScopedRouteFilterTests|ApiRoutePrefixTests` 30/30 passed. Kiểm đột biến: bỏ chuẩn hoá `imageId` → test R14 đỏ.
- `dotnet test backend/vsite.sln` **đầy đủ** (sau sửa review) → ComponentSchemaTests 12/12, ArchitectureTests 12/12,
  IntegrationTests 839 passed / 1 skipped (skip có sẵn: `ShopEndpointTests` chờ module Listing).

**Sửa sau `change-reviewer`** (0 Critical, 2 Important — mỗi sửa có test đỏ trước)
- I1: `params.truncated` bỏ qua cờ `ErrorsTruncated` của validator (tự dừng 50 lỗi/trang) → nay OR vào; lỗi cấp website
  (favicon/OG mặc định) đưa lên **đầu** danh sách để trần 100 không cắt mất. Test `Truncated_is_true_when_a_page_validator_stopped_early_I1`,
  `Website_level_image_error_has_null_page_and_comes_first_I1`.
- I2: ảnh bị xoá mềm giữa lúc kiểm (`EnsureValidAsync`) và lúc nạp bảng `media` làm snapshot bất biến có `imageId` không có
  khoá trong `media` → nay số dòng nạp lệch số id thì `409 PUBLISH_CONFLICT`, không ghi. Test
  `Image_soft_deleted_between_check_and_load_is_409_and_nothing_is_written_I2`.

**Theo dõi (Minor từ review, chưa làm)**
- Test R14 chưa có ca `imageId` chữ hoa nằm trong list (`Gallery.items[]`) — walker đã phủ, thiếu test khoá hành vi.
- `Tenant_of_shop_B_cannot_publish_website_of_shop_A` yếu (B không có website nên luôn 404); nên thêm ca B có website riêng.
- OpenAPI: 422 không mô tả `params.errors[]`/`truncated` (tiền lệ `TREE_INVALID`), header `Location` không khai; `pageId`/
  `pageTitle` nullable — nêu ở `contract-diff.md`/`brief.md` (T9). `Location` trỏ tài nguyên không GET được → câu hỏi Gate 1.
- Thiếu theme/draft (dữ liệu lệch invariant seed) → 500 thay vì lỗi có mã.
- `note` kiểm độ dài trước khi trim (200 ký tự + khoảng trắng đầu → 422). Vô hại.
- **T6 phải giữ TTL con trỏ 5'** (vì lỗi xoá cache sau commit chỉ log).
- `brief.md`: lỗi publish có thể đến hai đợt (cây sai cấu trúc trước, rồi prop/ảnh).

**Chưa làm (các task sau)**: list/rollback (T5), `GET /api/site` + con trỏ Redis thật thay no-op (T6), isolation/bất biến
(T7), references (T8), docs module + contract-sync (T9). `brief.md`: FE nạp lại version draft sau publish (R9) — viết sau Gate 1.

## T5

Danh sách bản + rollback: `GET /api/shops/{shopId}/website/publications` (`listPublications`) và
`POST …/publications/{publicationId}/rollback` (`rollbackPublication`) (Bước 8b, S1). **Chạm contract** (2 route mới, dùng lại
`PublicationDto`) — contract-sync gộp ở T9.

**Thay đổi**
- `Vsite.Application/Website/Queries/ListPublications/` (mới) — `Take(50)` theo `Version` giảm dần, chiếu cột tường minh nên
  **không kéo `Snapshot`**; `isCurrent` so với `Website.CurrentPublicationId`. Chưa có website → `WEBSITE_NOT_FOUND`.
- `Vsite.Application/Website/Commands/RollbackPublication/` (mới) — tìm bản bằng `Id + ShopId + WebsiteId` trong một query
  (`PUBLICATION_NOT_FOUND`); đã là current → trả luôn, không ghi; còn lại `Website.PointTo` + `MarkDirty` các draft đang sạch,
  một `SaveChanges`, rồi `ISitePointerCache.InvalidateAsync` (lỗi xoá cache sau commit chỉ log, như publish).
- `Vsite.Api/Website/WebsiteEndpoints.cs` — hai route, `RequireGlobalScope` + `RequireOwner` (#100).
- Test: `PublishWebsiteHandlerTests` (+10 ca), `PublicationEndpointTests` (+4 ca), `WebsiteHandlerKit` (`ListPublications`, `Rollback`).

**Lệch có chủ đích / giả định tự đặt** (người rà ở pha 6)
- Rollback có thêm `409 ROLLBACK_CONFLICT` (plan chỉ liệt kê 200/401/403/404): nếu draft bị lưu giữa lúc đọc và lúc ghi cờ
  (`xmin` lệch) thì báo xung đột thay vì 500. Khai `409` trong OpenAPI — nêu ở `contract-diff.md` (T9). Chưa có test cho nhánh này.
- Chỉ draft đang **sạch** bị ghi khi rollback (draft đã bẩn giữ nguyên) — kết quả quan sát được giống "mọi draft `IsDirty = true`".
- Draft của trang đã xoá mềm không bị đụng (lọc qua `Page` thuộc website).
- Rollback nạp cả entity bản đích (có `Snapshot`, một bản) vì `Website.PointTo` nhận entity.
- Rollback về bản đang chạy: không ghi, không gọi `InvalidateAsync`.

**Kiểm chứng** (Docker CÓ)
- TDD: handler test đỏ vì thiếu kiểu (biên dịch); 4 test HTTP đỏ (405/404) trước khi thêm route → xanh.
- `PublishWebsiteHandlerTests|PublicationEndpointTests|ShopScopedRouteFilterTests|ApiRoutePrefixTests` 47/47.
- `dotnet test backend/vsite.sln` đầy đủ: ComponentSchemaTests 12/12, ArchitectureTests 12/12, IntegrationTests 853 passed / 1 skipped
  (skip có sẵn: `ShopEndpointTests` chờ module Listing).

**Chưa làm (các task sau)**: `GET /api/site` + con trỏ Redis thật (T6), isolation/bất biến (T7), references (T8), docs module +
contract-sync (T9). Theo dõi T4 vẫn còn nguyên (đặc biệt: T6 giữ TTL con trỏ 5').

**Theo dõi (từ `change-reviewer` T5: 0 Critical, 2 Important xử lý bằng tài liệu, 4 Minor — chưa làm)**
- `409 ROLLBACK_CONFLICT` chưa có test race (cần interceptor bump `xmin` của draft như `BumpXminInterceptor`) và là thay đổi contract — nêu ở `contract-diff.md` T9 để Gate 1 chốt; plan §T5 vốn chỉ ghi 200/401/403/404.
- Thiếu test rollback với draft trộn sạch/bẩn và với trang đã xoá mềm (hành vi đã có, chưa có test khoá).
- Publish và rollback chạy song song: `Website` không có concurrency token → last-write-wins, cache xoá sau cả hai, trạng thái vẫn nhất quán. Chấp nhận.
- Có thể thay nạp entity bản đích bằng `Select` cột + `PointTo(Guid)` nếu snapshot lớn gây chậm; test cache-failure nên dùng `using` thay tên đầy đủ (style).

## T6

Endpoint công khai `GET /api/site` (`getPublishedSite`) + con trỏ Redis thật (Bước 8b, S1, #108, #109). **Chạm contract** (route mới,
`PublishedSiteDto`, `304`, header `ETag`/`Cache-Control`) — contract-sync gộp ở T9.

**Thay đổi**
- `ISitePointerCache` thêm `GetAsync`/`SetAsync` + `SitePointer(WebsiteStatus?, Guid?)` (`WebsiteStatus = null` ⇒ shop chưa có website).
- `Vsite.Infrastructure/Website/SitePointerCache.cs` (mới) trên `IDistributedCache`, key `site:pointer:{shopId:N}`, TTL **5'** có website /
  **1'** chưa có; JSON `{websiteStatus, publicationId}`; giá trị hỏng coi như hụt. `NoOpSitePointerCache` đã xoá, DI trỏ bản thật.
- `Vsite.Application/Website/Queries/GetPublishedSite/` (mới) — handler theo plan T6 bước 1–8; **không** chạm `PageDrafts`/`Pages`/`Themes`,
  không `IgnoreQueryFilters()`, không đọc tham số định danh nào (query `?shopId=`/`?host=` không được bind).
- `Vsite.Api/Website/SiteEndpoints.cs` (mới, `AllowAnonymous`, `Produces 200/304/404`) + `api.MapSiteEndpoints()`.
- Test: `PublishedSiteEndpointTests` (12 ca), `SitePointerCacheTests` (4 ca), `WebsiteHandlerKit.RecordingSitePointerCache` +
  `ThrowingPointerCache` khớp interface mới; `ShopScopedRouteFilterTests` + 1 ca.

**Lệch có chủ đích / giả định tự đặt** (người rà ở pha 6)
- **`ShopScopedRouteFilterTests` được nới đúng một ngoại lệ.** Plan bắt `GET /api/site` thuộc document OpenAPI "website", nhưng test #100
  ép MỌI endpoint của document đó là Owner-only → đỏ. Ngoại lệ chỉ khớp: đường dẫn `/api/site`, method `GET` duy nhất, **và** có `AllowAnonymous`;
  thêm test chứng minh cùng đường dẫn thiếu `AllowAnonymous`, hoặc method khác, hoặc route Owner có `AllowAnonymous` vẫn bị bắt.
- `If-None-Match: *` cũng cho 304 (RFC 9110); plan chỉ nói `W/` và danh sách dấu phẩy. Response `Suspended` không có ETag nên không bao giờ 304.
- **304 không đọc `snapshot` và không gọi `IShopLogoReader`** (ETag đã gồm `Shop.UpdatedAt` nên đổi logo/tên làm ETag đổi). Header `ETag` + `Cache-Control`
  vẫn có trên 304.
- Con trỏ cũ (cache trỏ bản không còn): ETag tính từ `publicationId` trong con trỏ nên client giữ ETag của bản đó vẫn nhận 304 tới hết TTL (≤5').
  Chỉ xảy ra khi bản bị xoá cứng (chưa có đường nào làm vậy; prune là 8d) — chấp nhận, TTL là lưới an toàn.
- Cache Redis lỗi (`GetAsync`/`SetAsync` ném) thì request công khai lỗi 500 — không có fallback sang DB. Middleware tenant đã phụ thuộc Redis
  cho mọi request (`ShopLookupService`) nên không thêm độ tin cậy nếu chỉ bọc ở đây.
- Token **của shop khác** (audience `shop:{B}`) gửi trên Host shop A bị `ShopMembershipValidationMiddleware` chặn `403 TOKEN_SHOP_MISMATCH` TRƯỚC khi tới
  endpoint (hành vi có sẵn, fail-closed, không lộ bản của B). Token global (loại login thường) thì kết quả y hệt không token — có test.
- `Shop.UpdatedAt` null (chưa từng sửa) dùng `CreatedAt` cho ETag.
- Thêm ngoài checklist: con trỏ cũ tự chữa (cache trỏ bản không tồn tại → xoá, đọc DB đúng một lần); test TTL qua spy `IDistributedCache`; `www.` host → 404.
- **Redis thật** được dùng trong test (`MediaApiFactory` có Redis Testcontainers) — không phải in-memory.

**Kiểm chứng** (Docker CÓ)
- TDD: test viết trước, đỏ vì thiếu kiểu (biên dịch) → hiện thực → 2 ca đỏ do test gọi sai PUT thay PATCH `/api/shops/{id}` (lỗi test, sửa) → 18/18.
- `ShopScopedRouteFilterTests` đỏ sau khi thêm route (xem lệch ở trên) → sửa → xanh.
- `dotnet test backend/vsite.sln` đầy đủ: ComponentSchemaTests 12/12, ArchitectureTests 12/12, IntegrationTests 871 passed / 1 skipped (skip có sẵn:
  `ShopEndpointTests` chờ module Listing).

**Chưa làm (các task sau)**: test isolation theo Host + bất biến + test kiến trúc quét handler (T7 — R1, R3, R11), references (T8), docs module +
contract-sync (T9; nêu `304`, header `ETag`/`Cache-Control`, `snapshot` schema rỗng ở `contract-diff.md`). `brief.md` (8c): SSR gọi với header `Host` gốc,
gửi lại `If-None-Match`, `Suspended` không có ETag.
