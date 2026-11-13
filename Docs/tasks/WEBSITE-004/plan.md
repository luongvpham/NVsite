# WEBSITE-004 — `SitePublication`, publish/rollback, endpoint đọc công khai (Bước 8b, BE)

> **Đã chốt 2026-11-13 — chưa code**
>
> **Lane C.** Entity mới, contract mới, lần đầu có endpoint không token suy tenant từ Host.
> Cổng: Gate 1 → Gate 2. Tuần tự S0 (codegen, ngắn) → S1 (Backend). Không có session FE — FE là Bước 8c, task riêng.
>
> **Người thực thi dùng skill:** `superpowers:subagent-driven-development` hoặc
> `superpowers:executing-plans`. Mỗi bước có checkbox `- [ ]`.

**Mục tiêu:** Chủ shop xuất bản toàn bộ website thành một bản bất biến và quay lại bản cũ bằng một
thao tác. Khách vãng lai mở Host của shop thì BE trả đúng bản đang xuất bản của shop đó, không bao giờ
trả bản nháp.
**Nhu cầu dữ liệu:** [`data-needs.md`](data-needs.md) · **Spec:** `DesignIdeal/step.md` §8b, `05` §2, §6, §7, §25 mục 4
· **Quyết định liên quan:** #7, #21, #41, #90, #92, #94, #97, #99, #100, #103, #104, **#108**, **#109**

Đường dẫn trong file này tương đối với root repo.

## 1. Yêu cầu (đã chốt với người)

- **Người dùng / vai trò:** chủ shop (`Owner`) xuất bản và rollback; khách vãng lai (không token) đọc site.
- **Điểm chạm:** 3 endpoint Owner dưới `/api/shops/{shopId}/website/publications…`; 1 endpoint công khai
  `GET /api/site` trên Host của shop; `references` của Media báo thêm nguồn.
- **Xong nghĩa là** (`step.md` §8b): publish sinh một bản bất biến; sửa draft sau đó không đổi kết quả
  endpoint công khai; rollback về bản cũ trong một thao tác; gọi bằng Host shop A không đọc được bản của
  shop B; website `Draft` trả 404; `Suspended` trả trạng thái.
- **Không làm trong task này:** `apps/web` render + SSR loader (8c) · `NavigationConfig` thật (7 —
  `navigation.items` để rỗng) · job prune 20 bản (8d) · dữ liệu bind `Service`/`Product` (9) · custom
  domain / bảng `ShopDomain` · bảng `*_translation` · màn hình Portal cho publish · endpoint gỡ xuất bản /
  đình chỉ website · anchor `#nodeId` và năm © (chuyển 8c, G3).

## 2. Hiện trạng (khảo sát 2026-11-12/13)

