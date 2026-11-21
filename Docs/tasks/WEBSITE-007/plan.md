# WEBSITE-007 — `NavigationConfig`: menu website shop (Bước 7)

> **Đã chốt 2026-11-20 — chưa code**
>
> **Lane C.** Entity + bảng mới, endpoint mới (chạm contract `website.v1`), đổi shape snapshot
> (`navigation.items`), màn hình Portal mới, đổi `SiteLayout` của renderer.
> Cổng: Gate 1 → Gate 2. Tuần tự S1 (BE) → S2 (FE).
>
> **Người thực thi dùng skill:** `superpowers:subagent-driven-development` hoặc
> `superpowers:executing-plans`. Mỗi bước có checkbox `- [ ]`.

**Mục tiêu:** Owner sắp menu website shop trên **một** màn kéo-thả ở Portal (trang của site + link tự
do, menu lồng tối đa 3 cấp), publish, và site công khai hiện đúng menu đó trên `Header`.
**Nhu cầu dữ liệu:** [`data-needs.md`](data-needs.md) (chưa viết) · **Spec:** `DesignIdeal/05` §8, §7
· `step.md` mục 7 · **Quyết định liên quan:** #34, #94, #98, #103, #108, #11, #23

## 1. Yêu cầu (đã chốt với người 2026-11-20)

- **Người dùng / vai trò:** Owner của shop (Portal). Khách xem Shop Site (công khai, không đăng nhập).
- **Điểm chạm:** màn "Menu" ở Portal (khu website) · API đọc/ghi menu (Owner-only) · publish (snapshot
  mang menu) · `Header` trong `SiteLayout` ở `apps/web` và preview Portal.
- **Nguồn mục menu:** `page:{pageId}` (nhãn mặc định = tiêu đề trang, `label` ghi đè được) và `custom`
  (nhãn + URL tự do).
- **Menu con:** **có, tối đa 3 cấp** (cấp 1 → con → cháu; người chốt 2026-11-21, thay cho "một cấp" — lý do ở §4) — dropdown lồng trên site.
- **Menu mặc định:** chưa từng sắp → tự sinh từ các trang đang bật (trừ `NotFound`), hiện hết theo thứ
  tự trang. Đã có menu → trang tạo **sau đó** được thêm vào **cuối, ẩn** (`hidden: true`); Owner tự bật
  (người chốt — đúng `05` §8).
- **Publish:** sửa menu **phải publish** mới thấy trên site; rollback trả cả menu cũ (người chốt —
  đúng `05` §8 "menu nằm trong snapshot").
- **Xong nghĩa là (step.md 7):** sắp menu ở Portal, publish, site công khai hiện đúng menu; mục trỏ tới
  trang đã xoá/tắt không hiện ra.
