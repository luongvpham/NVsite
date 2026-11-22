# Website — vsite

> 📌 **Mọi đường dẫn endpoint trong tài liệu này tương đối với prefix `/api`** (Quyết định #91) — vd.
> `/shops/{shopId}/website` nghĩa là `/api/shops/{shopId}/website`.

## Trạng thái (WEBSITE-001, Bước 5a; WEBSITE-004, Bước 8b; WEBSITE-007, Bước 7)

`Website` → `Theme` → `Page` → `PageDraft` (backend) đã code: tạo website trắng, API metadata trang, `GET`/`PUT`
draft có `version`, validate **toàn tree** mỗi lần `PUT`. **8b (WEBSITE-004) đã code:** `SitePublication` + publish/rollback + endpoint đọc công khai `GET /api/site` (xem các mục bên dưới; brief FE: `Docs/tasks/WEBSITE-004/brief.md`, lệch: `Docs/tasks/WEBSITE-004/changelog.md`). **Bước 7 (WEBSITE-007, BE) đã code:** `NavigationConfig` + `GET`/`PUT` menu + snapshot mang menu (mục "Menu (`NavigationConfig`)"; lệch: `Docs/tasks/WEBSITE-007/changelog.md`). **Chưa** có
bảng `*_translation` (#98), `SeedTemplateId`, prune bản publish (8d), custom domain (`ShopDomain`). Contract `website.v1`/`media.v1` đã qua Gate 1 và promote
(`contracts/openapi/`, `luongpham`); đã merge `master`, Gate 2 đóng 2026-11-12. Brief cho FE: `Docs/tasks/WEBSITE-001/brief.md`. Lệch so với `05` và mục "Chưa làm xong":
`Docs/tasks/WEBSITE-001/changelog.md`.

## Đừng nhầm: Shop Site vs Shop Profile

| | Shop Site (module này) | Shop Profile |
|---|---|---|
| URL | `{slug}.vsite.vn`, domain riêng | `vsite.vn/shop/{slug}` |
| Render | `builder-renderer` từ snapshot của bản đang publish (draft chỉ dùng trong editor) | Mẫu thống nhất của vsite, không đụng module này |
| `Review` | **Tuyệt đối không có** (BE từ chối `binding.source = "Review"`) | Có |

## Entity (`Vsite.Domain/Website`)

| Entity | Ghi chú |
|---|---|
| `Website` | 1 shop = 1 website (`ux_website_shop`). `Status`, `DefaultLocale` + `Locales` (`text[]`, `ck_website_default_locale`: mặc định ∈ locales), SEO mặc định, `DefaultOgImageId`, `FaviconAssetId`, `TrackingSnippets` (jsonb). Khoá phụ `(id, shop_id)` cho FK ghép |
| `Theme` | 1:1 với `Website`. `PresetKey?`, `Tokens` (jsonb, có `schemaVersion`). **Không** concurrency (last-write-wins) |
| `Page` | `Kind` (`Composable` / `System`), `SystemType?`, `Slug?` (`""` = trang chủ; **NULL chỉ cho `NotFound`**), SEO, `OgImageId`, `NoIndex`, `IsEnabled`, `SortOrder`. `Page.SoftDelete()`; `CanBeDeleted` (System page và trang chủ không xoá được) |
| `PageDraft` | 1:1 với `Page`. `Tree` (jsonb, `schemaVersion` ở gốc, #94), `Version` (= cột hệ thống `xmin`), `IsDirty` |
| `SitePublication` | **Bất biến** (`TenantEntity`, không audit/soft delete, không setter). Một bản = snapshot toàn site (`Snapshot` jsonb, `schemaVersion` ở gốc), `Version` tăng dần theo website (`ux_site_publication_website_version`, không tái dùng số sau rollback), `Note ≤ 200`, `PublishedAt`, `PublishedByUserId`. Khoá phụ `(id, website_id)`; FK ghép `(website_id, shop_id) → website`. `Website.CurrentPublicationId` trỏ bản đang chạy qua FK ghép `(current_publication_id, id) → site_publication (id, website_id)` — không trỏ chéo website được (R4) |
| `NavigationConfig` | 1:1 với `Website` (PK `Id` = website id, cột `website_id`). `Items` (jsonb `{ schemaVersion, items }`, `DocumentKind.Navigation`), `IsDirty`, `Version` (= `xmin`). FK ghép `(website_id, shop_id) → website`. **Không bao giờ xoá mềm** (PK là `website_id` — dòng xoá mềm bị filter giấu nhưng vẫn giữ PK ⇒ lưu mãi 409). Chi tiết: mục "Menu" |

Mọi bảng có `shop_id`, mọi FK tới entity thuộc shop là **FK ghép** `(X, shop_id) → (id, shop_id)` (#92).
Tên bảng/cột snake_case số ít (`website`, `theme`, `page`, `page_draft`). `Theme` và `PageDraft` có PK là `Id` của base
(cột `website_id` / `page_id`); `WebsiteId`/`PageId` trên entity chỉ là bí danh đọc — **query LINQ theo `Id`**.
Ràng buộc DB đáng biết: `ck_page_kind_system_type`, `ck_page_slug_not_found`, `ck_page_empty_slug_composable`.

## Route (document OpenAPI `website`, tag `Website`)

Mọi route **trừ `getPublishedSite`**: `RequireAuthorization(RequireGlobalScope)` + `RequireShopOwner("WEBSITE_OWNER_REQUIRED")` (#100) — đọc lẫn ghi.

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
| `getNavigation` | `GET /shops/{shopId}/website/navigation` | 200 `{version: int\|null, isDirty, items}` (menu **đã ghép**) | 401 403 404 `WEBSITE_NOT_FOUND` |
| `saveNavigation` | `PUT /shops/{shopId}/website/navigation` | 200 `{version, isDirty}` | 400 `NAVIGATION_BODY_INVALID` · 401 403 404 · 409 `NAVIGATION_CONFLICT` · 413 `NAVIGATION_BODY_TOO_LARGE` · 422 `NAVIGATION_INVALID` |
| `publishWebsite` | `POST /shops/{shopId}/website/publications` | 201 + `Location` | 401 403 404 409 `PUBLISH_CONFLICT` · 422 `PUBLISH_BLOCKED` |
| `listPublications` | `GET /shops/{shopId}/website/publications` | 200 **mảng** (≤ 50, version giảm dần, không kèm snapshot) — ngoại lệ có chủ đích của "một shape pagination" (#19): danh sách có trần cứng, không phân trang; trần thành 20 khi có prune (8d). Như `listPages` | 401 403 404 |
| `rollbackPublication` | `POST /shops/{shopId}/website/publications/{publicationId}/rollback` | 200 | 401 403 404 `PUBLICATION_NOT_FOUND` · 409 `ROLLBACK_CONFLICT` |
| `getPublishedSite` | **`GET /api/site`** (tag `Site`, cùng document `website`) | 200 · 304 | 404 `SITE_NOT_FOUND` — **`AllowAnonymous`**, xem mục "Đọc công khai" |

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

`requiresProps` của manifest **không** enforce ở `PUT draft` (draft dở dang là hợp lệ); nó được kiểm lúc **publish** (`RequiredPropsChecker`, mã `REQUIRED_PROP_MISSING`, nguồn `tree-rules.json` → `requiresProps`).

## Config và artifact nạp lúc khởi động

Singleton, resolve **eager** ở `Program.cs` — thiếu/hỏng file làm host dừng ngay với thông báo nêu tên file:

- `config/`: `locales.json`, `theme-tokens.json` (+`defaults`), `system-pages.json`, `sanitize-profiles.json` (+`dangerous`), `binding-sources.json`, `reserved-routes.json`.
- Artifact `packages/builder-components/generated/` (gitignored): `props-schemas.json`, `tree-rules.json`, `tree-envelope.schema.json`, `theme-tokens.schema.json`, `site-snapshot.schema.json` (vỏ snapshot; chỉ dùng ở test/khởi động, không nằm trên đường phục vụ `GET /api/site`).
  Fresh clone/CI chạy `pnpm install && pnpm --filter @vsite/builder-components run gen:registry` trước `dotnet build` (target `EnsureWebsiteArtifacts`).
- `WebsiteSchemaProvider` dựng mỗi schema với `SchemaRegistry` **riêng** (nếu không, host thứ hai trong cùng process — `WebApplicationFactory` — ném "Overwriting registered schemas").
- `reservedShopSitePaths` trong `reserved-routes.json` là key **bắt buộc**: thiếu → `ReservedRoutesProvider` ném lỗi lúc khởi động (fail-fast), như các catalog khác.

## Publish / rollback (8b)

- **Publish** (một `SaveChanges`): gom website + theme + mọi trang **đang bật** và draft; mỗi trang `PageTreeValidator` → `RequiredPropsChecker` → `IMediaReferenceValidator` (ảnh tree + OG trang);
  OG mặc định + favicon kiểm một lần (`pageId = null`). Bất kỳ lỗi nào → `422 PUBLISH_BLOCKED` (`params.errors[] { pageId, pageTitle, path, code, message }`, ≤ 100, `params.truncated`), không ghi gì.
  Rồi chuẩn hoá `imageId` về GUID chữ thường (R14), nạp bảng `media`, `Version = max + 1`, `SnapshotBuilder.Build`, `Website.Publish` (Draft → Published; **Suspended giữ nguyên**), hạ `IsDirty` **chỉ** draft đang bẩn
  (⇒ `version` draft đó đổi — FE nạp lại). Menu: `Merge` → `ForSnapshot` (chỉ mục đang hiện, trỏ trang có trong snapshot) → vào snapshot; chưa có dòng `NavigationConfig` ⇒ tạo từ menu tự sinh; `MarkPublished()` cùng `SaveChanges`. Menu **không** chặn publish.
  23505 / `xmin` lệch (website hoặc menu) → `409 PUBLISH_CONFLICT`. Sau commit: `ISitePointerCache.InvalidateAsync` (lỗi chỉ log; TTL 5 phút là lưới an toàn).
- **Snapshot v1** (khác `05` §7): `schemaVersion, version, defaultLocale, locales, theme{presetKey,tokens}, navigation{items}, seo{…faviconAssetId}, tracking, media{imageId→{storageKey,preset}}, pages[]` (mỗi trang có `id`; `slug: null` chỉ ở `NotFound`).
  `navigation.items` (#113, từ WEBSITE-007): `{ key: "page:{guid chữ thường}"|"custom", label: string|null, url? (chỉ custom), children? }`, tối đa 3 cấp (cấp 3 không `children`), **không** `hidden`, thứ tự = vị trí mảng. Bản publish trước Bước 7 có `items: []` — vẫn hợp lệ, `schemaVersion` vẫn 1. Nhãn/URL do FE ghép từ `pages` — BE không dựng URL.
- **Rollback** chỉ đổi con trỏ (`Website.PointTo`), không sinh bản mới, không đổi `Status`, không đụng nội dung draft; draft đang sạch → `IsDirty = true` (G7); menu có dòng → `IsDirty = true`. Tìm bản bằng `Id + ShopId + WebsiteId` trong một query.

## Menu (`NavigationConfig`, Bước 7 — #113)

- **Ghép lúc đọc** (`NavigationItems.Merge`, hàm thuần): chưa có dòng ⇒ tự sinh từ mọi trang chưa xoá trừ `NotFound`, theo `SortOrder` rồi `Title`, tất cả hiện (`version: null`, `isDirty: true`); trang chưa có trong menu ⇒ nối **cuối cấp 1, `hidden`**; trang đã xoá ⇒ bỏ cả nhánh con. Trang **đang tắt** vẫn giữ trong menu (không render, không vào snapshot). Không ghi chéo từ `CreatePage`/`DeletePage`.
- **Lưu** (`PUT`): body thô có trần 256 KB (413), ba kiểu DTO cho ba cấp (cấp 3 `additionalProperties: false`, `key`/`hidden` bắt buộc) ⇒ sai shape/cấp 4/thiếu field ⇒ `400 NAVIGATION_BODY_INVALID` (`detail` chỉ đường dẫn cấu trúc, không lặp input).
  Rồi `Validate` ⇒ `422 NAVIGATION_INVALID` `params.errors[] {path (JSON Pointer vào items), code, message}`: `NAV_PAGE_NOT_FOUND` (cả trang shop khác / đã xoá — không lộ tồn tại), `NAV_PAGE_DUPLICATE`, `NAV_PAGE_NOT_ALLOWED` (`NotFound`), `NAV_URL_UNSAFE` (`LinkUrlRules.IsSafe`: http(s)/mailto/tel; > 2048), `NAV_LABEL_REQUIRED` (custom), `NAV_LABEL_TOO_LONG` (> 60), `NAV_TOO_MANY_ITEMS` (> 12 cấp 1, > 10 mỗi cha). `NAV_TOO_DEEP` chỉ phòng dữ liệu lưu hỏng — qua HTTP cấp 4 là 400.
  Lưu `Normalize(items)` (trim; nhãn rỗng ⇒ `null`; khoá GUID chữ thường) ⇒ `IsDirty = true`.
- Trang tra **trong câu query** theo tenant + `website_id` (#21.3); `ShopId` chỉ từ `ITenantContext` (#99).

## Đọc công khai: `GET /api/site` (#108, #109)

- **Tenant suy từ header `Host`** (`TenantResolutionMiddleware`), không có `shopId`/`host` ở path/query/body; `AllowAnonymous`. Handler chỉ chạy khi `AudienceKind == Shop`, nếu không → `404 SITE_NOT_FOUND`.
  Đọc qua Global Query Filter, **không** `IgnoreQueryFilters()`.
- **Ranh giới cứng:** handler `GetPublishedSite` **không chạm `db.PageDrafts`, `db.Pages`, `db.Themes`** (`PublishedSiteBoundaryTests` quét nguồn). Chỉ đọc `Shop` (name, status, logo, updatedAt), `Website` (status, `CurrentPublicationId`) và `SitePublication`.
- Một mã `404 SITE_NOT_FOUND` cho mọi "không có site": host không phải shop, shop `Closed`, chưa có website, website `Draft`/chưa publish (quyết định theo cả `Status` và `CurrentPublicationId`, R6). Shop `Draft` **không** chặn.
- Website hoặc shop `Suspended` → `200 { status: "Suspended", shop, publication: null }`, không ETag.
- `ETag` strong `"{publicationId:N}-{shop.UpdatedAt unix ms}"` + `Cache-Control: no-cache`; `If-None-Match` (kể cả `W/`, danh sách, `*`) khớp → 304 không đọc snapshot. Header **không** khai trong OpenAPI.
- **Con trỏ Redis** `site:pointer:{shopId:N}` → `{ websiteStatus, publicationId? }` (`SitePointerCache` trên `IDistributedCache`): **TTL 5 phút** khi có website, **1 phút** khi chưa có website. Xoá ngay khi publish/rollback; `Shop` đọc sống mỗi request (đình chỉ shop có hiệu lực tức thì).
  Race đọc↔publish có thể giữ con trỏ cũ tới 5 phút (chấp nhận). Khi có endpoint đình chỉ/gỡ `Website` phải gọi `InvalidateAsync`.
- `snapshot` ra OpenAPI là schema rỗng (`JsonElement`); FE parse bằng Zod `siteSnapshotSchema`.

## Concurrency

- `PageDraft.Version` ↔ `xmin`; `PUT` gửi `version` đã đọc. Lệch → `409 DRAFT_CONFLICT` (`params.currentVersion`, `updatedAt`, `updatedByUserId`), không trả tree.
- `ConcurrencyAndUniqueExceptionHandler` (đăng ký sau `AppExceptionHandler`) map chung `DbUpdateConcurrencyException` → `CONCURRENCY_CONFLICT` và `23505` → `CONFLICT_UNIQUE` (409);
  handler cụ thể bắt `23505` trước để trả `WEBSITE_ALREADY_EXISTS`/`PAGE_SLUG_TAKEN`.
- Cây y hệt bản đã lưu → EF không UPDATE → `version` giữ nguyên.
- `Theme` **không** có concurrency.
- `NavigationConfig.Version` ↔ `xmin`. `PUT` gửi `version` đã đọc: `null` mà đã có dòng, số mà chưa có dòng, số lệch, race insert lần đầu (23505 — unique duy nhất của bảng là PK) hay `DbUpdateConcurrencyException` ⇒ `409 NAVIGATION_CONFLICT` `params { currentVersion, updatedAt }` (**cả hai null** khi chưa có dòng). Lưu ý: publish khi menu chưa từng lưu **tạo dòng** ⇒ tab đang giữ `version: null` lưu sau đó nhận 409 — FE nạp lại menu sau publish/rollback.
- `Website` có concurrency token = cột hệ thống `xmin` (shadow property `RowVersion`, không lộ ra Domain/DTO; migration `WebsiteRowVersion` không sinh SQL). Publish và rollback đều ghi dòng này ⇒ chạy song song thì bên sau nhận `409 PUBLISH_CONFLICT` / `ROLLBACK_CONFLICT`, không còn last-write-wins (review I1, #112).

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

## Quyết định #111, #112

- **#111 — Trạng thái `Shop` đối với Shop Site.** Chỉ `Suspended` (→ `200 { status: "Suspended", publication: null }`, không ETag) và `Closed` (→ `404 SITE_NOT_FOUND`) ảnh hưởng site.
  Shop `Draft` **không** chặn: shop mới tạo mặc định `Draft` và chưa có luồng kích hoạt riêng. Ai được chuyển `ShopStatus` nào thuộc module Shop — hiện Owner tự đặt được bất kỳ (nợ trước production, `00-INDEX` §4).
- **#112 — Quy tắc publish/rollback.** Một trang **đang bật** lỗi ⇒ chặn cả lần publish (`422 PUBLISH_BLOCKED`, lỗi theo trang, không ghi gì); trang đang tắt không bị kiểm, không vào snapshot;
  trang chủ tắt vẫn publish được (site không có trang chủ — 8c hiện `NotFound`). Rollback chỉ đổi con trỏ: không sinh bản mới, không đổi `Status`, không đụng nội dung draft (draft sạch → `IsDirty = true`).
  Publish ∥ rollback bị chặn bằng concurrency token của `Website`. 8b không có endpoint gỡ xuất bản / đình chỉ.

## Media liên quan

- `SavePageDraft` gọi `IMediaReferenceValidator`; ảnh OG của Page/Website kiểm khi **thay đổi** (ảnh đang gắn bị xoá mềm không chặn sửa tiêu đề).
- Endpoint `references` của Media gom `IMediaReferenceSource`; nguồn của Website (Infrastructure, đăng ký ở `AddMediaModule` vì Media không được tham chiếu Website — #1): `PageDraftReferenceSource` (`PageDraft` + `pageId`/`pageTitle`; validate lại draft của các trang liên quan),
  `SitePublicationReferenceSource` (`SitePublication`: đọc key `snapshot.media` của mọi bản, gộp một mục, `isCurrent`), `SeoImageReferenceSource` (`PageOgImage`, `WebsiteOgImage`, `WebsiteFavicon`; #110). Xoá ảnh Library không kéo theo clone nên site đã publish không mất ảnh (R11).
- Media: cả 10 route Owner-only (`MEDIA_OWNER_REQUIRED`).

## Build/test: khi `Vsite.Api.exe` khoá DLL

Một process dev `Vsite.Api.exe` đang chạy từ `backend/src/Vsite.Api/bin/Debug/net9.0` làm `dotnet build` mặc định lỗi `MSB3027`.
**Không cần kill nó.** Dùng một props đổi `BaseOutputPath` sang thư mục **trong** project (ví dụ `C:\tmp\vout.props`):

```xml
<Project><PropertyGroup><BaseOutputPath>$(MSBuildProjectDirectory)\bin\alt\</BaseOutputPath></PropertyGroup></Project>
```

```powershell
$env:CustomBeforeMicrosoftCommonProps = 'C:\tmp\vout.props'; dotnet test backend/vsite.sln
```

**Đừng** đặt output **ngoài repo**: `TreeFixtures` đi ngược lên từ thư mục test DLL để tìm `pnpm-workspace.yaml`, nên ~270 test Website đỏ giả (WEBSITE-007).
`bin/alt` nằm dưới `backend/**/bin/` (đã gitignore). File props không nằm trong repo (không commit). Test Integration cần Docker (Testcontainers) — Docker không chạy thì ghi vào `Docs/DOCKER-TEST-DEBT.md`, đừng `Skip`.
Một lần chạy full hiếm khi có 2 test `TenantResolutionTests` đỏ do `Docker.DotNet` lúc khởi động container (flake khi nhiều container chạy dồn) — chạy lại riêng trước khi điều tra.
