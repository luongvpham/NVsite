# vsite — Thứ tự triển khai

> **STATUS:** `STABLE` · **Tasks:** `DESIGN-ROADMAP-001` · **Changelog:** `—` · **Stale:** `—`
> **Cửa vào:** [`00-INDEX.md`](00-INDEX.md) §3 — bảng tiến độ có trạng thái từng bước.

**Thứ tự code ≠ Phase.** Phase ở `01` §8 nói một tính năng thuộc **đợt ra mắt** nào (#39.1). File này
nói **làm theo thứ tự nào**. Hai thứ được phép khác nhau — tiền lệ là #68.

**Quyết định #96 — Builder (Phase 2) code trước Marketplace (Phase 1).** Marketplace ít rủi ro kỹ thuật
(CRUD + tìm kiếm), còn builder là chỗ thiết kế dễ vỡ nhất (tree JSONB, publish, binding, cache). Làm
builder trước để lộ lỗi thiết kế sớm. Phạm vi ra mắt **không đổi**: MVP vẫn là Phase 1. Hạ tầng dùng
chung (Hangfire 8a, outbox + Elasticsearch 10c) do bên làm trước dựng, Marketplace dùng lại.

Mỗi bước ghi theo bốn mục: **Gồm** · **Chốt trước** (chưa chốt thì chưa bắt đầu) · **Nợ mang theo** (lấy
từ `00-INDEX` §4) · **Xong khi**.

---

## Đã xong

| Bước | Nội dung | Task |
|---|---|---|
| 1 | Framework FE + BE, Orval, `contracts/openapi/`, MSW, `config/reserved-routes.json`, module boundary | BOOTSTRAP-001 |
| 2 | Component Manifest + codegen + 5 component mẫu (thuần build-time). Whitelist sanitize (#67) chốt ở bước này | BOOTSTRAP-002 |
| 3 | Identity + Role + UserShop + Shop + PendingRegistration + Global Query Filter + token scope | IDENTITY-001 |
| 4 | `MediaAsset` + pipeline ảnh + Media Library + 9 preset + `/media/*` trên mọi domain (`08`) | MEDIA-001 |

## 4.x — Dọn nền trước Bước 5 (Lane A, task nhỏ)

1. **Nâng `SixLabors.ImageSharp`** (CVE high, REFACTOR-BE-001 #4) và chạy lại test Imaging. Pipeline ảnh
   xử lý file người dùng upload.
2. **Nghiệm thu tay tiêu chí dừng `08` §9** (MEDIA-001 #5, #7). Bước 5 sẽ đưa `imageId` vào tree, nên
   không xây tiếp trên một nền chưa ai kiểm.

---

## 5a. `Website` → `Theme` → `Page` → `PageDraft` — backend (Lane C)

**Gồm**
- Entity + migration. FK ghép theo #92. `schemaVersion` + khung upcaster cho `Tree` và `Tokens` theo #94.
  `Website` có sẵn `DefaultLocale` + `Locales` (#98, `05` §26).
- **Tạo website trắng** cho shop `Hosted`, gồm trang chủ (`Slug = ""`) và các `System` page của
  Phase 2 **trừ nhóm Product**: `Contact` và `NotFound`. `ProductListing`/`ProductDetail`/`ProductSearch`
  sinh ở 10c (website đã có thì tạo bù). Trước khi có Bước 11, website chỉ tạo được bằng cách này.
- API metadata `Page`, cùng `GET`/`PUT` `PageDraft` có `version`. Concurrency dùng `xmin`; lệch version
  trả `409 DRAFT_CONFLICT`. Map chung `DbUpdateConcurrencyException`/`23505` → 409 (REFACTOR-DB-001 #1, #2).
- **Validate toàn tree khi `PUT`.** Server không nhận op (#42) mà nhận cả tree, nên nó tự kiểm mọi luật
  của `05` §11:
  - `allowedInPageKinds`.
  - Với `System` page: cấu trúc phải trùng bản đang lưu, và chỉ prop có `editableInSystemPage` được đổi.
    Việc này cần so với tree cũ.
  - Props theo `props-schemas.json` (#60). Vỏ tree và `Theme.Tokens` theo #97.
  - Whitelist `binding.source`.
  - **Sanitize richText ở BE** theo `config/sanitize-profiles.json`. Hiện BE **chưa** đọc file này;
    `07` §11 yêu cầu chạy lại ở BE.
  - Nối `IMediaReferenceValidator` (#71, #77 — MEDIA-001 #1).
- Endpoint `references` quét thêm tree (MEDIA-001 #2, phần tree).
- `Slug` không được trùng mã locale trong `config/locales.json` (#98) hay route của `System` page.

**Chốt trước:**
- Quy ước **một nguồn `ShopId`** cho handler: route hay `ITenantContext` (REFACTOR-BE-001 #3). Handler
  mới sẽ nhân lỗi này lên nếu chưa chốt.
- Tên file và field cụ thể cho #97 (`config/theme-tokens.json`, `config/locales.json`): chốt trong
  `data-needs.md`.

**Xong khi:** hai tab cùng sửa một trang thì tab cũ nhận 409. Tree chứa id bản Library, id của shop
khác, `binding.source = "Review"` hoặc richText có `<script>` đều bị BE từ chối, dù đã bỏ qua FE.

## 5b. Operations Engine + editor — FE (Lane C)

**Gồm**
- Operations Engine phía client: immer + zundo (#42), 5 op, bảng phép/cấm `05` §11, dùng `op-rules.ts`.
- Editor ở Portal: canvas (dùng `builder-renderer` hiện có, đọc draft), Inspector (có từ Bước 2), autosave
  debounce ~2s, xử lý 409 (báo có bản mới hơn, cho tải lại).
- Theme editor (`changeTheme`), theme đi qua **CSS variables** (TOOLING-001 #1).
- Khung crop theo tỉ lệ preset ở dialog ảnh (MEDIA-001 #6).

**Chốt trước:** refresh token chuyển sang cookie httpOnly (REFACTOR-API-001 #2). Editor có autosave mà F5
mất phiên là mất việc, nên làm trước hoặc ngay đầu bước.

**Xong khi:** dựng được một trang từ đầu bằng kéo-thả, reload thì còn nguyên, undo/redo chạy trong phiên.

## 6. `builder-renderer` hoàn thiện + preview toàn trang

Renderer đã có từ Bước 2. Bước này **chốt API của nó** trước khi `apps/web` dùng thật ở 8b.

**Gồm**
- `RenderTree` nhận `basePath` (#11), `locale` + `defaultLocale` (#98), cùng một `resolveUrl` duy nhất.
- Snapshot SSR↔CSR (#23, BOOTSTRAP-002) và lint chặn renderer import `apps/**`/`builder-core` (TOOLING-001).
- Khung trang (layout có Header/Footer), preview toàn trang trong Portal đọc **draft**.

**Không gồm:** render công khai ở `apps/web`. Việc đó cần `SitePublication` nên nằm ở 8b (`05` §7: web
**không bao giờ** đọc draft).

## 7. `NavigationConfig`

**Gồm:** nguồn `page:{id}` + `custom`, màn kéo-thả, `Header` nhận danh sách đã resolve (`05` §8).
Nguồn `serviceGroup:{id}` thêm ở Bước 9, `sysProductListing`/`productCategory:{id}` thêm ở 10c.
Job dọn mục mồ côi chờ 8a. Trong lúc chờ, renderer chỉ bỏ qua mục mồ côi.

## 8a. Hangfire — hạ tầng job (dùng chung)

**Gồm:** Hangfire lưu trên Postgres. Dashboard chỉ `PlatformAdmin`. Quy ước job chạy **ngoài** request
không có tenant context: job đặt `TenantContext` theo từng shop, hoặc dùng helper truy vấn có trong
allowlist (#90) — **không** rải `IgnoreQueryFilters()`. Nên trả luôn 3 job của `03` (IDENTITY-001 §B).

## 8b. `SitePublication` + publish/rollback + cache + render công khai

**Chốt trước:** cache/TTL (`05` §25 #4). Lời giải **phải tính cả dữ liệu bind sống**: snapshot bất biến
cache theo `version` được, nhưng `Service`/`Product` đổi thì site phải đổi theo mà không cần publish (`05`
§6 quy tắc 3). Chốt cache chỉ cho snapshot thì Bước 9 sẽ phải làm lại phần này.

**Gồm**
- Snapshot toàn site gồm theme, navigation, seo, locales và pages (`05` §7, §26). `IsDirty`.
  Rollback = đổi `CurrentPublicationId`.
- Hành vi theo `Website.Status`: `Draft` thì công chúng không thấy, `Suspended` thì hiện trang thông báo.
- `apps/web` render theo host `{slug}.vsite.vn`, có SSR loader thật (REFACTOR-API-001 #3). Kèm sitemap/robots
  theo site.
- Job: prune giữ 20 bản (không xoá bản current), dọn file media mồ côi (#55, #72), `references` quét
  snapshot (MEDIA-001 #2, #3), dọn mục menu mồ côi.

**Xong khi:** publish xong thì `{slug}.vsite.local` thấy bản mới; sửa draft không lộ ra ngoài; rollback
về bản cũ trong một thao tác.

## 9. `Service` + Binding Resolver (`06`)

**Chốt trước:** Binding Resolver **chạy ở đâu** (SSR loader `apps/web` gọi API, hay BE resolve sẵn) và
**chữ ký** của nó. Chữ ký nhận `{ shopId, locale }` ngay từ đầu (#98), dù dữ liệu chưa dịch.

**Gồm**
- `ShopServiceGroup` → `Service`. Có luôn `DetailPageId`, vì `Page` đã có nên không cần tách đợt như
  `06` §9.
- Sanitize `Description` bằng profile `basic`. Ảnh dữ liệu nghiệp vụ nguồn `Service`/`ServiceGroup` (#73).
- Manifest `ServiceGrid`/`ServiceList`/`PriceTable`/`ServiceHighlight`/`ServiceGroupTabs`.
  Nguồn menu `serviceGroup:{id}`.
- Xoá `Service` → quét tree tìm `binding.serviceId`, chỉ cảnh báo (`06` §7).

## 10. `Product` — tách ba task

- **10a.** `ShopProductCategory` → `ShopAttribute` → `ShopAttributeOption` → `CategoryAttribute` (#95).
- **10b.** `Product` → `ProductAttributeValue` → `ProductVariant`, ảnh `ImageUrls` (#79–#81) kèm bản TS
  của hàm tên `thumb_`/`fthumb_` (MEDIA-001 #4). **Chốt trước:** UX sinh variant tự động (`05` §25 #6) —
  quyết định này ảnh hưởng API.
- **10c.** **Outbox** (REFACTOR-DB-001 #4) → Elasticsearch index + facet + sync. Kèm `System` page
  `ProductListing`/`ProductDetail`/`ProductSearch` + route (tạo bù cho website đã có), binding
  `ProductGrid`, nguồn menu sản phẩm. Hạ tầng outbox + ES sau đó Marketplace dùng lại.

## 11. `WebsiteTemplate` + khởi tạo từ template

**Chốt trước:** template được soạn bằng gì. Đề xuất: dựng bằng chính builder trên một shop nội bộ rồi
xuất snapshot, thay vì viết tay JSON. Ảnh placeholder lấy từ đâu và copy thành `MediaAsset` của shop
thế nào (`05` §10).

---

## Sau builder — Marketplace (Phase 1, #96)

Thứ tự phụ thuộc theo `02` §1.2: `ServiceCategory` → `Listing` → `Review`/`Lead`/index ES.

| Bước | Nội dung | Chốt trước |
|---|---|---|
| M0 | Mô hình **public read/write** xuyên tenant cho `Review`/`Lead` và tìm kiếm marketplace (review kiến trúc P1) | — chặn mọi bước M |
| M1 | `administrative_unit` (#93) + `ServiceCategory` + màn quản trị taxonomy | |
| M2 | `Listing`: ảnh theo #79/#81, đủ 3 `TargetKind` vì `Page` đã có | |
| M3 | `Lead` — gồm form trên website shop (#47) + component `ContactForm` — và `Review` | `02` §6 #10 (SLA khiếu nại) |
| M4 | Tìm kiếm bản đồ trên ES (dùng hạ tầng 10c) + trang hồ sơ `vsite.vn/shop/{slug}` + landing SEO khu vực | |
| M5 | Kiểm duyệt + job kiểm link chết (8a) | `02` §6 #9 |

`Contact` page trước M3 chỉ có thông tin liên hệ, chưa có form.

## Đa ngôn ngữ (#98, `05` §26)

Thiết kế xong rồi rải vào các bước. **Cái nào đổi sau là phải migrate hoặc vỡ URL thì làm sớm.**

| Ở đâu | Việc |
|---|---|
| 5a | `Website.DefaultLocale` + `Locales`, `config/locales.json`, chặn `Slug` trùng mã locale |
| 6 | Renderer nhận `locale` + `defaultLocale` |
| 8b | Snapshot mang `defaultLocale` + `locales` |
| 9 | Chữ ký Binding Resolver nhận `locale` |
| **L** (chưa xếp) | **Bật tính năng:** `page_translation`, overlay `i18n` trên node, nhãn menu theo locale, UI dịch trong editor, URL `/{locale}/…` + `hreflang` |
