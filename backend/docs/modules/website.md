# Website — vsite

> 📌 **Mọi đường dẫn endpoint trong tài liệu này tương đối với prefix `/api`** (Quyết định #91) — vd.
> `/shops/{shopId}/website` nghĩa là `/api/shops/{shopId}/website`.

## Trạng thái (WEBSITE-001, Bước 5a)

`Website` → `Theme` → `Page` → `PageDraft` (backend) đã code: tạo website trắng, API metadata trang, `GET`/`PUT`
draft có `version`, validate **toàn tree** mỗi lần `PUT`. **Chưa** có publish/rollback (8b), `NavigationConfig` (7),
bảng `*_translation` (#98), `CurrentPublicationId`/`SeedTemplateId`. Contract `website.v1`/`media.v1` còn ở
`contracts/openapi/.staging/` — **chưa qua Gate 1**. Lệch so với `05` và mục "Chưa làm xong":
`Docs/tasks/WEBSITE-001/changelog.md`.

## Đừng nhầm: Shop Site vs Shop Profile

| | Shop Site (module này) | Shop Profile |
|---|---|---|
| URL | `{slug}.vsite.vn`, domain riêng | `vsite.vn/shop/{slug}` |
| Render | `builder-renderer` từ tree trong `PageDraft` (sau 8b: snapshot) | Mẫu thống nhất của vsite, không đụng module này |
| `Review` | **Tuyệt đối không có** (BE từ chối `binding.source = "Review"`) | Có |

## Entity (`Vsite.Domain/Website`)

| Entity | Ghi chú |
|---|---|
| `Website` | 1 shop = 1 website (`ux_website_shop`). `Status`, `DefaultLocale` + `Locales` (`text[]`, `ck_website_default_locale`: mặc định ∈ locales), SEO mặc định, `DefaultOgImageId`, `FaviconAssetId`, `TrackingSnippets` (jsonb). Khoá phụ `(id, shop_id)` cho FK ghép |
| `Theme` | 1:1 với `Website`. `PresetKey?`, `Tokens` (jsonb, có `schemaVersion`). **Không** concurrency (last-write-wins) |
| `Page` | `Kind` (`Composable` / `System`), `SystemType?`, `Slug?` (`""` = trang chủ; **NULL chỉ cho `NotFound`**), SEO, `OgImageId`, `NoIndex`, `IsEnabled`, `SortOrder`. `Page.SoftDelete()`; `CanBeDeleted` (System page và trang chủ không xoá được) |
| `PageDraft` | 1:1 với `Page`. `Tree` (jsonb, `schemaVersion` ở gốc, #94), `Version` (= cột hệ thống `xmin`), `IsDirty` |

Mọi bảng có `shop_id`, mọi FK tới entity thuộc shop là **FK ghép** `(X, shop_id) → (id, shop_id)` (#92).
Tên bảng/cột snake_case số ít (`website`, `theme`, `page`, `page_draft`). `Theme` và `PageDraft` có PK là `Id` của base
(cột `website_id` / `page_id`); `WebsiteId`/`PageId` trên entity chỉ là bí danh đọc — **query LINQ theo `Id`**.
Ràng buộc DB đáng biết: `ck_page_kind_system_type`, `ck_page_slug_not_found`, `ck_page_empty_slug_composable`.

## Route (document OpenAPI `website`, tag `Website`)

Mọi route: `RequireAuthorization(RequireGlobalScope)` + `RequireShopOwner("WEBSITE_OWNER_REQUIRED")` (#100) — đọc lẫn ghi.

| operationId | Method + path | OK | Lỗi khai thêm |
|---|---|---|---|
| `createWebsite` | `POST /shops/{shopId}/website` | 201 | 401 403 404 409 422 |
| `getWebsite` | `GET /shops/{shopId}/website` | 200 | 401 403 404 |
| `listPages` | `GET /shops/{shopId}/website/pages` | 200 (`[]` nếu chưa có website) | 401 403 404 |
| `createPage` | `POST /shops/{shopId}/website/pages` | 201 + `Location` | 401 403 404 409 422 |
| `updatePage` | `PUT /shops/{shopId}/website/pages/{pageId}` | 200 | 401 403 404 409 422 |
| `deletePage` | `DELETE /shops/{shopId}/website/pages/{pageId}` | 204 | 401 403 404 409 |
| `getPageDraft` | `GET /shops/{shopId}/website/pages/{pageId}/draft` | 200 | 401 403 404 |
| `savePageDraft` | `PUT /shops/{shopId}/website/pages/{pageId}/draft` | 200 `{version,isDirty,sanitized}` | 401 403 404 409 413 422 |
| `getTheme` | `GET /shops/{shopId}/website/theme` | 200 | 401 403 404 |
| `updateTheme` | `PUT /shops/{shopId}/website/theme` | 200 | 401 403 404 413 422 |

Lỗi có cấu trúc nằm ở ProblemDetails **`params.errors[]`** (`{path, code, message}`, + `params.truncated`), không phải `errors`.
Danh sách mã lỗi: `Docs/tasks/WEBSITE-001/changelog.md` mục 11; mã con của tree: `packages/builder-components/tests/fixtures/tree/README.md`.

## Quy ước phân quyền và tenant

- **#99 — `ITenantContext` là nguồn `ShopId` duy nhất** cho handler tenant-scoped; command chỉ mang id con (`pageId`…),
  không bao giờ `ShopId` từ body. **Ngoại lệ duy nhất:** `CreateWebsite(shopId)` (lệnh tạo aggregate gốc) nhận `shopId` tường minh
  rồi đối chiếu `ITenantContext.ShopId`; lệch → `SHOP_NOT_FOUND`; tenant null → 403 `TENANT_REQUIRED` (fail-closed).
  Endpoint vẫn phải đi qua filter set tenant. Handler Media/Shop cũ chưa đổi (nợ dọn).
- **#100 — Website và Media chỉ `Owner`**, đọc lẫn ghi, vì `RequireShopMembership` nhận cả `Customer` có token global.
  `ShopScopedRouteFilterTests` duyệt `EndpointDataSource` (theo group **hoặc** prefix `api/shops/{shopId}/(website|media)`) và bắt route quên Owner.
  Route thêm mới phải gắn `RequireShopOwner("<MÃ>")`.
- Page của shop khác qua route shop A → 404 `PAGE_NOT_FOUND` (ownership validate trong câu query, không rò tồn tại).
- Cấm: nhận `ShopId` từ body; `IgnoreQueryFilters()` ngoài allowlist (#90).

## Validation pipeline của `PUT draft` (thứ tự)

`WebsiteEndpoints` đọc body **thô** (không bind `JsonElement` — binding chạy trước filter) với trần `MaxBodyBytes + 4096`; vượt → 413
`TREE_BODY_TOO_LARGE`. Rồi `PageTreeValidator.Validate(treeJson, page.Kind)`:

1. Kích thước body (`Website:TreeLimits:MaxBodyBytes`, mặc định 1 MB) — hơn mức nhưng trong biên → 422 + code con `TREE_BODY_TOO_LARGE`.
2. Cú pháp JSON + độ sâu (`Utf8JsonReader`, trần cứng 64); **khoá trùng** hoặc surrogate lẻ → 422, không 500.
3. Giới hạn depth/nodes (`TREE_TOO_DEEP`, `TREE_TOO_MANY_NODES`).
4. `schemaVersion` + upcast (`DocumentVersioning`); version lạ → `SCHEMA_VERSION_UNSUPPORTED`.
5. Vỏ (JSON Schema `tree-envelope.schema.json`).
6. Node: type tồn tại, `allowedInPageKinds`, `acceptsChildren`/`allowedChildTypes`, `maxPerPage`, props theo `props-schemas.json`, binding source (`Review` bị cấm), `link.url` (`LINK_URL_UNSAFE`), richText qua `RichTextSanitizer` (`inline`/`basic`; vi phạm `dangerous` → `RICHTEXT_UNSAFE`; dọn được → `sanitized: true`).
7. Ảnh: `ImageIds` → `IMediaReferenceValidator` (cùng shop, chưa xoá, đúng loại, **không** là bản Library — #71, #77) → `MEDIA_INVALID_IMAGE_REFERENCE`.
8. System page: `SystemPageGuard.Compare(stored, incoming)` — chỉ sửa được props trong `editableProps`; đổi cấu trúc → `SYSTEM_PAGE_STRUCTURE_CHANGED`, đổi prop không cho sửa → `SYSTEM_PAGE_PROP_NOT_EDITABLE`.
9. Lưu; `version` mới lấy từ `xmin` sau `SaveChanges`.

`requiresProps` của manifest **chưa được enforce** (chờ 5b).

## Config và artifact nạp lúc khởi động

Singleton, resolve **eager** ở `Program.cs` — thiếu/hỏng file làm host dừng ngay với thông báo nêu tên file:

- `config/`: `locales.json`, `theme-tokens.json` (+`defaults`), `system-pages.json`, `sanitize-profiles.json` (+`dangerous`), `binding-sources.json`, `reserved-routes.json`.
- Artifact `packages/builder-components/generated/` (gitignored): `props-schemas.json`, `tree-rules.json`, `tree-envelope.schema.json`, `theme-tokens.schema.json`.
  Fresh clone/CI chạy `pnpm install && pnpm --filter @vsite/builder-components run gen:registry` trước `dotnet build` (target `EnsureWebsiteArtifacts`).
- `WebsiteSchemaProvider` dựng mỗi schema với `SchemaRegistry` **riêng** (nếu không, host thứ hai trong cùng process — `WebApplicationFactory` — ném "Overwriting registered schemas").
- ⚠️ `reservedShopSitePaths` trong `reserved-routes.json` **chưa có** → provider đọc rỗng (fail-open). Người phải thêm; xem changelog "Chờ người (a)".

## Concurrency

- `PageDraft.Version` ↔ `xmin`; `PUT` gửi `version` đã đọc. Lệch → `409 DRAFT_CONFLICT` (`params.currentVersion`, `updatedAt`, `updatedByUserId`), không trả tree.
- `ConcurrencyAndUniqueExceptionHandler` (đăng ký sau `AppExceptionHandler`) map chung `DbUpdateConcurrencyException` → `CONCURRENCY_CONFLICT` và `23505` → `CONFLICT_UNIQUE` (409);
  handler cụ thể bắt `23505` trước để trả `WEBSITE_ALREADY_EXISTS`/`PAGE_SLUG_TAKEN`.
- Cây y hệt bản đã lưu → EF không UPDATE → `version` giữ nguyên.
- `Theme` **không** có concurrency.

## Thêm một System page mới

1. Thêm giá trị vào enum `SystemType` (Domain) **và** khoá cùng tên vào `config/system-pages.json` (`SystemPageCatalog` kiểm khoá == `Enum.GetNames<SystemType>()` — thiếu/thừa là lỗi khởi động).
2. Khai `routes`, `phase`, `seedOnCreate` trong file đó. Nội dung seed nằm trong code `WebsiteSeeder` (loại `seedOnCreate: false` chưa có seed riêng thì dùng nội dung chung); tree seed phải qua được `PageTreeValidator` đúng kind `System`. Prop nào System page được sửa do `editableInSystemPageProps` của manifest quyết (artifact `tree-rules.json`), không khai ở đây.
3. Website **đã có** cần "tạo bù": `WebsiteSeeder.EnsureSystemPages` (idempotent) tạo System page còn thiếu. Gọi nó từ thao tác phù hợp
   (ví dụ nhóm Product ở 10c); không viết SQL bù tay. `NotFound` luôn `Slug = NULL` và `NoIndex` bắt buộc (`PAGE_NOINDEX_REQUIRED`).
4. Thêm test `CreateWebsiteHandlerTests`/`EnsureSystemPages` tương ứng; cập nhật fixture FE nếu có `systemType` mới.

## Thêm một locale

Sửa `config/locales.json` (FE và BE cùng đọc — #97, #98); không hardcode danh sách thứ hai. Slug trang không được trùng mã locale
(`SlugRules`, so không phân biệt hoa/thường). Website cũ không tự đổi `Locales` — chỉ website mới/đổi cấu hình thấy locale mới.
Bật đa ngôn ngữ thật (bảng `*_translation`, overlay `i18n`, URL `/{locale}/…`) là bước L, chưa xếp.

## Migration: `ON DELETE SET NULL (cột)` sửa tay

EF Core không sinh được `SET NULL (col)`. Migration `20261015151020_AddWebsiteModule` sinh FK `Restrict` rồi **sửa tay** ba FK thành:

```sql
ALTER TABLE website ADD CONSTRAINT fk_website_media_asset_default_og_image
  FOREIGN KEY (default_og_image_id, shop_id) REFERENCES media_asset (id, shop_id) ON DELETE SET NULL (default_og_image_id);
ALTER TABLE website ADD CONSTRAINT fk_website_media_asset_favicon
  FOREIGN KEY (favicon_asset_id, shop_id)    REFERENCES media_asset (id, shop_id) ON DELETE SET NULL (favicon_asset_id);
ALTER TABLE page    ADD CONSTRAINT fk_page_media_asset_og_image
  FOREIGN KEY (og_image_id, shop_id)         REFERENCES media_asset (id, shop_id) ON DELETE SET NULL (og_image_id);
```

Model/snapshot vẫn ghi `DeleteBehavior.Restrict` — **đúng chủ đích**. **Đừng sinh lại migration** (sẽ mất đoạn sửa tay); đổi schema thì thêm migration mới và
kiểm `dotnet ef migrations has-pending-model-changes` ra "No changes". Mẫu cùng loại: `shop(logo_id, id) → media_asset(id, shop_id)` (#92).

## Media liên quan

- `SavePageDraft` gọi `IMediaReferenceValidator`; ảnh OG của Page/Website kiểm khi **thay đổi** (ảnh đang gắn bị xoá mềm không chặn sửa tiêu đề).
- Endpoint `references` của Media gom `IMediaReferenceSource`; `PageDraftReferenceSource` (Infrastructure) trả `Kind = PageDraft` + `pageId`/`pageTitle`.
  Đăng ký ở `AddMediaModule` vì Media không được tham chiếu Website (#1). Mỗi lần gọi validate lại draft của các trang liên quan (đủ cho shop nhỏ).
- Media: cả 10 route Owner-only (`MEDIA_OWNER_REQUIRED`).

## Build/test: khi `Vsite.Api.exe` khoá DLL

Một process dev `Vsite.Api.exe` đang chạy từ `backend/src/Vsite.Api/bin/Debug/net9.0` làm `dotnet build` mặc định lỗi `MSB3027`.
**Không cần kill nó.** Dùng một props đổi `OutputPath` (ví dụ `C:\tmp\vout.props`, đặt `BaseOutputPath`/`OutputPath` sang thư mục ngoài repo) và chạy:

```powershell
$env:CustomBeforeMicrosoftCommonProps = 'C:\tmp\vout.props'; dotnet test backend/vsite.sln
```

File props không nằm trong repo (không commit). Test Integration cần Docker (Testcontainers) — Docker không chạy thì ghi vào `Docs/DOCKER-TEST-DEBT.md`, đừng `Skip`.
Một lần chạy full hiếm khi có 2 test `TenantResolutionTests` đỏ do `Docker.DotNet` lúc khởi động container (flake khi nhiều container chạy dồn) — chạy lại riêng trước khi điều tra.