- **Không làm trong task này:** nguồn `serviceGroup:{id}` (Bước 9) · `sysProductListing` /
  `productCategory:{id}` (10c) · job dọn mục mồ côi (8d — trong lúc chờ, renderer bỏ qua) · nhãn đa ngôn
  ngữ `labels` (#98, bước L) · menu footer · menu sâu hơn 3 cấp.

## 2. Hiện trạng (khảo sát 2026-11-20)

| Khả năng | Trạng thái | Ở đâu | Ghi chú |
|---|---|---|---|
| Entity / bảng `NavigationConfig` | ❌ chưa có | — | `Website` không có field menu — [Website.cs:50-75](../../../backend/src/Vsite.Domain/Website/Entities/Website.cs#L50-L75); DbContext chỉ có Websites/Themes/Pages/PageDrafts/SitePublications — [AppDbContext.cs:51-55](../../../backend/src/Vsite.Infrastructure/Persistence/AppDbContext.cs#L51-L55) |
| Trang có cờ bật/tắt, xoá mềm | ✅ có | [Page.cs:111](../../../backend/src/Vsite.Domain/Website/Entities/Page.cs#L111) `IsEnabled` ("tắt = không render, không lên menu"); xoá mềm [Page.cs:181-189](../../../backend/src/Vsite.Domain/Website/Entities/Page.cs#L181-L189) | `SortOrder` là thứ tự quản lý, **không** phải thứ tự menu — [Page.cs:114](../../../backend/src/Vsite.Domain/Website/Entities/Page.cs#L114). Trang chủ = Composable + slug rỗng — [Page.cs:117](../../../backend/src/Vsite.Domain/Website/Entities/Page.cs#L117) |
| Snapshot có chỗ cho menu | 🟡 một phần | [SnapshotBuilder.cs:83](../../../backend/src/Vsite.Application/Website/Publishing/SnapshotBuilder.cs#L83) | Ghi cứng `navigation: { items: [] }` |
| Publish chỉ lấy trang đang bật | ✅ có | [PublishWebsiteHandler.cs:51-54](../../../backend/src/Vsite.Application/Website/Commands/PublishWebsite/PublishWebsiteHandler.cs#L51-L54) | Trang tắt/xoá không vào `snapshot.pages` → menu trỏ tới nó tự thành mồ côi |
| Shape `navigation` ở Zod + JSON Schema | 🟡 một phần | [envelope-schema.ts:215](../../../packages/builder-components/meta/envelope-schema.ts#L215) (Zod), :262 (JSON Schema) | `items: z.array(z.unknown())` — chưa có shape mục |
| `GET /api/site` | ✅ có | [GetPublishedSiteHandler.cs](../../../backend/src/Vsite.Application/Website/Queries/GetPublishedSite/GetPublishedSiteHandler.cs) | Trả snapshot nguyên văn; **cấm** đụng Pages/Drafts/Themes (`PublishedSiteBoundaryTests`) → menu phải nằm sẵn trong snapshot |
| Dấu "có thay đổi chưa publish" | 🟡 chỉ theo trang | [PageDraft.cs:41](../../../backend/src/Vsite.Domain/Website/Entities/PageDraft.cs#L41) | Không có dirty cấp website; Portal chỉ hiện ở danh sách trang — [page-list.tsx:129](../../../apps/portal/src/components/website/page-list.tsx#L129) |
| `Header` hiện menu | 🟡 một phần | [site-layout.tsx:12-41](../../../packages/builder-renderer/src/site-layout.tsx#L12-L41) | Prop `menuItems?: {label, href}[]`, render `<a>` phẳng; không menu con, không mobile |
| Ai truyền menu vào `SiteLayout` | ❌ chưa | [site-view.tsx:23-25](../../../apps/web/src/components/site-view.tsx#L23-L25) (web), [site-preview.tsx:164-167](../../../apps/portal/src/components/website/site-preview.tsx#L164-L167) (Portal) | Cả hai không đọc `snapshot.navigation` |
| Dựng URL trang | ✅ có | [site-url.ts:44-65](../../../packages/builder-components/src/site-url.ts#L44-L65) `createResolveUrl` | Một nguồn URL duy nhất (#103) — menu phải dùng lại |
| Endpoint menu | ❌ chưa | Nhóm route website — [WebsiteEndpoints.cs:48](../../../backend/src/Vsite.Api/Website/WebsiteEndpoints.cs#L48) | Mọi route `RequireGlobalScope` + `RequireOwner()` — [WebsiteEndpoints.cs:228-229](../../../backend/src/Vsite.Api/Website/WebsiteEndpoints.cs#L228-L229) |
| Màn menu ở Portal | ❌ chưa | Trang tổng quan website — [website-overview.tsx:49-64](../../../apps/portal/src/components/website/website-overview.tsx#L49-L64) | Chỗ tự nhiên để thêm khu "Menu" |
| Kéo-thả sắp xếp | 🟡 một phần | `@dnd-kit/core` — [apps/portal/package.json:15](../../../apps/portal/package.json#L15) | **Chưa có `@dnd-kit/sortable`** |
| Job dọn mục mồ côi | ❌ (đúng kế hoạch) | — | Không có Hangfire; để 8d |

### Phát hiện khi khảo sát

1. **Không có dirty cấp website.** Sửa theme cũng không đánh dấu gì — [UpdateThemeHandler.cs:16-33](../../../backend/src/Vsite.Application/Website/Commands/UpdateTheme/UpdateThemeHandler.cs#L16-L33).
   Menu sẽ gặp đúng vấn đề này: Owner sửa menu xong không có gì nhắc "chưa publish".
2. **`GET /api/site` không được đọc bảng nào khác ngoài publication** — nên menu phải được chép vào
   snapshot lúc publish, không resolve lúc đọc.
3. **`snapshot.pages` đã có `id`, `slug`, `title`, `systemType`** — [SnapshotBuilder.cs](../../../backend/src/Vsite.Application/Website/Publishing/SnapshotBuilder.cs) (khoảng :42-60) —
   đủ để FE tự gắn nhãn + URL cho mục `page:{id}` mà không cần BE resolve sẵn.
4. `apps/web` chưa có upcaster snapshot, chỉ nhận `schemaVersion` 1 —
   [site-client.ts:65](../../../apps/web/src/server/site-client.ts#L65). Thêm field vào mục `navigation`
   (vốn rỗng) là **additive**, không cần lên `schemaVersion` 2 — xem §4.

## 3. Flow

Ký hiệu: ✅ có sẵn · 🟡 một phần · ❌ cần làm.

### Flow 1: Owner mở màn Menu lần đầu
1. Vào khu website ở Portal ✅ [website-overview.tsx:49-64](../../../apps/portal/src/components/website/website-overview.tsx#L49-L64) — ❌ thêm lối vào "Menu"
2. `GET …/website/navigation` ❌ — chưa có dòng `navigation_config` ⇒ BE trả menu **tự sinh**: mọi trang
   chưa xoá, trừ `NotFound`, theo `SortOrder` rồi `Title` (cùng thứ tự với [SnapshotBuilder.cs:42-44](../../../backend/src/Vsite.Application/Website/Publishing/SnapshotBuilder.cs#L42-L44)),
   tất cả `hidden: false`; trang đang tắt vẫn có mặt (render sẽ bỏ qua) để bật lại trang thì menu có ngay.
   `version: null`, `isDirty: true`.
3. Portal ghép nhãn từ danh sách trang (`listPages` có sẵn) ✅ để hiện "Trang chủ", "Liên hệ"…

### Flow 2: Owner sắp / đổi nhãn / ẩn / thêm link / lồng (tối đa 3 cấp) rồi lưu
1. Kéo-thả ❌ (cần `@dnd-kit/sortable`) · đổi nhãn (rỗng = dùng tên trang) · ẩn/hiện · thêm/xoá link `custom`
2. `PUT …/website/navigation` `{ version, items }` ❌
   - `version` lệch ⇒ `409 NAVIGATION_CONFLICT` (khuôn `DRAFT_CONFLICT`), Portal báo "menu vừa được sửa ở nơi khác, tải lại".
   - Không hợp lệ ⇒ `422` lỗi theo mục ở `params.errors[]` `{path, code, message}`: trang
     không tồn tại/đã xoá, trùng một trang hai lần, URL `custom` không an toàn (luật
     [LinkUrlRules.cs](../../../backend/src/Vsite.Application/Website/Validation/LinkUrlRules.cs) — http(s)/mailto/tel),
     `custom` thiếu nhãn, nhãn quá dài, quá số mục.
3. Lưu thành công ⇒ `isDirty = true`, trả `version` mới.

### Flow 3: Owner tạo trang mới sau khi đã có menu
1. Tạo trang ✅ (luồng hiện có — **không sửa**)
2. Lần đọc menu kế tiếp, BE ghép: trang chưa có trong menu ⇒ thêm vào **cuối, `hidden: true`**; mục trỏ
   trang **đã xoá** ⇒ bỏ khỏi kết quả đọc (lưu lần sau là sạch). Ghép lúc đọc, không ghi chéo từ
   `CreatePage`/`DeletePage`.

### Flow 4: Publish
1. Nút publish ✅ (WEBSITE-006)
2. `PublishWebsiteHandler` ❌ đọc menu đã ghép (Flow 3), chép **chỉ mục đang hiện** vào
   `snapshot.navigation.items` thay cho mảng rỗng ở [SnapshotBuilder.cs:83](../../../backend/src/Vsite.Application/Website/Publishing/SnapshotBuilder.cs#L83);
   chưa có dòng menu ⇒ ghi luôn dòng từ menu tự sinh. Mục trỏ trang **không vào snapshot** (đang tắt)
   cũng bị bỏ, cha bị bỏ ⇒ bỏ cả nhóm — snapshot công khai không mang id trang tắt; FE vẫn tự phòng thủ
   (Flow 5) cho preview và snapshot cũ. Hạ `isDirty`. Cùng một `SaveChanges`.
3. Menu **không** chặn publish: mục mồ côi chỉ bị bỏ qua, không phải lỗi `PUBLISH_BLOCKED`.

### Flow 5: Khách xem site
1. `GET /api/site` ✅ trả snapshot nguyên văn (không đổi handler)
2. `apps/web` ❌ gọi hàm dùng chung `resolveNavigation(items, snapshot.pages, resolveUrl)`:
   `page:{id}` ⇒ nhãn = `label ?? page.title`, URL qua `createResolveUrl` (#103); trang không có trong
   `snapshot.pages` (tắt/xoá) ⇒ bỏ mục; **mục cha mồ côi ⇒ bỏ cả nhóm con**.
3. `SiteLayout` ❌ hiện menu con dạng dropdown, có menu mobile; isomorphic, không JS (#23); link
   ngoài site mở tab mới `rel="noopener noreferrer"`.
4. Preview Portal ❌ dùng cùng hàm, từ menu đang sửa + danh sách trang.

### Flow 6: Rollback
1. Rollback ✅ chỉ đổi con trỏ ([RollbackPublicationHandler.cs:43-55](../../../backend/src/Vsite.Application/Website/Commands/RollbackPublication/RollbackPublicationHandler.cs#L43-L55))
   ⇒ site hiện menu của bản cũ (nằm trong snapshot).
2. ❌ Menu đang sửa giữ nguyên, bật `isDirty = true` — đúng cách trang đang làm (#112).

## 4. Đã chốt trong session lập plan

| # | Chốt | Ai chốt |
|---|---|---|
| — | Menu lồng **tối đa 3 cấp**, làm ngay trong task này (2026-11-21, thay cho "một cấp"). Contract diễn đạt độ sâu bằng **ba kiểu DTO tường minh** (cấp 1 có `children` cấp 2, cấp 2 có `children` cấp 3, cấp 3 không có `children` và từ chối field lạ ⇒ 400) — vì OpenAPI .NET 9 sinh `items: {}` cho DTO đệ quy (Orval ra `unknown[]`). `NAV_TOO_DEEP` chỉ còn ở `Validate` (phòng dữ liệu lưu hỏng) | người |
| — | Chưa có menu ⇒ tự sinh từ trang; trang mới ⇒ cuối, ẩn (`05` §8) | người |
| — | Sửa menu phải publish; rollback trả menu cũ (`05` §8) | người |
| #113 | Snapshot lưu menu dạng **khoá** (`page:{id}` / `custom` + `label?` + `url?` + `children?`), chỉ mục đang hiện; nhãn và URL do **FE ghép** từ `snapshot.pages` bằng một hàm dùng chung, qua `createResolveUrl` (#103) — BE không dựng URL. Menu ghép lúc đọc (tự sinh / thêm trang mới ẩn / bỏ trang đã xoá) | người |
| — | Menu có cờ `IsDirty` riêng (bật khi lưu và khi rollback, hạ khi publish); dấu "chưa publish" cấp website (gồm theme) **ngoài phạm vi** | người |
| — | `custom` chỉ nhận http(s), `tel:`, `mailto:` (dùng lại luật link đã có); link ngoài mở tab mới | người |

## 5. Trình tự session

- **S1 — Backend** (một session): T1 → T5. Kết thúc bằng `contract-sync` → **Gate 1** (#89: chỉ thêm +
  không câu hỏi ⇒ promote ngay). T4 sửa cả `packages/builder-components/meta/envelope-schema.ts` vì BE
  nạp `site-snapshot.schema.json` sinh từ đó lúc khởi động — shape snapshot là một phần của S1.
- **S2 — Frontend** (session mới, **sau** Gate 1 + `pnpm gen:api`): F1 → F6.
- Không chạm Component Registry (menu là chrome, không có manifest — [site-layout.tsx:5](../../../packages/builder-renderer/src/site-layout.tsx#L5)),
  không chạm `config/reserved-routes.json`.

## 6. Ràng buộc toàn cục

| Ràng buộc | Giữ bằng | Ở task |
|---|---|---|
| Tenant #21: `navigation_config` có `ShopId`, FK ghép `(website_id, shop_id)` (#92), query qua Global Query Filter, `ShopId` chỉ từ `ITenantContext` (#99) | `WebsiteTenantIsolationTests` thêm ca menu · `WebsiteDbConstraintTests` thêm FK ghép | T1, T3 |
| Trang trong menu phải cùng shop/website — tra **trong câu query** (#21.3) | `NavigationHandlerTests` "page của shop B ⇒ NAV_PAGE_NOT_FOUND" | T3 |
| Route mới chỉ Owner | `ShopScopedRouteFilterTests` (tự quét route mới) | T3 |
| `GET /api/site` không đọc bảng nào ngoài publication | `PublishedSiteBoundaryTests` vẫn xanh, không sửa test | T4 |
| Một nguồn URL (#103): menu dùng `createResolveUrl`, không tự ghép chuỗi URL | review + test `resolveNavigation` đi qua `createResolveUrl` | F1 |
| Renderer isomorphic (#23): dropdown/mobile không `window`/`document`, không JS | `ssr-hydrate.node.test.tsx` thêm ca có menu (không cảnh báo hydrate) | F2 |
| Enum string (#19) / TS không `enum` | kiểu `key` là chuỗi template, không enum | T2, F1 |
| File generated không sửa tay | `pnpm gen:api`, `pnpm gen:registry` | T4, F4 |
| `apps/web` không đọc draft | lint `no-restricted-imports` có sẵn | F3 |

## 7. Review focus — lỗi dễ lọt khi spec im lặng

| # | Tình huống | Hành vi đúng | Test ở |
|---|---|---|---|
| R1 | `PUT` mang `page:{id}` của shop khác (hoặc của trang đã xoá) | `422 NAV_PAGE_NOT_FOUND`, không lộ trang có tồn tại ở shop khác | T3 |
| R2 | Hai tab cùng lưu lần đầu (`version: null`), hoặc gửi `null` khi đã có bản lưu | Một bên thắng, bên kia `409 NAVIGATION_CONFLICT` (kể cả nhánh `23505`) | T3 |
| R3 | URL `custom` độc: `javascript:`, `  javascript:`, `java&#x09;script:`, `//evil.com` | `422 NAV_URL_UNSAFE`; FE render cũng lọc lại bằng `isSafeLinkUrl` | T2, F1 |
| R4 | Nhãn có HTML / toàn khoảng trắng | Lưu nguyên văn đã trim, render dạng text; rỗng sau trim ⇒ `null` (page) / `NAV_LABEL_REQUIRED` (custom) | T2, F2 |
| R5 | Trang trong menu bị tắt rồi publish; trang cha có con bị tắt | Snapshot không có mục đó; cha tắt ⇒ cả nhóm biến mất | T4, F1 |
| R6 | Rollback về bản publish trước Bước 7 (`items: []`) | Site hiện header không menu, không lỗi parse; menu đang sửa bật `isDirty` | T4, F3 |
| R7 | Menu con trên mobile, không JS | Mở/đóng được bằng `<details>`; SSR = CSR | F2 |

## 8. Sơ đồ file

```text
backend/src/
  Vsite.Domain/Website/Entities/NavigationConfig.cs                       (mới)
  Vsite.Application/Website/Navigation/NavigationItems.cs                 (mới — model + ghép + kiểm, hàm thuần)
  Vsite.Application/Website/Queries/GetNavigation/…                       (mới)
  Vsite.Application/Website/Commands/SaveNavigation/…                     (mới)
  Vsite.Application/Website/Commands/PublishWebsite/PublishWebsiteHandler.cs   (sửa)
  Vsite.Application/Website/Commands/RollbackPublication/RollbackPublicationHandler.cs (sửa)
  Vsite.Application/Website/Publishing/SnapshotBuilder.cs                 (sửa: nhận items)
  Vsite.Application/Website/Versioning/DocumentVersioning.cs              (sửa: Navigation = 1)
  Vsite.Application/Website/Exceptions/WebsiteExceptions.cs               (sửa: NAVIGATION_CONFLICT)
  Vsite.Infrastructure/Persistence/Configurations/Website/NavigationConfigConfiguration.cs (mới)
  Vsite.Infrastructure/Persistence/AppDbContext.cs                        (sửa)
  Vsite.Infrastructure/Persistence/Migrations/*_AddNavigationConfig.cs    (mới)
  Vsite.Api/Website/WebsiteEndpoints.cs                                   (sửa: 2 route)
backend/tests/IntegrationTests/Website/
  NavigationItemsTests.cs · NavigationHandlerTests.cs · NavigationEndpointTests.cs   (mới)
  WebsiteTenantIsolationTests.cs · WebsiteDbConstraintTests.cs            (sửa)
  PublishWebsiteHandlerTests.cs · SnapshotBuilderTests.cs                 (sửa)
packages/builder-components/
  meta/envelope-schema.ts                                                 (sửa: shape mục menu)
  src/site-navigation.ts (+ export ở src/index.ts)                        (mới: resolveNavigation)
  tests/site-snapshot-schema.test.ts · tests/site-navigation.test.ts      (sửa / mới)
packages/builder-renderer/
  src/site-layout.tsx · src/site-layout.test.tsx                          (sửa)
  tests/ssr-hydrate.node.test.tsx                                         (sửa)
apps/web/src/components/site-view.tsx · site-view.test.tsx                (sửa)
apps/web/src/server/load-site-core.ts                                     (sửa: bỏ workaround JsonSafe nếu hết cần)
apps/portal/
  package.json                                                            (thêm @dnd-kit/sortable)
  src/routes/_authenticated.shops.$shopId.website.menu.tsx                (mới)
  src/components/website/navigation-editor.tsx (+ .test.tsx)             (mới)
  src/components/website/website-overview.tsx · publish-dialog.tsx        (sửa)
  src/components/website/site-preview.tsx                                 (sửa)
```

## 9. Session S1: Backend

### T1: Entity `NavigationConfig` + bảng
`NavigationConfig` (`TenantAuditableEntity` như `Website`): `WebsiteId` (PK, 1:1), `ShopId`, `Items`
(jsonb, document `{ schemaVersion: 1, items: [...] }` — #94, đăng ký `Navigation = 1` ở
[DocumentVersioning.cs:22](../../../backend/src/Vsite.Application/Website/Versioning/DocumentVersioning.cs#L22) cạnh `Snapshot`),
`IsDirty`, `Version` = `xmin` (khuôn [PageDraft.cs:43-44](../../../backend/src/Vsite.Domain/Website/Entities/PageDraft.cs#L43-L44)).
Bảng `navigation_config`, FK ghép `(website_id, shop_id) → website (id, shop_id)`.
Phương thức: `Replace(itemsJson)` (bật dirty), `MarkPublished()`, `MarkDirty()`.
- [x] Test trước: `WebsiteDbConstraintTests` (FK ghép chặn `shop_id` lệch website) · `WebsiteTenantIsolationTests` (shop A không thấy menu shop B) · test entity dirty/published
- [x] FAIL → hiện thực + migration `AddNavigationConfig` → PASS · không còn pending model changes
- [x] Commit `feat(website): NavigationConfig entity + bảng (WEBSITE-007)`

### T2: Model menu — ghép + kiểm (hàm thuần)
`NavigationItems.Merge(stored?, pages)` → danh sách đã ghép (Flow 1, 3): chưa có ⇒ tự sinh (trừ
`NotFound`, theo `SortOrder` rồi `Title`); trang chưa có trong menu ⇒ nối cuối cấp 1, `hidden`; trang
không còn ⇒ bỏ (kèm con nếu là cha).
`NavigationItems.Validate(items, pages)` → lỗi `{path, code, message}` (data-needs §Lưu), URL qua
[LinkUrlRules.cs:24](../../../backend/src/Vsite.Application/Website/Validation/LinkUrlRules.cs#L24) `IsSafe`; trim nhãn.
`NavigationItems.ForSnapshot(items, publishedPageIds)` → chỉ mục hiện + trang có trong snapshot (Flow 4).
- [x] Test trước `NavigationItemsTests`: tự sinh đúng thứ tự, bỏ `NotFound` · trang mới nối cuối ẩn · trang xoá bỏ cả con · mỗi mã lỗi một ca (R3: 4 biến thể URL độc) · nhãn trim/rỗng (R4) · `ForSnapshot` bỏ `hidden`, bỏ trang tắt, cha tắt bỏ nhóm (R5)
- [x] FAIL → hiện thực → PASS
- [x] Commit `feat(website): ghép và kiểm menu (WEBSITE-007, #113)`

### T3: `GET` / `PUT …/website/navigation`
Query `GetNavigation` (đọc dòng + trang chưa xoá của website, `Merge`), command `SaveNavigation`
(`version` null ⇒ insert, khác ⇒ so `xmin`; `23505`/`DbUpdateConcurrencyException` ⇒ `409
NAVIGATION_CONFLICT` theo khuôn [SavePageDraftHandler.cs](../../../backend/src/Vsite.Application/Website/Commands/SavePageDraft/SavePageDraftHandler.cs)).
Request là DTO có kiểu (không raw `JsonElement`) để Orval sinh type. Route `.WithName("getNavigation")`,
`.WithName("saveNavigation")`, `RequireOwner()`.
- [ ] Test trước `NavigationHandlerTests` + `NavigationEndpointTests`: R1, R2, 422 có `params.errors[].path`, `isDirty` sau lưu, `version` mới khác cũ, không phải Owner ⇒ 403
- [ ] FAIL → hiện thực → PASS · `ShopScopedRouteFilterTests` xanh
- [ ] Commit `feat(website): API đọc/ghi menu (WEBSITE-007)`

### T4: Publish / rollback mang menu + shape snapshot
`envelope-schema.ts`: thay `items: z.array(z.unknown())` ([:215](../../../packages/builder-components/meta/envelope-schema.ts#L215))
và `{ type: 'array' }` (:262) bằng shape mục `{ key, label: string|null, url?, children? }` tối đa 3 cấp (cấp 3 không có `children`),
strict, cả Zod lẫn JSON Schema. Xuất kiểu `SiteNavigationItem`. `SnapshotBuilder.Build` nhận thêm items
(thay dòng :83). `PublishWebsiteHandler`: `Merge` → `ForSnapshot` → build; upsert `NavigationConfig` +
`MarkPublished()` trong cùng `SaveChanges`. `RollbackPublicationHandler`: `MarkDirty()` menu nếu có dòng.
- [ ] Test trước: `site-snapshot-schema.test.ts` (snapshot `items: []` cũ vẫn hợp lệ — R6; mục 3 cấp hợp lệ, mục cấp 4 bị từ chối) · `schema-equivalence` / `envelope-schema` Zod ≡ JSON Schema · `SnapshotBuilderTests` · `PublishWebsiteHandlerTests` (chưa có menu ⇒ snapshot có menu tự sinh + dòng được tạo, `isDirty=false`; R5) · rollback ⇒ `isDirty=true`
- [ ] `pnpm gen:registry` → FAIL → hiện thực → PASS · `PublishedSiteBoundaryTests` xanh không sửa
- [ ] Commit `feat(website): snapshot mang menu thật (WEBSITE-007, #113)`

### T5: Kết thúc S1
- [ ] `dotnet test` toàn bộ (cần Docker — không có thì ghi `Docs/DOCKER-TEST-DEBT.md`)
- [ ] `pnpm contract:export` → skill `contract-sync` → `contract-diff.md` → Gate 1 (#89)
- [ ] `backend/docs/modules/website.md`: mục `NavigationConfig`, bỏ câu "Chưa có NavigationConfig (7)" (:9), shape snapshot (:106)
- [ ] `00-INDEX.md` §1 dòng "Quyết định `#N` nằm ở đâu" thêm `#113`; banner STATUS `05` (§8 đã code; lệch: thứ tự = vị trí mảng, không field `order`; cấm trùng trang) → `pnpm gen:doc-index`
- [ ] `DECISIONS.md` #113 → ✅ đã code · `changelog.md` (DoD) · `pnpm check:docs`

## 10. Session S2: Frontend (mở SAU Gate 1)

### F1: `resolveNavigation` (`builder-components`)
`resolveNavigation(items, pages: {id, title, slug, systemType}[], resolveUrl)` →
`{ label, href, external, children[] }[]`. `page:{id}` ⇒ `label ?? title`, `href` qua `resolveUrl`
(link kiểu page — [site-url.ts:44-65](../../../packages/builder-components/src/site-url.ts#L44-L65));
trang thiếu ⇒ bỏ (cha thiếu ⇒ bỏ nhóm); `custom` ⇒ chỉ khi `isSafeLinkUrl`, `external` = scheme http(s).
- [ ] Test trước `tests/site-navigation.test.ts`: R3 (lọc lại), R5, nhãn null, thứ tự giữ nguyên · `public-exports.test.ts` có hàm mới
- [ ] Commit `feat(builder-components): resolveNavigation (WEBSITE-007, #113)`

### F2: `SiteLayout` — menu con + mobile
Prop `menuItems` ([site-layout.tsx:12](../../../packages/builder-renderer/src/site-layout.tsx#L12)) thêm
`children?`, `external?`. Desktop: dropdown mở bằng `:hover`/`:focus-within` (CSS). Mobile:
`<details><summary>` hamburger. Link ngoài `target="_blank" rel="noopener noreferrer"`. Style chỉ `var(--vs-*)`.
- [ ] Test trước `site-layout.test.tsx` (con render, link ngoài có `rel`, nhãn HTML hiện dạng text — R4) · `ssr-hydrate.node.test.tsx` ca có menu (R7)
- [ ] Commit `feat(builder-renderer): Header có menu con + mobile (WEBSITE-007)`

### F3: `apps/web` truyền menu
[site-view.tsx:23-25](../../../apps/web/src/components/site-view.tsx#L23-L25): dựng `resolveUrl` một lần,
`resolveNavigation(snapshot.navigation.items, snapshot.pages, …)` → `SiteLayout menuItems`. Dọn workaround
`JsonSafe` ở [load-site-core.ts:25-36](../../../apps/web/src/server/load-site-core.ts#L25-L36) nếu kiểu đã cụ thể.
- [ ] Test trước `site-view.test.tsx`: có menu · snapshot `items: []` (R6) · mục mồ côi
- [ ] Commit `feat(web): Shop Site hiện menu (WEBSITE-007)`

### F4: Portal — màn Menu
`pnpm gen:api` (sau Gate 1). Thêm `@dnd-kit/sortable`. Route `website.menu` bọc `OwnerOnlyGate`, lối vào ở
[website-overview.tsx:49-64](../../../apps/portal/src/components/website/website-overview.tsx#L49-L64).
`navigation-editor.tsx`: danh sách sắp được (tối đa 3 cấp), kéo vào/ra giữa các cấp, đổi nhãn (để trống = tên
trang), ẩn/hiện, thêm/xoá `custom`, gắn "trang đang tắt", nút Lưu, lỗi 422 tô đúng dòng theo `path`,
409 ⇒ thông báo + tải lại.
- [ ] Test trước (MSW) `navigation-editor.test.tsx`: loading · error · menu tự sinh (`version: null`) · sắp bằng bàn phím (dnd-kit keyboard sensor) · lưu gửi đúng body · 422 tô dòng · 409 · chặn lồng cấp 4
- [ ] Smoke chạy app thật sau F4 (workflow §8)
- [ ] Commit `feat(portal): màn sắp menu website (WEBSITE-007)`

### F5: Portal — dấu "menu chưa publish" + preview
`website-overview.tsx` / `publish-dialog.tsx` hiện "Menu có thay đổi chưa publish" theo `isDirty`; sau
publish/rollback invalidate query menu. [site-preview.tsx:164-167](../../../apps/portal/src/components/website/site-preview.tsx#L164-L167)
truyền `menuItems` qua `resolveNavigation` (trang đang tắt bị bỏ như site).
- [ ] Test trước: `publish-dialog.test.tsx` / `site-preview.test.tsx` thêm ca
- [ ] Commit `feat(portal): nhắc menu chưa publish + preview có menu (WEBSITE-007)`

### F6: Nghiệm thu tay với BE thật
- [ ] Sắp menu (có menu con + link Fanpage) → publish → `{slug}.vsite.local` hiện đúng; dropdown + mobile chạy
- [ ] Tạo trang mới → màn Menu thấy ở cuối, ẩn · xoá trang có trong menu → site sau publish không còn mục
- [ ] Rollback → site về menu cũ, màn Menu báo chưa publish
- [ ] Hai tab cùng sửa menu → tab sau nhận 409 có thông báo

## 11. Đóng task (trước Gate 2)
- [ ] Integration tắt MSW · `change-reviewer` (chạm tenant + contract ⇒ model mạnh nhất) · `review.md` (kèm "Ma sát quy trình")
- [ ] `changelog.md` thêm phần FE · `00-INDEX` §3 dòng Bước 7

## 12. Giả định tôi đã tự đặt (người đã duyệt 2026-11-20)

| # | Giả định | Vì sao | Nếu sai thì |
|---|---|---|---|
| G1 | Một trang chỉ một lần trong menu | Đơn giản hoá ghép "trang chưa có ⇒ nối cuối" | Bỏ `NAV_PAGE_DUPLICATE`, ghép theo id + vị trí |
| G2 | Thứ tự = vị trí mảng, bỏ field `order` của `05` §8 | Một nguồn sự thật, không lệch số | Thêm `order` additive |
| G3 | Trang đã xoá bị bỏ lúc đọc; trang tắt giữ lại | Lưu lần sau tự sạch; bật lại trang thì menu có ngay | Job 8d phải dọn |
| G4 | Giới hạn 12 mục cấp 1 / 10 mục con mỗi cha (cấp 2 và cấp 3) / nhãn 60 / URL 2048 | Header shop nhỏ; chặn payload phình | Đổi hằng số |
| G5 | Lưu menu có `version` + 409 (khác theme last-write-wins) | Menu là danh sách, dễ ghi đè mất | — |
| G6 | Chưa từng lưu ⇒ `isDirty = true` | Website đang chạy có snapshot `items: []`, menu tự sinh chưa lên site | Owner không được nhắc publish lần đầu |
| G7 | Trang hệ thống được vào menu, trừ `NotFound` (vd. `Contact`) | `NotFound` không có URL (slug null) | — |

## 13. Verification tổng

- `dotnet test` backend xanh (kể cả Testcontainers) · `pnpm build && pnpm test` xanh · `pnpm check:docs` xanh
- `contracts/contract.lock` khớp sau promote; `contract-diff.md` chỉ có ADDED
- Nghiệm thu F6 4/4 với BE thật, ghi vào `changelog.md`
- Tiêu chí `step.md` 7: sắp menu ở Portal, publish, site công khai hiện đúng menu; mục trỏ tới trang đã xoá không hiện ra
