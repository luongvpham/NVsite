# WEBSITE-001 — changelog: lệch so với thiết kế

Ghi lại mọi chỗ thực thi khác với `DesignIdeal/05-website-builder-and-product-design.md` (và `08` §3.5,
`07` §7.2 nơi liên quan) cùng nguyên nhân, để người duyệt biết chỗ nào là **lệch có chủ đích** (giữ nguyên)
và chỗ nào là **việc chưa làm xong** (kèm nơi theo dõi).

**Task này gồm (Bước 5a của `step.md`, Lane C):**

| Nhóm | Nội dung |
|---|---|
| S0 | Config dùng chung + artifact codegen: `config/locales.json`, `theme-tokens.json` (+`defaults`), `system-pages.json`, khoá `dangerous` trong `sanitize-profiles.json`; artifact `tree-rules.json`, `tree-envelope.schema.json`, `theme-tokens.schema.json`; fixture cây/XSS dùng chung FE+BE |
| T1–T3 | Nạp config + artifact lúc khởi động (fail-fast), entity `Website`/`Theme`/`Page`/`PageDraft`, migration `AddWebsiteModule` (FK ghép #92, `xmin`) |
| T4–T5b | `PageTreeValidator` (vỏ → node → props → binding → richText → ảnh), `RichTextSanitizer`, `SystemPageGuard`, `DocumentVersioning` (upcaster, #94) |
| T6–T9 | Handler: tạo website trắng + System page, API metadata Page, lưu draft nối `IMediaReferenceValidator`, `SlugRules`, Theme; map chung `DbUpdateConcurrencyException`/`23505` → 409 |
| T10 / T10b / T11 | 10 endpoint `/api/shops/{shopId}/website…`; Media chỉ Owner (#100); `references` quét thêm `PageDraft` |
| T12 | Contract: `website.v1.json` + `media.v1.json` ở `.staging/` — **chưa promote** (xem "Chờ người" dưới) |

**Đọc kèm (KHÔNG chép lại ở đây):**
- `Docs/tasks/WEBSITE-001/plan.md` — plan; §4 giữ #99/#100 (chuyển nghĩa sang `backend/docs/modules/website.md`).
- `Docs/tasks/WEBSITE-001/contract-diff.md` — Gate 1 vòng 1 + 5 câu hỏi.
- `backend/docs/modules/website.md` — quy ước vận hành module.
- `packages/builder-components/tests/fixtures/tree/README.md` — danh sách mã lỗi chi tiết `params.errors[].code`.

**Trạng thái kiểm thử khi đóng code (2026-10-16, Docker thật — `Docs/DOCKER-TEST-DEBT.md` rỗng):**
`ComponentSchemaTests` 12 pass · `ArchitectureTests` 11 pass + 1 skip · `IntegrationTests` 755 pass + 2 skip,
0 fail · `builder-components` 245 pass + 1 todo. Hai skip của `IntegrationTests`: skip cũ
`ShopEndpointTests.UpdateShop_Hosted_to_ExternalOnly_...` và skip mới `SlugRulesTests.Real_config_reserves_media_and_api`
(cùng lý do với skip của `ArchitectureTests`: chờ key `reservedShopSitePaths`, xem "Chờ người" (a)).
Một lần chạy giữa chừng có 2 test `TenantResolutionTests` đỏ do `Docker.DotNet` lúc khởi động container
(flake hạ tầng khi nhiều Testcontainers chạy dồn); chạy lại riêng 2/2 pass, lần full cuối 0 fail.

---

## Chờ người (chặn đóng task)

### (a) Key `reservedShopSitePaths` trong `config/reserved-routes.json`

- Plan §4 cần key này (slug trang không được là `media`, `api`, `assets`, `static`…). Hook `guard-write.mjs`
  chặn agent ghi vào file đó (đúng thiết kế), nên **người** phải thêm.
- **Hiện fail-open:** `IReservedRoutesProvider` đọc thiếu key → danh sách rỗng, không vỡ gì. Hệ quả: shop
  hiện **tạo được** trang slug `media`/`api`/`assets`/`static` (route Shop Site sẽ đụng route hệ thống ở 8b).
- Khi đã thêm key: (1) bỏ `Skip` ở `ArchitectureTests.ReservedRoutesTests.ReservedShopSitePaths_in_repo_config_contains_media_and_api`;
  (2) bỏ `Skip` ở `IntegrationTests/Website/SlugRulesTests.Real_config_reserves_media_and_api`;
  (3) đổi provider sang **bắt buộc** (thiếu key → fail-fast lúc khởi động, như các catalog khác);
  (4) đổi `it.todo` ở `packages/builder-components/tests/config-files.test.ts` thành test thật (locale/route System không trùng key).
- Theo dõi: `00-INDEX.md` §4.

### (b) Gate 1 chưa qua — contract chưa promote, `brief.md` chưa viết

- `contracts/openapi/.staging/website.v1.json` và `media.v1.json` đã sinh; `contracts/contract.lock` chưa đổi.
- Cần người trả lời 5 câu hỏi ở `contract-diff.md` rồi chạy
  `node tools/contract-sync/promote.mjs WEBSITE-001 <tên-người-duyệt> website.v1.json media.v1.json`:
  1. tên người duyệt (agent không tự điền); 2. `JsonElement` → schema rỗng cho `tree`/`tokens`;
  3. trần `THEME_BODY_TOO_LARGE` 64 KB và tree `MaxBodyBytes` + 4096 byte biên; 4. `updatePage` = PUT hay PATCH;
  5. Media owner-only đổi hành vi (#100).
- Sau promote mới viết `brief.md` cho 5b (DoD bước 8). **Task chưa đóng được cho tới lúc đó.**

---

## Lệch có chủ đích (giữ nguyên, không phải bug)

### Mô hình dữ liệu

#### 1. `NotFound` có `Slug = NULL` + `ck_page_slug_not_found`

- **`05` §4 nói gì:** mọi `Page` có `Slug` (NotFound dùng slug cố định).
- **Thực thi:** `Page.Slug` nullable; chỉ System page `NotFound` có `Slug = NULL` (không có route riêng — renderer
  bắt 404 rồi dựng trang này). `system-pages.json` ghi `routes: []` cho `NotFound`.
- **Ràng buộc DB:** `ck_page_slug_not_found CHECK ((slug IS NULL) = (system_type IS NOT DISTINCT FROM 'NotFound'))`.
  Plan viết `=`; với `system_type` NULL thì `=` ra NULL và CHECK lọt, tức Composable có slug NULL không bị chặn.
  `IS NOT DISTINCT FROM` đóng lỗ đó (có test `WebsiteDbConstraintTests`).
- **Điều kiện đảo lại:** nếu muốn mọi page có slug để index/unique đơn giản, đặt slug sentinel cho NotFound và bỏ NULL.

#### 2. `ck_page_empty_slug_composable` (không có trong plan)

- **Thực thi:** `CHECK (slug IS DISTINCT FROM '' OR kind = 'Composable')` — slug rỗng (trang chủ) chỉ dành cho
  Composable. Plan chỉ nêu invariant này ở entity; đặt luôn ở DB để dữ liệu sai không lọt qua đường khác.
- **Vì sao `IS DISTINCT FROM`:** `slug <> ''` ra NULL với NotFound và CHECK sẽ lọt/ép sai; ở đây NULL được xử lý tường minh.

#### 3. `ON DELETE SET NULL (cột)` sửa tay trong migration `AddWebsiteModule`

- **`05` §0 #92 nói gì:** FK ghép tới entity thuộc shop; `SET NULL` phải ghi cột — `SET NULL (X)` để `shop_id` không bị NULL.
- **Thực thi:** EF Core không sinh được `ON DELETE SET NULL (col)`. Migration sinh FK với `Restrict` rồi **sửa tay**
  ba FK (`website.default_og_image_id`, `website.favicon_asset_id`, `page.og_image_id`) thành `SET NULL (cột)`.
  Model/snapshot vẫn ghi `DeleteBehavior.Restrict` — đó là chủ đích.
- **Cách giữ:** **KHÔNG sinh lại / không `migrations remove` + `add` lại migration này.** Cần đổi schema thì thêm migration
  mới; `dotnet ef migrations has-pending-model-changes` phải ra "No changes". SQL cụ thể và cách kiểm: `backend/docs/modules/website.md`.
- Test chứng minh: `WebsiteDbConstraintTests` — xoá cứng asset chỉ NULL cột ảnh, `shop_id` giữ nguyên.

#### 4. `Theme` và `PageDraft` có cột audit + `is_deleted` thừa; `Remove` = xoá mềm chặn tạo lại

- **`05` §3/§5 nói gì:** hai bảng này gọn (không audit, không xoá mềm).
- **Thực thi:** cả hai kế thừa `TenantAuditableEntity`; PK là `Id` của base (cột `website_id` / `page_id`), còn
  `WebsiteId`/`PageId` chỉ là bí danh đọc — **không dùng được trong LINQ-to-EF**, query theo `Id`.
  Hệ quả: có cột audit/`is_deleted` thừa; `DbSet.Remove()` thành xoá mềm (interceptor) nên **không tạo lại được** hàng cùng PK.
  Code không xoá `Theme`/`PageDraft` ở đâu cả; `Page.SoftDelete()` không xoá draft (draft mồ côi vô hình — mọi đường đọc join `Page` còn sống;
  `PageDraftReferenceSource` cũng join).
- **Điều kiện đảo lại:** khi cần "reset website" hoặc job dọn (Bước 8) thì tách base không audit hoặc xoá cứng có chủ đích.

#### 5. `Version` = `xmin` đổi sang `long`

- **`05` §5:** token `xmin` + `version` trong contract. **Thực thi:** cột hệ thống `xmin` (uint) map vào `PageDraft.Version`;
  DTO trả `long`, opaque với FE — gửi lại nguyên văn. Npgsql bỏ qua cột hệ thống trong `CREATE TABLE`, nên migration
  không có cột `xmin`. Khi cây y hệt bản đã lưu, EF không UPDATE → version giữ nguyên (chấp nhận).
- `PUT draft` trả `{version, isDirty, sanitized}`, **không** trả lại tree; `409 DRAFT_CONFLICT` kèm `params.currentVersion`,
  `updatedAt`, `updatedByUserId`, không trả tree.

#### 6. Chỉ `Website`, `Theme`, `Page`, `PageDraft` — thiếu cột/phần của `05`

- `Website` **chưa có** `CurrentPublicationId`, `SeedTemplateId` (Bước 8b / 11), chưa có `Status` transition (Publish/Suspend).
- `NavigationConfig` chưa có (Bước 7). Bảng `*_translation` chưa có (#98 — chỉ `DefaultLocale`/`Locales`).
- **Tạo website qua endpoint** `POST /shops/{shopId}/website`, **không** gắn vào `CreateShop` (shop tạo xong có thể chưa cần website;
  tạo bù `EnsureSystemPages` cho nhóm Product ở 10c). Bước 11 sẽ thêm đường tạo từ template.

### Hành vi API

#### 7. `updatePage` là `PUT` thay toàn bộ (plan Flow 3 ghi PATCH)

- Form "Thuộc tính trang" gửi đủ trường. Riêng `slug = null` nghĩa là **không đổi** slug. Ảnh OG chỉ kiểm khi **thay đổi**
  (ảnh đang gắn bị xoá mềm không làm hỏng việc sửa tiêu đề). **Điều kiện đảo lại:** nếu FE muốn gửi từng trường → đổi verb ở Gate 1 (câu hỏi 4).

#### 8. `createPage` trả 201 + `Location`; `createWebsite` 201; `deletePage` 204

- Plan chỉ nói rõ 201 cho `createWebsite`. `createPage` tự tạo `PageDraft` rỗng (không thì `GET draft` của trang mới là 404).

#### 9. `listPages` khi chưa có website trả `200 []`

- Chỉ `getWebsite` trả `404 WEBSITE_NOT_FOUND`. OpenAPI của `listPages` vẫn khai 404 (thừa). **Điều kiện đảo lại:** nếu FE muốn
  phân biệt "chưa có website" ở `listPages` thì đổi sang 404 (Gate 1).

#### 10. Lỗi có cấu trúc ở `params.errors[]`, không ở `errors` cấp ProblemDetails

- `AppException.Errors` là từ điển `{field:[msg]}` (#19). Danh sách `{path, code, message}` đặt ở **`params.errors[]`**
  (+ `params.truncated`); `errors` (từ điển) điền theo path (`"$"` cho gốc). Không sửa `AppExceptionHandler` dùng chung
  để khỏi đổi contract toàn cục. FE 5b phải cast kiểu `params`.

#### 11. Mã lỗi mới

`WEBSITE_OWNER_REQUIRED`, `WEBSITE_NOT_FOUND`, `WEBSITE_ALREADY_EXISTS`, `WEBSITE_REQUIRES_HOSTED_SHOP`, `PAGE_NOT_FOUND`,
`PAGE_SLUG_TAKEN`, `PAGE_SLUG_RESERVED`, `PAGE_SLUG_INVALID`, `PAGE_SYSTEM_SLUG_FIXED`, `PAGE_HOME_SLUG_FIXED`,
`PAGE_NOINDEX_REQUIRED`, `PAGE_NOT_DELETABLE`, `TREE_INVALID`, `THEME_INVALID`, `TREE_BODY_TOO_LARGE`, `THEME_BODY_TOO_LARGE`,
`DRAFT_CONFLICT`, `RICHTEXT_UNSAFE`, `LINK_URL_UNSAFE`, `SCHEMA_VERSION_UNSUPPORTED`, `MEDIA_INVALID_IMAGE_REFERENCE`,
`CONCURRENCY_CONFLICT`, `CONFLICT_UNIQUE`, `TENANT_REQUIRED`, `SHOP_NOT_FOUND` (CreateWebsite). Mã con trong `params.errors[].code`:
`TREE_*`, `NODE_TYPE_UNKNOWN`, `PROP_UNKNOWN`, `BINDING_SOURCE_NOT_ALLOWED`, `SYSTEM_PAGE_*`, `THEME_TOKEN_INVALID`… — nguồn là
fixture README (S0 đề xuất, BE dùng đúng tên; **FE 5b phải dùng cùng tên**).

#### 12. Slug sai khuôn → `PAGE_SLUG_INVALID` (422); kiểm reserved chạy trước khuôn

- Khuôn `\A[a-z0-9]+(-[a-z0-9]+)*\z` ≤ 120 (dùng `\z`, không `$` — `$` nhận `"a\n"`). So reserved không phân biệt hoa/thường (`EN` → `RESERVED`).
  Trang chủ (`Slug = ""`) không đổi slug và không trang nào khác thành trang chủ (`PAGE_HOME_SLUG_FIXED`).

#### 13. Giới hạn cây (G6) và mã `TREE_BODY_TOO_LARGE`

- `Website:TreeLimits` (appsettings): `MaxBodyBytes` (1 MB), `MaxDepth`, `MaxNodes`. Trần JSON lồng 64 tầng là cứng (System.Text.Json/JsonSchema.Net)
  nên `MaxDepth` > ~28 không có tác dụng. Mã `TREE_BODY_TOO_LARGE`/`TREE_TOO_DEEP`/`TREE_TOO_MANY_NODES` do BE đặt (plan R8 chỉ nói "413/422").
- Body đọc **thô** (không để Minimal API bind `JsonElement` — binding chạy trước filter): trần = `MaxBodyBytes + 4096` byte biên;
  vượt hẳn → **413**, trong biên nhưng hơn `MaxBodyBytes` → **422** `TREE_INVALID` với `params.errors[].code = TREE_BODY_TOO_LARGE`.
  Theme: trần 64 KB → 413 `THEME_BODY_TOO_LARGE`; `ThemeTokensValidator` giới hạn thêm 16 KB. Các con số này do BE tự đặt (Gate 1 câu hỏi 3).
- 413 route-aware: `BadHttpRequestExceptionHandler` đọc metadata `RequestBodyTooLargeErrorMetadata`; route không có metadata giữ `MEDIA_FILE_TOO_LARGE`.
- Thứ tự validate: body size → cú pháp + độ sâu JSON → depth/nodes → phiên bản/upcast → vỏ → node (lệch nhỏ so Flow 2: giới hạn trước phiên bản để
  cây độc hại không tới JSON Schema đệ quy).

#### 14. `link.url` whitelist scheme (`LINK_URL_UNSAFE`)

- Không có trong plan: `cta.target.url` chưa lọc là stored XSS (`javascript:`). BE chỉ nhận scheme trong whitelist, chuẩn hoá bỏ ký tự điều khiển/khoảng trắng.

#### 15. Khoá `dangerous` trong `config/sanitize-profiles.json`

- Plan không có. Danh sách thẻ/thuộc tính/scheme nguy hiểm dùng chung để cờ `unsafe` (→ `RICHTEXT_UNSAFE`, reject) tách khỏi "dọn được" (→ `sanitized: true`).
  Khoá này **không phải tên profile**: `sanitizeRichText('dangerous')` throw, `gen-registry` và catalog BE loại khỏi tập tên profile.
  `unsafe` cũng bật khi thuộc tính nguy hiểm nằm trên **thẻ bị cắt** (`<img onerror>`, `src=javascript:`); `data:` ở `src` ảnh dán từ clipboard → `RICHTEXT_UNSAFE`.

#### 16. Config mới và artifact codegen mới (S0)

- `config/locales.json`, `config/theme-tokens.json` (+ khoá `defaults` đủ `schemaVersion/colors/typography/shape/spacing`), `config/system-pages.json`
  (6 `SystemType`; `SystemPageCatalog` kiểm khoá == `Enum.GetNames<SystemType>()`).
- Artifact (gitignored, sinh bằng `pnpm gen:registry`, build BE có target `EnsureWebsiteArtifacts`): `tree-rules.json`, `tree-envelope.schema.json`,
  `theme-tokens.schema.json`. Registry (`*.manifest.ts`, `registry.lock.json`) **không đổi** (G8).
- Binding trong tree = `{source, filters?}` (khớp `05` §6); `props-schemas.json` chỉ khai `filters`, không cấm key khác.

### Thư viện / bảo mật

#### 17. `HtmlSanitizer` 9.2.1039 (MIT, AngleSharp 1.7.2)

- Bản 9.0.892 (có sẵn trong cache) ghim AngleSharp 0.17.1 dính GHSA-pgww-w46g-26qg (mXSS) nên nâng 9.2.1039. NuGetAudit mặc định không cảnh báo
  vuln bắc cầu — thêm package thì chạy `dotnet list package --vulnerable --include-transitive`.
- `HtmlSanitizerOptions` không tự điền mặc định (khác `new HtmlSanitizer()`): phải set `UriAttributes = HtmlSanitizerDefaults.UriAttributes` nếu không `href`/`src` không bị kiểm scheme.
- Ràng buộc thuộc tính theo từng thẻ (`a` → href/target/rel) làm ở `PostProcessNode`. Một `HtmlSanitizer` mới mỗi lần gọi (chưa pool).
- **R4:** `<scr<script>ipt>alert(1)</scr</script>ipt>` → **`clean`**, không reject: parser HTML5 đọc `scr<script` là tên thẻ, không tạo phần tử script
  (output `ipt&gt;alert(1)ipt&gt;`, đã encode). Nếu đổi parser phải chạy lại vector này.
- Sanitizer **không bật unsafe cho `meta`/`style`/`template`** (bị cắt âm thầm, không reject) — FE biết khi viết `sanitize-html.ts`.

### Phân quyền (#99, #100)

#### 18. Website và Media chỉ `Owner` — đổi hành vi Media

- `RequireShopMembership` nhận cả `Customer`; khách có token global gọi được. Cả 10 route website và **10/10 route Media**
  (`uploadToSlot`, `uploadToLibrary`, `listLibrary`, `cloneFromLibrary`, `getAssetReferences`, `getDerivatives`, `getAssetsByIds`, `getMediaUsage`,
  `deleteFromLibrary`, `uploadShopLogo`) dùng `RequireShopOwner("WEBSITE_OWNER_REQUIRED" | "MEDIA_OWNER_REQUIRED")`.
- **Hành vi đổi:** Manager/Staff/Accountant/Customer có membership Active trước dùng được Media, nay nhận 403 `MEDIA_OWNER_REQUIRED`.
  Shape contract `media.v1` không đổi (403 đã khai). `ShopScopedRouteFilterTests` tự duyệt `EndpointDataSource` và bắt route quên Owner.
- **Điều kiện đảo lại:** khi làm vai Manager/Staff (nghiệp vụ phân quyền nhân viên) thì nới theo permission, không nới về "member".

#### 19. `ITenantContext` là nguồn `ShopId` duy nhất (#99); ngoại lệ `CreateWebsite`

- Handler Website không nhận `ShopId` từ request/route trong command. Ngoại lệ: `CreateWebsite(shopId)` nhận tường minh rồi **đối chiếu**
  `ITenantContext.ShopId == command.ShopId` (lệch → `SHOP_NOT_FOUND`); tenant null → 403 `TENANT_REQUIRED` (fail-closed).

### MEDIA-001 nối vào Website

#### 20. Validator ảnh đã nối; `references` quét `PageDraft`

- `IMediaReferenceValidator` gọi trong `SavePageDraft` (+ ảnh OG ở Page/Website). `GetReferencesHandler` gom `IMediaReferenceSource`;
  `PageDraftReferenceSource` (Infrastructure, đăng ký ở `AddMediaModule` — Media không tham chiếu Website) lấy `imageId` bằng `PageTreeValidator`
  rồi so tập clone; một mục/trang dù nhiều chỗ; so GUID theo `Guid` (GUID HOA không lọt). `MediaReferenceKind` thêm `PageDraft`,
  `MediaReferenceDto` thêm `pageId?`/`pageTitle?` (additive; classifier gắn nhãn BREAKING chỉ vì thêm giá trị enum).

---

## Chưa làm xong

| # | Việc | Nơi theo dõi |
|---|---|---|
| 1 | Key `reservedShopSitePaths` + bỏ 2 `Skip` + provider bắt buộc (hiện **fail-open**: slug `media`/`api`/`assets`/`static` chưa bị chặn) | Mục "Chờ người (a)" trên; `00-INDEX.md` §4 |
| 2 | Gate 1: promote contract (`contract-diff.md` câu hỏi 1–5) và `brief.md` | Mục "Chờ người (b)"; `00-INDEX.md` §3 |
| 3 | `requiresProps` của manifest **chưa enforce** ở BE (đúng `data-needs.md` câu 7) | Bước 5b / 6 — khi editor cần |
| 4 | `props-schemas.json` thiếu `additionalProperties: false` ở `image`/`binding`/`cta` (nên key lạ ở các prop này lọt qua vỏ) | Sửa ở manifest/Zod (#60) trước 5b; ghi `00-INDEX.md` §4 |
| 5 | Comment `IMediaReferenceValidator` nói `IsInLibrary` nhưng code kiểm theo `Kind` (REFACTOR-DB-001) — chỉ lệch chữ | Sửa comment khi chạm file lần sau |
| 6 | `GET /api/shops/{shopId}` (Shop) còn mở cho `Customer` có token global — chưa Owner-only | `00-INDEX.md` §4 — làm trong task Shop (cùng #100) |
| 7 | Handler Media (và Shop) cũ vẫn lấy `ShopId` từ route/command, không từ `ITenantContext` | `00-INDEX.md` §4 — nợ dọn theo #99 |
| 8 | Theme **không** có concurrency (G7: last-write-wins) | `00-INDEX.md` §4 (brief 5b) |
| 9 | `PUT draft` có thể trả `sanitized: true` (FE 5b phải nạp lại tree sạch); `params.errors[]` | `00-INDEX.md` §4 (brief 5b) |
| 10 | `JsonElement` → schema rỗng ở `tree`/`tokens` (Orval ra `interface JsonElement {}`); FE dùng Zod của `builder-components` | Gate 1 câu 2 |
| 11 | Website chưa có `CurrentPublicationId`/`SeedTemplateId`/`NavigationConfig`/bảng translation; chưa có publish/rollback | Bước 7, 8b, 11, L (#98) |
| 12 | `references` validate lại toàn bộ draft của shop có clone liên quan (đủ cho shop nhỏ; cần cache/cột index ảnh nếu thành điểm nóng); `references` của **clone id** không báo `PageDraft` | Bước 8 (job dọn mồ côi) |
| 13 | `DeletePage` không xoá mềm draft; không có job dọn | Bước 8 |

**Minor đã hoãn trong review (không chặn, không có nơi theo dõi riêng — gom ở đây):**
- S0: `gen-registry.test.ts` assertion "itemProps đệ quy" rỗng (`?? 'text'` luôn defined); `richtext-danger.ts` thiếu vector `svg onload`, `href` khoảng trắng đầu, `srcdoc`, `img data:`; `defaults` chưa chạy qua `theme-tokens.schema.json`.
- A: `catch` của `WebsiteSchemaProvider` chỉ bắt `JsonException`/`JsonSchemaException`; `TreeRulesCatalog` không validate enum.
- B: link kind khác có `url` không kiểm; `url` rỗng bị từ chối; helper TS `String.fromCodePoint` có thể `RangeError`.
- C: GUID HOA giữ nguyên chuỗi trong cây (T11 so theo `Guid` nên không ảnh hưởng); `GetWebsite` dùng `FirstAsync` trang chủ (500 nếu dữ liệu hỏng); `PresetKey` chỉ kiểm độ dài.
- D: bind JSON trước filter Owner (JSON hỏng ra 400 thay 403); không kiểm `Content-Type`; TestServer không áp `MaxRequestBodySize` nên không assert được Media 413 qua HTTP.

---

## Giả định tôi đã tự đặt

Ngoài các mục trên (đều do BE tự quyết, đã nêu lý do): seed `Liên hệ`/`Không tìm thấy trang` bằng `RichText01` tiếng Việt ngắn;
`CreatePage` không nhận `kind`/`systemType`/`shopId`; id node seed `^c_[a-z0-9]{5}$`.