| Khả năng | Trạng thái | Ở đâu | Ghi chú |
|---|---|---|---|
| Suy tenant từ Host cho mọi request, kể cả không token | ✅ có | [TenantResolutionMiddleware.cs:27-53](../../../backend/src/Vsite.Api/Tenancy/TenantResolutionMiddleware.cs#L27-L53) | Nhãn đầu của Host → `Shop.Slug` → `ShopId` + `AudienceKind = Shop`; chạy trước xác thực ([Program.cs:179](../../../backend/src/Vsite.Api/Program.cs#L179)) |
| Tra slug có cache Redis | ✅ có | [ShopLookupService.cs:29-56](../../../backend/src/Vsite.Infrastructure/Shop/ShopLookupService.cs#L29-L56) | 30 phút; không tồn tại 1 phút; có `InvalidateAsync` — khuôn cho cache con trỏ publish |
| Global Query Filter fail-closed | ✅ có | [TenantQueryFilterExtensions.cs:49-53](../../../backend/src/Vsite.Infrastructure/Persistence/TenantQueryFilterExtensions.cs#L49-L53) | Không có `ShopId` → rỗng |
| Allowlist `IgnoreQueryFilters()` | ✅ có | [IgnoreQueryFiltersAllowlistTests.cs:21-35](../../../backend/tests/ArchitectureTests/IgnoreQueryFiltersAllowlistTests.cs#L21-L35) | Task này không thêm mục nào |
| Dev proxy giữ Host | ✅ có | [vite.config.ts:31-34](../../../apps/web/vite.config.ts#L31-L34) | `changeOrigin: false` |
| SSR tự đặt header khi gọi BE | 🟡 một phần | [axios-instance.ts:174-175](../../../packages/api-sdk/src/mutator/axios-instance.ts#L174-L175) | `createApiClient` nhận `headers`; việc gửi `Host` tự đặt từ Node chưa chạy thật — kiểm ở 8c |
| Test gửi Host tuỳ ý qua `WebApplicationFactory` | ✅ có | [TenantResolutionTests.cs:75](../../../backend/tests/IntegrationTests/Identity/TenantResolutionTests.cs#L75) · [ShopApiHarness.cs:82](../../../backend/tests/IntegrationTests/Common/ShopApiHarness.cs#L82) | `request.Headers.Host = …`; factory có Postgres + Redis thật ([AppApiFactory.cs:35](../../../backend/tests/IntegrationTests/Common/AppApiFactory.cs#L35)) |
| `Website.Status` / `CurrentPublicationId` | 🟡 một phần | [Website.cs:50](../../../backend/src/Vsite.Domain/Website/Entities/Website.cs#L50) · [Website.cs:12](../../../backend/src/Vsite.Domain/Website/Entities/Website.cs#L12) | `Status` luôn `Draft`, chưa có hàm đổi; chưa có `CurrentPublicationId` |
| `SitePublication` | ❌ chưa có | — | Entity, bảng, migration, FK ghép hai chiều (`05` §7) |
| Đích FK ghép `(id, shop_id)` của `website` | ✅ có | [WebsiteConfiguration.cs:42](../../../backend/src/Vsite.Infrastructure/Persistence/Configurations/Website/WebsiteConfiguration.cs#L42) | `ak_website_id_shop_id` |
| Route Website hiện có | ✅ có | [WebsiteEndpoints.cs:45-60](../../../backend/src/Vsite.Api/Website/WebsiteEndpoints.cs#L45-L60) | Mọi route `RequireGlobalScope` + `RequireOwner` (#100) — khuôn cho publish/rollback |
| Validate lại tree đã lưu | ✅ có | [PageTreeValidator.cs:46](../../../backend/src/Vsite.Application/Website/Validation/PageTreeValidator.cs#L46) | Trả `Errors` + `ImageIds`, không ném; [PageDraftReferenceSource.cs:49](../../../backend/src/Vsite.Infrastructure/Website/PageDraftReferenceSource.cs#L49) đang gọi lại trên draft đã lưu |
| Kiểm ảnh trong tree | ✅ có | [SavePageDraftHandler.cs:71](../../../backend/src/Vsite.Application/Website/Commands/SavePageDraft/SavePageDraftHandler.cs#L71) · [IMediaReferenceValidator.cs:21-24](../../../backend/src/Vsite.Application/Media/Interfaces/IMediaReferenceValidator.cs#L21-L24) | Ném `MEDIA_INVALID_IMAGE_REFERENCE`, không nói id nào sai |
| `requiresProps` ở BE | ❌ chưa có | [gen-registry.ts:490-499](../../../packages/builder-components/scripts/gen-registry.ts#L490-L499) · [IWebsiteConfigCatalogs.cs:75-82](../../../backend/src/Vsite.Application/Website/Interfaces/IWebsiteConfigCatalogs.cs#L75-L82) | `tree-rules.json` chỉ xuất `variants: string[]`; không artifact nào trong `generated/` mang `requiresProps` → cần S0 |
| Vỏ JSON định nghĩa bằng Zod → JSON Schema (#97) | ✅ có cho Tree/Tokens | [gen-registry.ts:13](../../../packages/builder-components/scripts/gen-registry.ts#L13) (`meta/envelope-schema`) | Chưa có vỏ Snapshot |
| Khung upcaster (#94) | ✅ có cho Tree/Tokens | [DocumentVersioning.cs:8-18](../../../backend/src/Vsite.Application/Website/Versioning/DocumentVersioning.cs#L8-L18) · [StoredDocuments.cs:11](../../../backend/src/Vsite.Application/Website/Versioning/StoredDocuments.cs#L11) | `DocumentKind` chưa có `Snapshot` |
| `references` của Media | 🟡 một phần | [GetReferencesHandler.cs:27-30](../../../backend/src/Vsite.Application/Media/Queries/GetReferences/GetReferencesHandler.cs#L27-L30) · [MediaReferencesDto.cs:17-21](../../../backend/src/Vsite.Application/Media/Dtos/MediaReferencesDto.cs#L17-L21) | Mới có `ShopLogo` + `PageDraft`; nguồn mới = một `IMediaReferenceSource` đăng ký ở [DependencyInjection.cs:122](../../../backend/src/Vsite.Infrastructure/DependencyInjection.cs#L122) + giá trị enum |
| Renderer cần gì từ snapshot | ✅ đã rõ | [site-url.ts:9-14](../../../packages/builder-components/src/site-url.ts#L9-L14) · [context.tsx:14-17](../../../packages/builder-components/src/context.tsx#L14-L17) · [site-layout.tsx:10-17](../../../packages/builder-renderer/src/site-layout.tsx#L10-L17) | `pages[].id`; bảng `imageId → { storageKey, preset }`; `shop { name, logoUrl }` |
| Logo shop → URL | ✅ có | [IShopLogoReader.cs:15](../../../backend/src/Vsite.Application/Shop/Interfaces/IShopLogoReader.cs#L15) | Website `dependsOn` Shop + Media ([dependency-map.json:20](../../architecture/dependency-map.json#L20)) |

### Phát hiện khi khảo sát

1. **`05` §7 thiếu `pages[].id`.** Link nội bộ trong tree trỏ theo id trang; không có id thì renderer không dựng
   được URL. Snapshot v1 thêm `id` — lệch có chủ đích, ghi changelog + banner `05`.
2. **Site công khai chưa có đường lấy ảnh.** Endpoint tra ảnh là Owner-only (#100). Chốt: đóng băng bảng ảnh
   trong snapshot (§4).
3. **Shop mới tạo có `Status = Draft`** ([Shop.cs:30](../../../backend/src/Vsite.Domain/Shop/Entities/Shop.cs#L30)) và
   không có luồng kích hoạt riêng. Nếu coi shop `Draft` là 404 thì mọi site mới đều 404. Chốt ở §4: chỉ
   `Suspended`/`Closed` ảnh hưởng site.
4. **Owner tự đặt được `ShopStatus` bất kỳ** qua `UpdateShopRequest`
   ([ShopEndpoints.cs:76](../../../backend/src/Vsite.Api/Shop/ShopEndpoints.cs#L76)) — kể cả tự gỡ `Suspended`. Nợ có
   từ SHOP-001, **không sửa ở task này**; ghi vào changelog mục theo dõi.
5. **Publish sửa dòng `page_draft` (hạ `IsDirty`) → `xmin` đổi → `version` của draft đổi.** Editor đang mở sẽ
   nhận `409 DRAFT_CONFLICT` ở lần autosave kế nếu không nạp lại version. Xử lý ở §4 và R9.
6. **`shop.name`/`logoUrl` là dữ liệu sống nằm cùng response với snapshot bất biến.** `ETag` chỉ theo id bản
   publish thì đổi tên shop không bao giờ tới được SSR. Xử lý ở §4 (ETag gồm cả `Shop.UpdatedAt`).
7. [`backend/docs/modules/website.md:83`](../../../backend/docs/modules/website.md#L83) ghi `requiresProps` "chờ 5b" — thực tế 8b.
   Sửa ở T9.
8. Cấu hình Caddy/CDN chưa được kiểm trong khảo sát này — không ảnh hưởng 8b vì HTML không cache (#109).

## 3. Flow

Ký hiệu: ✅ đã có · 🟡 có một phần · ❌ chưa có.

### Flow 1: Chủ shop xuất bản

```text
1. POST /api/shops/{shopId}/website/publications { note? }    ❌ T4
2. Kiểm Owner + đặt tenant                                    ✅ khuôn WebsiteEndpoints.cs:53-54
3. Gom Website (SEO, locales, tracking) + Theme + mọi Page
   còn sống, đang bật (IsEnabled) và draft của chúng          ❌ T2 — ShopId từ ITenantContext (#99)
4. Validate lại từng tree bằng PageTreeValidator              ✅ PageTreeValidator.cs:46
5. Kiểm requiresProps theo variant của từng node              ❌ S0-T1 (artifact) + T3 (BE)
6. Kiểm ảnh trong tree + ảnh OG còn sống, đúng shop           ✅ IMediaReferenceValidator.cs:21 (gọi theo từng trang)
7. Dựng snapshot v1 (navigation.items = [], bảng media)       ❌ T2
8. Ghi SitePublication, Version = max + 1                     ❌ T1 + T4
9. Website.CurrentPublicationId = bản mới; Draft → Published  ❌ T1 + T4
10. Hạ IsDirty cho draft của các trang vừa publish            🟡 cờ có (PageDraft.cs:41), hàm hạ cờ ❌ T1
11. Xoá con trỏ cache Redis của shop                          ❌ T6 (interface) — gọi ở T4
12. Trả 201 { id, version, publishedAt, note, isCurrent }     ❌ T4
```

Nhánh lỗi:
- **Bất kỳ trang đang bật nào lỗi** → **chặn cả lần publish**, `422 PUBLISH_BLOCKED`, `params.errors[]` mỗi
  phần tử `{ pageId, pageTitle, path, code, message }` (tối đa 100 mục, `params.truncated`). Không ghi gì.
- Trang đang tắt không vào snapshot và không bị kiểm. Trang chủ đang tắt vẫn publish được: snapshot không có
  trang chủ, 8c hiện `NotFound` (G10).
- Chưa có website → `404 WEBSITE_NOT_FOUND` (đã có). Không phải Owner → `403 WEBSITE_OWNER_REQUIRED` (đã có).
- Hai lần publish cùng lúc, hoặc draft bị lưu giữa lúc đọc và lúc ghi → `409 PUBLISH_CONFLICT`.
- Website `Suspended` → vẫn ghi bản mới và trỏ current, `Status` **giữ `Suspended`** (G6).
- `note` dài hơn 200 ký tự → `422` qua validator.

### Flow 2: Chủ shop rollback

```text
1. POST /api/shops/{shopId}/website/publications/{publicationId}/rollback   ❌ T5
2. Tìm bản theo id + ShopId + WebsiteId trong MỘT câu query                  ❌ T5
3. Website.CurrentPublicationId = bản đó; mọi draft IsDirty = true           ❌ T5
4. Xoá con trỏ Redis; trả 200 thông tin bản đang chạy                        ❌ T5
```

- **Không đụng nội dung draft.** Chỉ dựng cờ `IsDirty` (bản nháp giờ khác bản đang chạy — G7).
- Bản của shop khác / không tồn tại → `404 PUBLICATION_NOT_FOUND`. FK ghép `(current_publication_id, id)` là lớp
  chặn thứ hai ở DB (#92).
- Rollback về chính bản đang chạy → 200, không ghi gì, không đụng cờ.
- Rollback không đổi `Status` (website `Suspended` vẫn `Suspended`).

### Flow 3: Chủ shop xem danh sách bản đã xuất bản

```text
1. GET /api/shops/{shopId}/website/publications   ❌ T5
   → [{ id, version, publishedAt, publishedByUserId, note, isCurrent }], version giảm dần, KHÔNG kèm snapshot
```

Trần 50 bản mới nhất (job prune 20 bản là 8d). Chưa có website → `404 WEBSITE_NOT_FOUND`.

### Flow 4: Khách vãng lai đọc site theo Host

```text
1. Trình duyệt mở {slug}.vsite.vn → SSR apps/web lấy host từ URL        ❌ 8c
2. SSR gọi GET /api/site, header Host = host gốc, If-None-Match?         ❌ T6
3. Middleware suy ShopId + AudienceKind = Shop                           ✅ TenantResolutionMiddleware.cs:42-47
4. Handler: audience phải là Shop, nếu không → 404                       ❌ T6
5. Đọc Shop (name, status, logo, updatedAt) — một query theo khoá chính  ❌ T6
6. Đọc con trỏ { websiteStatus, publicationId } từ Redis, hụt thì DB     ❌ T6
7. So ETag → 304; không khớp thì đọc snapshot (Global Filter) → 200      ❌ T6
```

- `404 SITE_NOT_FOUND` cho mọi trường hợp "không có site": host không phải shop, host `admin`/host chính, shop
  `Closed`, chưa có website, website `Draft` hoặc chưa publish lần nào (G4).
- `200 { status: "Suspended", shop, publication: null }` khi website `Suspended` **hoặc** shop `Suspended`.
- Không token, không cookie; có token cũng không đổi kết quả. Không bao giờ đọc `page_draft`.
- Query string `?shopId=`/`?host=` bị bỏ qua hoàn toàn.

### Flow 5: Xoá ảnh Library — cảnh báo đang dùng

```text
1. GET …/media/library/{assetId}/references     ✅ GetReferencesHandler.cs:27-30
2. Nguồn PageDraft                               ✅ PageDraftReferenceSource.cs:18
3. Nguồn snapshot (bản đang chạy + bản còn giữ)  ❌ T8 — đọc bảng snapshot.media, không validate lại tree
4. Nguồn ảnh OG của Page / Website, favicon      ❌ T8
```

## 4. Đã chốt trong session lập plan

| # | Chốt |
|---|---|
| **#108** | **Mô hình đọc công khai của Shop Site** (người chốt 2026-11-13, đã ghi `DECISIONS.md`). Endpoint công khai nằm trên Host của shop, không có `shopId`/`host` ở path, query hay body. SSR `apps/web` lấy host từ URL gốc (subdomain hoặc domain riêng) và gọi BE với header `Host` bằng host đó; `TenantResolutionMiddleware` suy `ShopId`; handler chỉ chạy khi audience là `Shop`, đọc qua Global Query Filter, không `IgnoreQueryFilters()`. Một endpoint `GET /api/site`, `ETag` + 304. Không có endpoint công khai đọc theo số version. |
| **#109** | **Cache dữ liệu, không cache HTML** (người chốt 2026-11-13, đã ghi `DECISIONS.md`; đóng `05` §25 mục 4). Con trỏ ở Redis xoá ngay khi publish/rollback; snapshot giữ theo `ETag`; dữ liệu bind (Bước 9) đi endpoint riêng TTL riêng; HTML không cache CDN. |
| — | Publish gặp trang lỗi → chặn cả lần publish, trả lỗi theo trang (người chốt 2026-11-13) |
| — | Rollback không đụng nội dung draft (người chốt 2026-11-13) |
| — | 8b không có endpoint gỡ xuất bản / đình chỉ (người chốt 2026-11-13) |
| — | **Các dòng dưới đây do tôi chọn theo uỷ quyền "chọn phương án tối ưu" (2026-11-13) — người đọc lại ở pha 6** |
| — | Task ID `WEBSITE-004`. |
| — | **Bảng ảnh đóng băng trong snapshot** (`snapshot.media`: `imageId → { storageKey, preset }`), gồm mọi `imageId` trong tree của các trang được publish + ảnh OG của trang/website + favicon (bản Library: `preset = null`, `storageKey` là file gốc). Lý do: `storageKey` của một asset không đổi; đọc công khai không tốn query Media; `references` chỉ cần đọc bảng này. |
| — | **Trạng thái shop:** chỉ `Suspended` (→ như website `Suspended`) và `Closed` (→ 404) ảnh hưởng site. Shop `Draft` **không** chặn — shop mới tạo mặc định `Draft` (phát hiện 3). |
| — | **`ETag` = `"{publicationId:N}-{shop.UpdatedAt unix ms}"`** (strong). Đổi tên/logo shop làm ETag đổi mà không cần publish (phát hiện 6). Response `Suspended` không có `ETag`. `Cache-Control: no-cache` (bắt hỏi lại, cho phép 304). |
| — | **Con trỏ Redis chỉ giữ dữ liệu của Website:** key `site:pointer:{shopId}` → `{ websiteStatus, publicationId? }`, TTL 5 phút; "chưa có website" cũng được cache (1 phút). Dòng `Shop` đọc sống mỗi request (một query theo khoá chính) nên đình chỉ shop có hiệu lực ngay và module Shop không phải biết cache của Website (#1). |
| — | **`references` báo cả bản publish cũ**, gộp thành tối đa một mục `SitePublication` kèm `isCurrent` (true nếu bản đang chạy dùng ảnh). Nguồn OG/favicon: `PageOgImage` (kèm `pageId`, `pageTitle`), `WebsiteOgImage`, `WebsiteFavicon`. |
| — | **`requiresProps` — "thiếu" nghĩa là:** key vắng hoặc `null`; `text` rỗng sau trim; `richText` không còn ký tự nào sau khi bỏ thẻ; `image` không có `imageId` chuỗi khác rỗng; `list` có 0 phần tử; `binding` không có `source`; kind khác chỉ tính vắng/`null`. Mã lỗi con `REQUIRED_PROP_MISSING`, `path` = `/…/props/{tên}`. |
| — | **Vỏ snapshot định nghĩa một lần bằng Zod** ở `packages/builder-components/meta/envelope-schema.ts`, sinh `generated/site-snapshot.schema.json` (#97). BE có test khẳng định snapshot dựng ra hợp lệ theo schema đó; 8c dùng lại đúng Zod này. `DocumentKind.Snapshot`, `DocumentSchemaVersions.Snapshot = 1` (#94). |
| — | **Sau publish, `version` của các draft vừa hạ cờ đổi.** Response publish không trả lại version; FE nạp lại draft/pages sau publish (ghi vào `brief.md`). Chỉ draft đang `IsDirty` mới bị ghi (draft sạch không đổi `xmin`). |
| — | Tên endpoint / `operationId`: `publishWebsite`, `listPublications`, `rollbackPublication`, `getPublishedSite`. Cả bốn thuộc document OpenAPI `website`. |

### Shape snapshot v1 (thay shape ở `05` §7)

```json
{
  "schemaVersion": 1,
  "version": 7,
  "defaultLocale": "vi",
  "locales": ["vi"],
  "theme": { "presetKey": null, "tokens": { "schemaVersion": 1, "…": "…" } },
  "navigation": { "items": [] },
  "seo": { "defaultTitle": null, "defaultDescription": null, "ogImageId": null, "faviconAssetId": null },
  "tracking": {},
  "media": { "5b0c…": { "storageKey": "shops/…/x.webp", "preset": "1600x900,cover" } },
  "pages": [
    { "id": "…", "slug": "", "kind": "Composable", "systemType": null, "title": "Trang chủ",
      "seo": { "title": null, "description": null, "ogImageId": null, "noIndex": false },
      "tree": { "schemaVersion": 1, "id": "root", "type": "Page", "children": [] } }
  ]
}
```

`pages` xếp theo `SortOrder` rồi `Title`. `tree` là `CleanTree` do validator trả (đã upcast, richText đã sạch).
`slug: null` chỉ ở trang `NotFound`.

### Shape response `GET /api/site`

```json
{ "status": "Published",
  "shop": { "name": "Spa ABC", "logoUrl": "/media/…" },
  "publication": { "id": "…", "version": 7, "publishedAt": "…", "snapshot": { "…": "…" } } }
```

`publication` nullable (null khi `Suspended`). `snapshot` là `JsonElement` (OpenAPI ra schema rỗng — cùng tiền lệ
`tree`/`tokens`, WEBSITE-001 changelog #10; FE dùng Zod).

## 5. Trình tự session

- **S0 — codegen (`packages/builder-components`), ngắn.** Không sửa manifest nào → không cần cổng duyệt
  Registry, `registry.lock.json` không đổi. Chỉ sửa script sinh artifact và thêm Zod vỏ snapshot. Phải xong
  trước S1 vì BE build đọc artifact.
- **S1 — Backend.** T1 → T9 tuần tự. Kết thúc bằng contract-sync → **Gate 1**.
- Không có S2. FE (8c + màn publish Portal) mở task riêng sau Gate 1; `brief.md` viết sau promote.
- **Gate 2** sau khi `change-reviewer` + `review.md` xong.

## 6. Ràng buộc toàn cục

- **#21 / #99:** `SitePublication` kế thừa `TenantEntity` (có `ShopId`, tự có Global Query Filter). Handler lấy
  `ShopId` từ `ITenantContext.RequireShopId()` ([WebsiteTenantExtensions.cs:14](../../../backend/src/Vsite.Application/Website/WebsiteTenantExtensions.cs#L14)); command chỉ mang `publicationId`/`note`.
  Ownership của `publicationId` validate **trong câu query**. Không thêm `IgnoreQueryFilters()` nào.
- **#108:** `GET /api/site` không đọc bất kỳ tham số định danh shop nào; chỉ chạy khi
  `ITenantContext.AudienceKind == Shop`.
- **`05` §7 — ranh giới cứng:** handler của `GET /api/site` không được chạm `db.PageDrafts`, `db.Pages`, `db.Themes`.
- **#100:** ba route Owner mới nằm trong group `/shops/{shopId}/website` và phải gắn `RequireGlobalScope` +
  `RequireOwner()` — `ShopScopedRouteFilterTests` sẽ đỏ nếu quên.
- **#19 / #91:** enum string; mọi route có `.WithName(...)` và `Produces`/`ProducesProblem` cho từng status;
  lỗi là ProblemDetails có `error_code`.
- **#1:** Media không tham chiếu Website — nguồn `references` mới cài ở `Vsite.Infrastructure/Website`, đăng ký
  trong `AddMediaModule` như `PageDraftReferenceSource`.
- **File generated không sửa tay** (`packages/builder-components/generated/**`); `contracts/openapi/*.json`
  không ghi thẳng — hook `guard-write.mjs` chặn là đúng.
- **Migration:** thêm migration mới, **không** sinh lại `AddWebsiteModule` (có SQL sửa tay —
  `backend/docs/modules/website.md` mục "Migration"). Sau khi thêm: `dotnet ef migrations has-pending-model-changes` ra "No changes".
- **Build khi `Vsite.Api.exe` đang chạy:** dùng `CustomBeforeMicrosoftCommonProps` (`backend/docs/modules/website.md` mục cuối), không kill process.
- **Docker có sẵn trên máy này** — test tích hợp chạy thật. `Docs/DOCKER-TEST-DEBT.md` đang rỗng; nếu session
  thực thi không có Docker thì ghi nợ theo quy ước, không `Skip`.

## 7. Review focus — lỗi dễ lọt khi spec im lặng

| # | Tình huống | Hành vi đúng | Test ở |
|---|---|---|---|
| R1 | Gọi `GET /api/site` với Host shop A, kèm `?shopId={B}` hoặc token Owner của shop B | Vẫn trả bản của A; không có đường nào ra bản của B | T7 |
| R2 | Gọi `GET /api/site` trên host `admin.…` / host chính / host slug không tồn tại | `404 SITE_NOT_FOUND`, không 500, không 200 rỗng | T6 |
| R3 | Sửa draft, đổi theme, đổi SEO, tắt trang, xoá trang **sau** publish | Response `GET /api/site` giữ nguyên từng byte của `snapshot`, `ETag` không đổi | T7 |
| R4 | Rollback bằng `publicationId` của shop khác (Owner hợp lệ của shop mình) | `404 PUBLICATION_NOT_FOUND`; `CurrentPublicationId` không đổi; chèn tay bằng SQL thì FK ghép từ chối | T1, T5 |
| R5 | Publish/rollback xong, request công khai kế tiếp | Thấy bản mới ngay (con trỏ Redis đã bị xoá), không chờ TTL | T6 |
| R6 | Website `Draft` nhưng đã có dòng `site_publication` (không xảy ra qua API, phòng dữ liệu lệch) | 404 — quyết định theo `Status` và `CurrentPublicationId`, cả hai phải thoả | T6 |
| R7 | Trang lỗi nằm ở trang **đang tắt** | Không chặn publish; trang đó không có trong snapshot | T4 |
| R8 | Website trắng vừa tạo (trang chủ rỗng + `Contact` + `NotFound` seed) | Publish thành công ngay — seed không được vi phạm `requiresProps` | T4 |
| R9 | Publish trong lúc editor đang mở trang (autosave sau đó dùng version cũ) | Publish thành công; autosave kế nhận `409 DRAFT_CONFLICT` (hành vi đã có) — ghi `brief.md`: FE nạp lại version sau publish. Draft sạch không bị ghi nên version không đổi | T4 |
| R10 | Draft được lưu giữa lúc publish đọc và ghi | `409 PUBLISH_CONFLICT`, không có bản publish nửa vời, cờ `IsDirty` của draft đó vẫn `true` | T4 |
| R11 | Xoá mềm ảnh sau khi publish | `GET /api/site` vẫn trả `snapshot.media` có ảnh đó (file sống tới 8d); `references` trước khi xoá đã cảnh báo | T7, T8 |
| R12 | Đổi tên shop sau publish | `ETag` đổi, response mang tên mới, `snapshot` không đổi | T6 |
| R13 | Snapshot vượt kích thước hợp lý | Không đặt trần mới: mỗi tree đã bị trần 1 MB ở `PUT draft`; ghi kích thước snapshot vào log publish | T4 (log) |

## 8. Sơ đồ file

```
packages/builder-components/
  scripts/gen-registry.ts                         sửa  (requiresProps vào tree-rules.json; ghi site-snapshot.schema.json)
  meta/envelope-schema.ts                         sửa  (Zod vỏ snapshot + buildSiteSnapshotJsonSchema)
  src/index.ts                                    sửa  (export schema/type snapshot)
  tests/gen-registry.test.ts                      sửa
  tests/site-snapshot-schema.test.ts              mới

backend/src/Vsite.Domain/Website/
  Entities/SitePublication.cs                     mới
  Entities/Website.cs                             sửa  (CurrentPublicationId, Publish, PointTo)
  Entities/PageDraft.cs                           sửa  (MarkPublished, MarkDirty)
backend/src/Vsite.Application/
  Common/Interfaces/IAppDbContext.cs              sửa  (DbSet<SitePublication>)
  Website/Interfaces/IWebsiteConfigCatalogs.cs    sửa  (TreeTypeRule.RequiresProps)
  Website/Interfaces/ISitePointerCache.cs         mới
  Website/Publishing/SnapshotBuilder.cs           mới
  Website/Publishing/RequiredPropsChecker.cs      mới
  Website/Publishing/PublishErrors.cs             mới  (PublishBlockedException, PublishPageError)
  Website/Versioning/DocumentVersioning.cs        sửa  (DocumentKind.Snapshot)
  Website/Commands/PublishWebsite/{Command,Handler,Validator}.cs        mới
  Website/Commands/RollbackPublication/{Command,Handler}.cs             mới
  Website/Queries/ListPublications/{Query,Handler}.cs                   mới
  Website/Queries/GetPublishedSite/{Query,Handler}.cs                   mới
  Website/Dtos/WebsiteDtos.cs + Mappings.cs       sửa  (PublicationDto, PublishedSiteDto, WebsiteDto.CurrentPublicationId)
  Media/Dtos/MediaReferencesDto.cs                sửa  (enum + IsCurrent)
backend/src/Vsite.Infrastructure/
  Persistence/AppDbContext.cs                     sửa
  Persistence/Configurations/Website/SitePublicationConfiguration.cs    mới
  Persistence/Configurations/Website/WebsiteConfiguration.cs            sửa
  Persistence/Migrations/{ts}_AddSitePublication.cs                     mới
  Website/ConfigCatalogs.cs                       sửa  (đọc requiresProps)
  Website/WebsiteSchemaProvider.cs                sửa  (ValidateSiteSnapshot — chỉ dùng trong test/khởi động)
  Website/SitePointerCache.cs                     mới
  Website/SitePublicationReferenceSource.cs       mới
  Website/SeoImageReferenceSource.cs              mới
  DependencyInjection.cs                          sửa
backend/src/Vsite.Api/
  Website/WebsiteEndpoints.cs                     sửa  (3 route Owner)
  Website/SiteEndpoints.cs                        mới  (GET /api/site)
  Program.cs                                      sửa  (MapSiteEndpoints; artifact mới)
  Vsite.Api.csproj                                sửa  (EnsureWebsiteArtifacts + link site-snapshot.schema.json)

backend/tests/IntegrationTests/Website/
  SitePublicationDbConstraintTests.cs   SnapshotBuilderTests.cs   RequiredPropsCheckerTests.cs
  PublishWebsiteHandlerTests.cs         PublicationEndpointTests.cs
  PublishedSiteEndpointTests.cs         PublishedSiteTenantIsolationTests.cs
  PublicationReferenceSourceTests.cs    WebsiteHandlerKit.cs (sửa: thêm Publish/Rollback)
```

## 9. Session S0: codegen

### S0-T1: `requiresProps` vào `tree-rules.json`

`generateTreeRules` ([gen-registry.ts:490-499](../../../packages/builder-components/scripts/gen-registry.ts#L490-L499)) thêm, **bên cạnh** `variants` (giữ nguyên để không vỡ BE cũ):
`requiresProps: { [variantKey]: string[] }` (mảng đã sort). `schemaVersion` của file giữ 1 (thêm field, additive).

- [ ] Test trước: `packages/builder-components/tests/gen-registry.test.ts` — `tree-rules.json` sinh ra có
  `types.Hero.requiresProps.Hero01` chứa `title` và `image`, `Hero02` chỉ `title`; mọi key của `requiresProps` khớp `variants`.
- [ ] FAIL → hiện thực → PASS (`pnpm --filter @vsite/builder-components test`)
- [ ] `pnpm gen:registry` (check-additive phải xanh, `registry.lock.json` không đổi — `git status` xác nhận)
- [ ] Commit `feat(builder-components): xuất requiresProps vào tree-rules.json (WEBSITE-004)`

### S0-T2: Zod vỏ snapshot + `site-snapshot.schema.json`

Trong `packages/builder-components/meta/envelope-schema.ts` thêm `siteSnapshotSchema` (Zod) đúng shape §4 và
`buildSiteSnapshotJsonSchema()`; `tree` của từng trang tham chiếu vỏ tree sẵn có, `theme.tokens` tham chiếu vỏ
tokens. `additionalProperties: false` ở mọi cấp vỏ. Export schema + type `SiteSnapshot` từ `src/index.ts`.
`gen-registry.ts` ghi `generated/site-snapshot.schema.json` (cạnh dòng ghi `tree-envelope.schema.json`,
[gen-registry.ts:647](../../../packages/builder-components/scripts/gen-registry.ts#L647)).

- [ ] Test trước: `packages/builder-components/tests/site-snapshot-schema.test.ts` — ví dụ ở §4 parse được;
  thiếu `pages[].id` → lỗi; `slug: null` hợp lệ; key lạ ở gốc → lỗi; `navigation.items` rỗng hợp lệ.
- [ ] FAIL → hiện thực → PASS · `pnpm gen:registry` · `pnpm build`
- [ ] Commit `feat(builder-components): vỏ SiteSnapshot v1 bằng Zod + JSON Schema (#94, #97, WEBSITE-004)`

## 10. Session S1: Backend

Lệnh test chung: `dotnet test backend/vsite.sln --filter "FullyQualifiedName~{TênLớpTest}"`; cuối mỗi task chạy
thêm `dotnet test backend/tests/ArchitectureTests`.

### T1: Entity `SitePublication` + `Website.CurrentPublicationId` + migration

```csharp
// Vsite.Domain/Website/Entities/SitePublication.cs — bất biến: không setter, không soft delete
public sealed class SitePublication : TenantEntity
{
    public const int MaxNoteLength = 200;
    public static SitePublication Create(Guid shopId, Guid websiteId, int version, string snapshotJson,
        string? note, Guid? publishedByUserId, DateTimeOffset publishedAt, Guid? id = null);
    public Guid WebsiteId { get; }  public int Version { get; }  public string Snapshot { get; }   // jsonb
    public string? Note { get; }    public DateTimeOffset PublishedAt { get; }  public Guid? PublishedByUserId { get; }
}
// Website.cs
public Guid? CurrentPublicationId { get; private set; }
public void Publish(SitePublication p);   // trỏ current; Draft → Published; Suspended giữ nguyên; p.WebsiteId != Id → DomainException
public void PointTo(SitePublication p);   // rollback: chỉ đổi con trỏ; cùng kiểm WebsiteId
public void Suspend(); public void Reinstate();   // chưa có endpoint; test và admin sau này dùng
// PageDraft.cs
public void MarkPublished();  // IsDirty = false
public void MarkDirty();      // IsDirty = true
```

Bảng `site_publication`: `id` PK · `website_id` · `shop_id` · `version int` · `snapshot jsonb NOT NULL` ·
`note varchar(200)` · `published_at timestamptz` · `published_by_user_id uuid NULL` (FK `app_user`, ghim
`HasConstraintName`). Ràng buộc: `ux_site_publication_website_version (website_id, version)` · alternate key
`ak_site_publication_id_website_id (id, website_id)` · FK `(website_id, shop_id) → website (id, shop_id)` ·
`ck_site_publication_version_positive`. Trên `website`: cột `current_publication_id uuid NULL` + FK
`fk_website_current_publication (current_publication_id, id) → site_publication (id, website_id)` `Restrict`.

- [ ] Test trước: `backend/tests/IntegrationTests/Website/SitePublicationDbConstraintTests.cs`
  - trùng `(website_id, version)` → `23505`
  - `current_publication_id` trỏ bản của **website khác** → `23503` (R4)
  - `site_publication.shop_id` lệch `shop_id` của website → `23503`
  - xoá bản đang là current → bị chặn; bản không current → xoá được
  - Global Query Filter: tenant A không thấy `SitePublications` của B; tenant null → rỗng (thêm vào `WebsiteTenantIsolationTests.cs`)
  - `WebsiteEntityTests.cs`: `Publish` Draft→Published, Suspended giữ nguyên; `Publish`/`PointTo` với bản của website khác ném `DomainException`
- [ ] FAIL → hiện thực (entity, configuration, `DbSet` ở `AppDbContext` + `IAppDbContext`) → migration:
  `dotnet ef migrations add AddSitePublication --project backend/src/Vsite.Infrastructure --startup-project backend/src/Vsite.Api`
- [ ] `dotnet ef migrations has-pending-model-changes --project backend/src/Vsite.Infrastructure --startup-project backend/src/Vsite.Api` → "No changes"
- [ ] PASS · Commit `feat(website): entity SitePublication + CurrentPublicationId (#41, #92, WEBSITE-004)`

### T2: `SnapshotBuilder` + `DocumentKind.Snapshot`

`Vsite.Application/Website/Publishing/SnapshotBuilder.cs` — thuần, không chạm DB:

```csharp
public sealed record SnapshotPage(Page Page, string CleanTree);
public sealed record SnapshotMedia(Guid Id, string StorageKey, string? Preset);
public static string Build(WebsiteEntity website, Theme theme, int version,
    IReadOnlyList<SnapshotPage> pages, IReadOnlyList<SnapshotMedia> media);
```

Thêm `DocumentKind.Snapshot`, `DocumentSchemaVersions.Snapshot = 1`; `StoredDocuments.ReadCurrent` đọc được
snapshot (chưa có upcaster nào). `WebsiteSchemaProvider` nạp `site-snapshot.schema.json` (eager như các artifact
khác — [Program.cs:147-150](../../../backend/src/Vsite.Api/Program.cs#L147-L150)); `Vsite.Api.csproj` thêm file vào
`EnsureWebsiteArtifacts` và link ra thư mục `generated`.

- [ ] Test trước: `SnapshotBuilderTests.cs`
  - snapshot dựng từ website seed hợp lệ theo `site-snapshot.schema.json`
  - `pages` có `id`, xếp theo `SortOrder` rồi `Title`; `NotFound` có `slug: null`; trang chủ `slug: ""`
  - `navigation.items` rỗng; `version` đúng tham số; `theme.tokens` nguyên văn tokens đã lưu
  - `media` đúng tập truyền vào, key là GUID dạng `D` chữ thường
  - `SchemaVersioningTests.cs`: snapshot `schemaVersion` lớn hơn hiện hành → `Unsupported`
- [ ] FAIL → hiện thực → PASS · Commit `feat(website): SnapshotBuilder + schemaVersion cho Snapshot (#94, WEBSITE-004)`

### T3: `RequiredPropsChecker`

`TreeTypeRule` thêm `IReadOnlyDictionary<string, IReadOnlyList<string>> RequiresProps`; `TreeRulesCatalog` đọc field
mới ([ConfigCatalogs.cs:207-217](../../../backend/src/Vsite.Infrastructure/Website/ConfigCatalogs.cs#L207-L217)); thiếu field
trong file → lỗi khởi động nêu tên file (fail-fast như các catalog khác).

```csharp
// Vsite.Application/Website/Publishing/RequiredPropsChecker.cs
public sealed class RequiredPropsChecker(ITreeRulesCatalog rules)
{
    public IReadOnlyList<TreeValidationError> Check(string cleanTreeJson);  // code = REQUIRED_PROP_MISSING
}
```

Định nghĩa "thiếu": §4. Thêm hằng `TreeErrorCodes.RequiredPropMissing`. **Không** gọi checker trong `PUT draft`
(draft dở dang là hợp lệ).

- [ ] Test trước: `RequiredPropsCheckerTests.cs` — Hero01 thiếu `image` → 1 lỗi path `/children/0/props/image`;
  Hero02 không cần `image`; `title` chỉ có khoảng trắng → lỗi; richText `<p></p>` → lỗi; Gallery `items: []` → lỗi;
  node lồng trong Section báo đúng path; tree rỗng → không lỗi; cả 3 tree seed của `WebsiteSeeder` → không lỗi (R8);
  `SavePageDraftHandlerTests`: lưu draft thiếu prop bắt buộc **vẫn 200**.
  `WebsiteConfigCatalogTests.cs`: file thiếu `requiresProps` → ném lỗi nêu tên file.
- [ ] FAIL → hiện thực → PASS · Commit `feat(website): kiểm requiresProps cho publish (WEBSITE-004)`

### T4: Lệnh publish + endpoint

`PublishWebsiteCommand(string? Note) : IRequest<PublicationDto>`; validator: `Note` ≤ 200.
`PublicationDto(Guid Id, int Version, DateTimeOffset PublishedAt, Guid? PublishedByUserId, string? Note, bool IsCurrent)`.
`WebsiteDto` thêm `Guid? CurrentPublicationId` (additive).

Handler (một `SaveChanges`):
1. `shopId = tenant.RequireShopId()`; nạp `Website` (tracking), `Theme`, các `Page` có `IsEnabled` + `PageDraft` (tracking) — mọi predicate có `ShopId == shopId` tường minh.
2. Mỗi trang: `validator.Validate(draft.Tree, page.Kind)` → lỗi thì gom; hợp lệ thì `RequiredPropsChecker.Check(CleanTree)`;
   rồi `media.EnsureValidAsync(imageIds của trang, [page.OgImageId nếu có], ct)` — bắt `MEDIA_INVALID_IMAGE_REFERENCE` thành một lỗi của trang đó (`path = ""`).
   Ảnh OG mặc định + favicon của website: một lời gọi `EnsureValidAsync([], ids)`; lỗi gắn `pageId = null`.
3. Có lỗi → `PublishBlockedException` (`422 PUBLISH_BLOCKED`, `params: { errors[], truncated }`).
4. Nạp `storageKey`/`preset` của mọi id ảnh: `db.MediaAssets.Where(a => a.ShopId == shopId && ids.Contains(a.Id))`.
5. `version = (max Version của website ?? 0) + 1` → `SnapshotBuilder.Build` → `SitePublication.Create` (người publish từ `IAuditActor.UserId`) → `website.Publish(p)` → `draft.MarkPublished()` **chỉ** cho draft đang `IsDirty`.
6. `SaveChanges`: `23505` trên `ux_site_publication_website_version` hoặc `DbUpdateConcurrencyException` → `ConflictException("PUBLISH_CONFLICT")`.
7. Sau khi lưu: `ISitePointerCache.InvalidateAsync(shopId)` (interface tạo ở task này, bản thật ở T6; T4 dùng bản no-op tạm đăng ký trong DI và được T6 thay). Log `snapshotBytes`.

Route `POST /shops/{shopId}/website/publications` · `publishWebsite` · body `{ note? }` (tuỳ chọn) · `201` + `Location` ·
`401 403 404 409 422`.

- [ ] Test trước: `PublishWebsiteHandlerTests.cs` (handler, Postgres thật qua `WebsiteHandlerKit`)
  - website trắng publish được ngay: version 1, `Status = Published`, `CurrentPublicationId` trỏ đúng (R8)
  - publish lần hai → version 2; bản 1 còn nguyên, `Snapshot` không đổi
  - sau publish mọi draft `IsDirty = false`; draft vốn sạch giữ nguyên `Version` (R9)
  - trang bật có Hero01 thiếu ảnh → `PUBLISH_BLOCKED` kèm `pageId`, `pageTitle`, `path`, `REQUIRED_PROP_MISSING`; không dòng nào được ghi
  - hai trang lỗi → lỗi của cả hai trang trong một response
  - ảnh trong tree đã xoá mềm → `PUBLISH_BLOCKED` với `MEDIA_INVALID_IMAGE_REFERENCE`
  - trang lỗi nhưng đang tắt → publish được, snapshot không có trang đó (R7)
  - website `Suspended` → có bản mới, `Status` vẫn `Suspended` (G6)
  - draft bị đổi giữa đọc và ghi (dùng `BumpXminInterceptor` sẵn có ở `backend/tests/IntegrationTests/Website/WebsiteHandlerKit.cs:219`) → `PUBLISH_CONFLICT`, không có bản mới (R10)
  - tenant null → `TENANT_REQUIRED`; tenant shop B không publish được website shop A (`WEBSITE_NOT_FOUND`)
  - `snapshot.media` chứa đủ id ảnh của tree + OG
- [ ] Test trước: `PublicationEndpointTests.cs` (HTTP, `ShopApiHarness`) — 201 + `Location`; không token 401; `Customer`/Owner shop khác 403; `note` 201 ký tự → 422; `GET website` trả `currentPublicationId`; `GET pages` trả `isDirty = false`
- [ ] FAIL → hiện thực → PASS · `ShopScopedRouteFilterTests` xanh
- [ ] Commit `feat(website): publish toàn site thành SitePublication (#41, WEBSITE-004)`

### T5: Danh sách bản + rollback

`ListPublicationsQuery : IRequest<IReadOnlyList<PublicationDto>>` — `Take(50)`, `OrderByDescending(Version)`, **không** select cột `Snapshot`.
`RollbackPublicationCommand(Guid PublicationId) : IRequest<PublicationDto>` — tìm bản bằng
`db.SitePublications.FirstOrDefaultAsync(p => p.Id == id && p.ShopId == shopId && p.WebsiteId == website.Id)`;
không thấy → `NotFoundException` mã `PUBLICATION_NOT_FOUND`; đã là current → trả luôn; còn lại `website.PointTo(p)`,
mọi draft của website `MarkDirty()`, `SaveChanges`, `InvalidateAsync`.

Route: `GET …/publications` (`listPublications`, `200 401 403 404`) ·
`POST …/publications/{publicationId:guid}/rollback` (`rollbackPublication`, `200 401 403 404`).

- [ ] Test trước (thêm vào `PublishWebsiteHandlerTests.cs` + `PublicationEndpointTests.cs`)
  - publish 3 lần → danh sách 3 mục giảm dần, đúng một `isCurrent`
  - rollback về bản 1 → `CurrentPublicationId` = bản 1; nội dung mọi `PageDraft.Tree` không đổi; mọi draft `IsDirty = true` (G7); không sinh bản mới
  - rollback bản đang chạy → 200, `UpdatedAt` của website không đổi
  - rollback `publicationId` của shop B qua route shop A → `404 PUBLICATION_NOT_FOUND`, con trỏ không đổi (R4)
  - id ngẫu nhiên → 404; `Customer` → 403; chưa có website → `404 WEBSITE_NOT_FOUND`
  - publish sau rollback → version = max + 1 (không tái dùng số)
- [ ] FAIL → hiện thực → PASS · Commit `feat(website): danh sách bản publish + rollback (WEBSITE-004)`

### T6: Endpoint công khai `GET /api/site` + con trỏ Redis

```csharp
// Vsite.Application/Website/Interfaces/ISitePointerCache.cs
public sealed record SitePointer(WebsiteStatus? WebsiteStatus, Guid? PublicationId);   // WebsiteStatus null = shop chưa có website
public interface ISitePointerCache
{
    Task<SitePointer?> GetAsync(Guid shopId, CancellationToken ct);
    Task SetAsync(Guid shopId, SitePointer pointer, CancellationToken ct);   // có website: 5 phút; chưa có: 1 phút
    Task InvalidateAsync(Guid shopId, CancellationToken ct);
}
```

`SitePointerCache` (Infrastructure/Website) trên `IDistributedCache`, key `site:pointer:{shopId:N}`, cùng khuôn
[ShopLookupService.cs:29-56](../../../backend/src/Vsite.Infrastructure/Shop/ShopLookupService.cs#L29-L56).

`GetPublishedSiteQuery(string? IfNoneMatch) : IRequest<PublishedSiteResult>` với
`PublishedSiteResult(PublishedSiteDto? Body, string? ETag, bool NotModified)`. Handler:
1. `tenant.AudienceKind != Shop` hoặc `tenant.ShopId` null → `NotFoundException` mã `SITE_NOT_FOUND`.
2. Đọc `Shop` (`Name`, `Status`, `LogoId`, `UpdatedAt`) theo id. Không thấy / `Closed` → 404.
3. Con trỏ: cache → hụt thì `db.Websites.Where(w => w.ShopId == shopId).Select(Status, CurrentPublicationId)` rồi `SetAsync`.
4. Chưa có website / `Draft` / `CurrentPublicationId` null → 404 (R6).
5. Website `Suspended` hoặc shop `Suspended` → body `{ status: Suspended, shop, publication: null }`, không ETag.
6. ETag theo §4; khớp `IfNoneMatch` (so cả dạng có `W/` phía trước và danh sách phân tách dấu phẩy) → `NotModified`.
7. Đọc `db.SitePublications.FirstOrDefaultAsync(p => p.Id == pointer.PublicationId && p.ShopId == shopId)`; null
   (cache cũ sau khi bản bị xoá) → `InvalidateAsync` rồi đọc lại DB đúng một lần; vẫn null → 404.
8. `snapshot` qua `StoredDocuments.ReadCurrent(DocumentKind.Snapshot, …)`; `logoUrl` qua `IShopLogoReader.GetLogoUrlAsync`.

`Vsite.Api/Website/SiteEndpoints.cs`: `app.MapGroup("/site").WithTags("Site").WithGroupName("website")`,
`MapGet("")` · `.AllowAnonymous()` · `getPublishedSite` · `Produces<PublishedSiteDto>()` · `Produces(304)` ·
`ProducesProblem(404)`. Đặt header `ETag`, `Cache-Control: no-cache`. Đăng ký `api.MapSiteEndpoints()` cạnh
[Program.cs:198](../../../backend/src/Vsite.Api/Program.cs#L198).

- [ ] Test trước: `PublishedSiteEndpointTests.cs` (HTTP, đặt `request.Headers.Host = "{slug}.vsite.local"`)
  - đã publish → 200, `status = Published`, `publication.version` đúng, `shop.name` đúng, có `ETag`, `Cache-Control: no-cache`
  - gửi lại với `If-None-Match` = ETag → 304 không body; ETag khác → 200
  - host `admin.vsite.local`, host chính, slug không tồn tại → `404 SITE_NOT_FOUND` (R2)
  - website `Draft` chưa publish → 404; website `Draft` nhưng chèn tay một `site_publication` → 404 (R6)
  - website `Suspended` → 200 `publication: null`; shop `Suspended` → như trên; shop `Closed` → 404; shop `Draft` + website `Published` → 200
  - publish lần hai → request kế thấy version 2 ngay, không chờ TTL (R5); rollback → thấy version 1 ngay
  - đổi tên shop qua `PUT /api/shops/{id}` → ETag đổi, `shop.name` mới, `snapshot` y nguyên (R12)
  - gọi không token → 200 (không 401); kèm token bất kỳ → kết quả như nhau
  - `ApiRoutePrefixTests` xanh (có `operationId`, dưới `/api`)
- [ ] FAIL → hiện thực → PASS · Commit `feat(website): endpoint công khai GET /api/site theo Host + ETag (#108, #109, WEBSITE-004)`

### T7: Tenant isolation theo Host + tính bất biến

Chỉ thêm test (code đã có từ T1–T6); test đỏ thì quay lại sửa task tương ứng.

- [ ] `PublishedSiteTenantIsolationTests.cs`
  - shop A và B đều publish, nội dung khác nhau: Host A → bản A, Host B → bản B
  - Host A kèm `?shopId={B}`, `?host={slugB}.vsite.local`, header `X-Forwarded-Host: {slugB}…` → vẫn bản A (R1)
  - Host A kèm token Owner của shop B → vẫn bản A (R1)
  - chỉ B publish: Host A → 404 dù B có bản
  - con trỏ Redis của A không bao giờ trả `publicationId` của B (publish B rồi đọc A)
- [ ] Bất biến (R3): publish → ghi lại chuỗi `snapshot` + ETag → lần lượt `PUT draft`, `PUT theme`, `PUT page` (đổi
  SEO, tắt trang), `DELETE page` → `GET /api/site` trả `snapshot` giống hệt từng ký tự, ETag giữ nguyên
- [ ] Xoá mềm ảnh sau publish → `snapshot.media` vẫn có ảnh đó (R11)
- [ ] Kiểm kiến trúc: thêm test trong `backend/tests/ArchitectureTests` quét nguồn
  `Vsite.Application/Website/Queries/GetPublishedSite/*.cs` — không chứa `PageDrafts`, `Pages`, `Themes`
- [ ] PASS · Commit `test(website): tenant isolation theo Host + snapshot bất biến (#21, #108, WEBSITE-004)`

### T8: `references` — snapshot + ảnh OG/favicon

`MediaReferenceKind` thêm `SitePublication`, `PageOgImage`, `WebsiteOgImage`, `WebsiteFavicon`;
`MediaReferenceDto` thêm `bool? IsCurrent = null` (additive; chỉ có với `SitePublication`).

- `SitePublicationReferenceSource`: lấy id các clone có `SourceAssetId == assetId` (như
  [PageDraftReferenceSource.cs:27-32](../../../backend/src/Vsite.Infrastructure/Website/PageDraftReferenceSource.cs#L27-L32)) **cộng chính `assetId`** (ảnh OG là bản
  Library) → duyệt các bản của shop (`ShopId == shopId` tường minh), đọc key của `snapshot.media` → có bản nào
  chứa thì trả **một** mục, `IsCurrent` = bản đang chạy có chứa hay không.
- `SeoImageReferenceSource`: `Pages.Where(ShopId && OgImageId == assetId)` → `PageOgImage` mỗi trang một mục;
  `Websites` → `WebsiteOgImage` / `WebsiteFavicon`.
- Đăng ký cả hai cạnh [DependencyInjection.cs:122](../../../backend/src/Vsite.Infrastructure/DependencyInjection.cs#L122). Xoá hai dòng TODO ở
  `MediaReferencesDto.cs:8-9` và `GetReferencesHandler.cs:12`.

- [ ] Test trước: `PublicationReferenceSourceTests.cs`
  - ảnh dùng trong bản đang chạy → một mục `SitePublication`, `isCurrent = true`
  - ảnh chỉ còn ở bản cũ (đã gỡ khỏi draft, publish lại) → `isCurrent = false`
  - ảnh ở 3 bản → vẫn một mục
  - ảnh OG của trang → `PageOgImage` + `pageId`/`pageTitle`; OG website / favicon → đúng kind
  - shop B không thấy tham chiếu của shop A; tenant null → rỗng
  - ảnh không dùng ở đâu → rỗng
- [ ] FAIL → hiện thực → PASS · Commit `feat(media): references quét snapshot và ảnh OG/favicon (WEBSITE-004)`

### T9: Kết thúc S1

- [ ] `dotnet test backend/vsite.sln` xanh toàn bộ (Docker thật); `pnpm build && pnpm test` xanh
- [ ] `pnpm contract:export website media` → skill `contract-sync` → `Docs/tasks/WEBSITE-004/contract-diff.md` → **Gate 1** (#89).
  Dự kiến chỉ thêm: 4 operation + 3 schema ở `website`; 4 giá trị enum + 1 field ở `media`; `WebsiteDto.currentPublicationId`.
  Lưu ý nợ classifier (TOOLING-001): thêm field vào DTO response có thể bị báo BREAKING nhầm — nêu rõ trong `contract-diff.md`.
- [ ] Sau promote: `pnpm gen:api` · viết `Docs/tasks/WEBSITE-004/brief.md` cho 8c + màn publish Portal, bắt buộc ghi:
  header `Host` (#108), `ETag`/304, `Suspended` → `publication: null`, một mã 404, `snapshot` là `unknown` → parse bằng
  Zod `siteSnapshotSchema`, `snapshot.media` thay cho `mediaMap`, version draft đổi sau publish (R9), shape `PUBLISH_BLOCKED`.
- [ ] `Docs/tasks/WEBSITE-004/changelog.md`: lệch có chủ đích (shape snapshot khác `05` §7: `id`, `media`, `tracking`,
  `theme.presetKey`, `seo.faviconAssetId`; shop `Draft` không chặn site) + "Chưa làm xong / theo dõi" (phát hiện 4;
  anchor `#nodeId` + năm © → 8c; prune → 8d; custom domain; kiểm `Host` tự đặt từ Node → 8c)
- [ ] Banner `STATUS` của `05` (Tasks + Changelog + Stale: §7 shape snapshot, §25 mục 4 đã chốt #109) và `08` (references
  đã quét snapshot) → `pnpm gen:doc-index`
- [ ] `DesignIdeal/00-INDEX.md`: §1 dòng "Quyết định #N nằm ở đâu" thêm #108, #109; §3 dòng 8b; §4 gỡ hai dòng "Cần bạn chốt"
  của 8b, gỡ nợ `MEDIA-001` #2, `WEBSITE-001` #3 và #14, cập nhật dòng "Review kiến trúc P1"
- [ ] `DesignIdeal/DECISIONS.md`: #108, #109 đổi trạng thái sang ✅ kèm tên test; #94 ghi `Snapshot` đã code
- [ ] `backend/docs/modules/website.md`: entity, 4 route, quy ước đọc công khai, con trỏ Redis, sửa dòng 83
- [ ] `pnpm check:docs` xanh

## 11. Đóng task (trước Gate 2)

- [ ] `change-reviewer` trên toàn diff (model mạnh nhất — task chạm tenant + contract)
- [ ] `Docs/tasks/WEBSITE-004/review.md` kèm mục "Ma sát quy trình"
- [ ] Nghiệm thu tay tối thiểu (không có FE): bằng `curl` với header `Host: {slug}.vsite.local` qua BE dev —
  publish, đọc, sửa draft, đọc lại, rollback, đọc lại

## 12. Giả định tôi đã tự đặt (chưa hỏi, cần duyệt)

| # | Giả định | Vì sao | Nếu sai thì |
|---|---|---|---|
| G2 | Custom domain: 8b chỉ chốt *cách chuyển host* (header `Host`); tra custom domain → shop vẫn chờ bảng `ShopDomain` | [TenantResolutionMiddleware.cs:18-21](../../../backend/src/Vsite.Api/Tenancy/TenantResolutionMiddleware.cs#L18-L21) | Phạm vi phình thêm entity `ShopDomain` |
| G3 | Anchor `#nodeId` và năm © (WEBSITE-003 changelog #3, #4) chuyển sang 8c; BE không trả `year` | `SiteLayout` nhận `year` từ bên gọi; anchor là việc của renderer | Thêm field vào response công khai (vòng `-D1`) |
| G4 | Một mã `404 SITE_NOT_FOUND` cho mọi trường hợp "không có site" | Không lộ shop nào đang dựng site; 8c chỉ cần một trang 404 | Tách mã (additive) |
| G5 | Publish luôn được phép dù không draft nào `IsDirty` | `Theme` và SEO không có cờ dirty | Thêm `409 NOTHING_TO_PUBLISH` |
| G6 | Publish khi website `Suspended` vẫn ghi bản mới nhưng giữ `Suspended` | Đình chỉ là việc của admin, publish không được tự gỡ | Chặn publish bằng 409 |
| G7 | Sau rollback, mọi draft `IsDirty = true` | `IsDirty` nghĩa là "khác bản đang chạy" ([PageDraft.cs:40](../../../backend/src/Vsite.Domain/Website/Entities/PageDraft.cs#L40)) | Giữ nguyên cờ |
| G8 | Publish đồng thời / draft đổi giữa chừng → `409 PUBLISH_CONFLICT`, không tự thử lại | Hiếm; một Owner một shop | Thêm vòng thử lại |
| G9 | Ảnh OG/favicon trong `snapshot.media` trỏ file **gốc** của bản Library (`preset: null`), chưa phải phái sinh `1200x630,cover` | Chưa xác minh ai sinh phái sinh OG; 8c vẫn dùng được file gốc cho `og:image` | Thêm bước sinh/tra phái sinh lúc publish |
| G10 | Trang chủ đang tắt vẫn publish được (site không có trang chủ) | Spec im lặng; chặn thì thêm một luật nghiệp vụ chưa ai yêu cầu | Thêm lỗi `HOME_PAGE_DISABLED` vào `PUBLISH_BLOCKED` |
| G11 | Danh sách bản publish trần 50, không phân trang | 8d sẽ giữ 20 bản | Dùng shape pagination chung |
| G12 | Lỗi ảnh trong publish báo theo **trang**, không chỉ ra node nào | `IMediaReferenceValidator` ném một lỗi chung, không trả id sai | Mở rộng contract nội bộ của Media |

## 13. Verification tổng

```bash
pnpm install && pnpm gen:registry                      # artifact có requiresProps + site-snapshot.schema.json
pnpm build && pnpm test
dotnet test backend/tests/ArchitectureTests            # ranh giới module, allowlist IgnoreQueryFilters không đổi
dotnet test backend/vsite.sln                          # Docker thật
dotnet ef migrations has-pending-model-changes --project backend/src/Vsite.Infrastructure --startup-project backend/src/Vsite.Api
pnpm check:docs
```

Tiêu chí đo được (khớp `step.md` §8b "Xong khi"):
1. Publish sinh một dòng `site_publication`; không endpoint nào sửa được cột `snapshot` — T4.
2. Sửa draft/theme/trang sau publish: `GET /api/site` trả `snapshot` giống từng ký tự — T7.
3. Rollback là một request, không sinh bản mới — T5.
4. Host shop A không đọc được bản shop B bằng bất kỳ tham số/token nào — T7.
5. Website `Draft` → 404; `Suspended` → 200 kèm trạng thái — T6.
6. `IgnoreQueryFiltersAllowlistTests` không đổi một dòng nào.
