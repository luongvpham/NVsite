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
