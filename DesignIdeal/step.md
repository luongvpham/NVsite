# vsite — Thứ tự triển khai

> **STATUS:** `STABLE` · **Tasks:** `DESIGN-ROADMAP-002,DESIGN-ROADMAP-001` · **Changelog:** `—` · **Stale:** `—`
> **Cửa vào:** [`00-INDEX.md`](00-INDEX.md) §3 — **trạng thái từng bước chỉ ghi ở đó**, file này không ghi trạng thái.

**Thứ tự code ≠ Phase.** Phase ở `01` §8 nói một tính năng thuộc **đợt ra mắt** nào (#39.1). File này
nói **làm theo thứ tự nào**. Hai thứ được phép khác nhau — tiền lệ là #68.

**Số bước là tên, không phải thứ tự.** Tên bước (`7`, `8a`, `10c`…) giữ nguyên qua các lần xếp lại vì
code, changelog và `DECISIONS.md` đã trỏ tới chúng. Thứ tự làm là **thứ tự các mục trong file này**:

```
1 → 2 → 3 → 4 → 5a → 5b → 6 → 8b → 8c → 7 → 8a → 8d → 9 → C → 11 → A0 → A1 → A2 → A3
  → M0…M5 → 10a → 10b → 10c → A4
```

**Quyết định #96 — Lõi builder và AI cho shop dịch vụ code trước Marketplace; `Product` code sau.**
- Lõi builder (Phase 2: Bước 5a–9, C, 11) và AI cho shop dịch vụ (A0–A3, #105) làm **trước** Marketplace
  (Phase 1). Marketplace ít rủi ro kỹ thuật (CRUD + tìm kiếm), còn builder là chỗ thiết kế dễ vỡ nhất
  (tree JSONB, publish, binding, cache). Làm builder trước để lộ lỗi thiết kế sớm. Cùng lý do đó, trong
  builder phần rủi ro nhất còn lại — publish + render công khai (8b, 8c) — làm trước `NavigationConfig`
  (7) và Hangfire (8a).
- `Product` (Bước 10a–c) làm **sau** Marketplace: nó không thuộc MVP (#39.1) và là khối lớn nhất còn lại.
  AI tạo shop có sản phẩm (A4) vì thế cũng đứng sau.
- Phạm vi ra mắt **không đổi**: MVP vẫn là Phase 1.
- Hạ tầng dùng chung do bên làm trước dựng: Hangfire ở 8a; **outbox ở M2, Elasticsearch ở M4**; 10c dùng lại.

Mỗi bước ghi theo bốn mục: **Gồm** · **Chốt trước** (chưa chốt thì chưa bắt đầu) · **Nợ mang theo** (lấy
từ `00-INDEX` §4) · **Xong khi**. Mục nào không có thì bỏ, không ghi "—".

---

## Bước 1–5b

| Bước | Nội dung | Task |
|---|---|---|
| 1 | Framework FE + BE, Orval, `contracts/openapi/`, MSW, `config/reserved-routes.json`, module boundary | BOOTSTRAP-001 |
| 2 | Component Manifest + codegen + 5 component mẫu (thuần build-time). Whitelist sanitize (#67) | BOOTSTRAP-002 |
| 3 | Identity + Role + UserShop + Shop + PendingRegistration + Global Query Filter + token scope | IDENTITY-001 |
| 4 | `MediaAsset` + pipeline ảnh + Media Library + 9 preset + `/media/*` trên mọi domain (`08`) | MEDIA-001 |
| 4.x | Thay ImageSharp bằng SkiaSharp (#84) · nghiệm thu tay `08` §9 | SEC-IMAGESHARP-001 · MEDIA-001-ACCEPT |
| 5a | `Website` → `Theme` → `Page` → `PageDraft` (BE): website trắng cho shop `Hosted` (trang chủ + `Contact` + `NotFound`), `PUT` draft có `version` (`xmin`, `409 DRAFT_CONFLICT`), validate **toàn tree** mỗi lần `PUT` (`05` §11, #60, #67, #71, #77, #97), một nguồn `ShopId` (#99) | WEBSITE-001 |
| 5b | Operations Engine (immer + zundo, #42) + editor Portal: canvas, Inspector, autosave ~2s, xử lý 409, Theme editor qua CSS variables, khung crop preset | WEBSITE-002 |

Chi tiết và phần lệch so với thiết kế: `Docs/tasks/<TASK>/changelog.md`.

## 6. `builder-renderer` hoàn thiện + preview toàn trang (WEBSITE-003)

Chốt API của renderer trước khi `apps/web` dùng thật ở 8c.

**Gồm**
- `RenderTree` nhận `basePath` (#11), `locale` + `defaultLocale` (#98), cùng một `resolveUrl` duy nhất (#103).
- Snapshot SSR↔CSR (#23) và lint chặn renderer import `apps/**`/`builder-core`.
- `SiteLayout` (Header/Footer ngoài Component Tree), preview toàn trang trong Portal đọc **draft**.

**Xong khi:** từ editor mở preview, bấm link sang trang khác của shop thì thấy draft trang đó; link hỏng
hiện `NotFound` kèm lý do; `renderToString` ở Node rồi hydrate không lệch.

---

## 8b. `SitePublication` + publish/rollback + endpoint đọc công khai — BE (Lane C)

**Chốt trước**
- **Mô hình đọc công khai của Shop Site.** Mọi endpoint hiện có đều `Owner`-only dưới
  `/api/shops/{shopId}/…`. Đây là lần đầu cần đọc **không token**, tenant suy từ Host. Phải chốt: hình
  dạng endpoint; SSR server của `apps/web` chuyển Host gốc sang BE bằng gì; đọc qua helper trong
  allowlist (#90), **không** rải `IgnoreQueryFilters()`. Đây là nửa "đọc" của bài toán public read/write
  — M0 chỉ còn nửa "ghi" và đọc xuyên shop.
- **Cache/TTL** (`05` §25 #4). Lời giải **phải tính cả dữ liệu bind sống**: snapshot bất biến cache theo
  `version` được, nhưng `Service`/`Product` đổi thì site phải đổi theo mà không cần publish (`05` §6 quy
  tắc 3). Chốt cache chỉ cho snapshot thì Bước 9 sẽ phải làm lại phần này.

**Gồm**
- Snapshot toàn site gồm theme, navigation, seo, locales và pages (`05` §7, §26), có `schemaVersion`
  (#94). `navigation.items` để **rỗng** cho tới Bước 7. `IsDirty`. `Website.CurrentPublicationId`.
- Publish và rollback (rollback = đổi `CurrentPublicationId`).
- Endpoint công khai trả bản đang publish theo Host. Theo `Website.Status`: `Draft` thì công chúng không
  thấy, `Suspended` thì trả trạng thái để web hiện trang thông báo.
- Enforce `requiresProps` lúc publish (WEBSITE-001 #3).
- `references` quét thêm snapshot (MEDIA-001 #2) và báo `Page.OgImageId` / `Website.DefaultOgImageId` /
  `FaviconAssetId` (WEBSITE-001 #14).

**Xong khi:** publish sinh một bản bất biến; sửa draft sau đó **không** đổi kết quả endpoint công khai;
rollback về bản cũ trong một thao tác; gọi bằng Host của shop A không đọc được bản của shop B; website
`Draft` trả 404.

## 8c. `apps/web` render công khai + cache — FE (Lane C)

`apps/web` hiện chỉ là khung. Bước này dựng app SSR thật lần đầu.

**Gồm**
- Render theo host `{slug}.vsite.vn` (#104), `basePath = ''` (#103), dùng `RenderTree` + `SiteLayout` của
  Bước 6. Đọc snapshot qua upcaster (#94). **Không bao giờ** đọc draft (`05` §7).
- SSR loader thật: `createApiClient` cho từng request, chuyển Host gốc (REFACTOR-API-001 #3).
- Trang `NotFound`, trang thông báo `Suspended`, sitemap/robots theo site.
- Cache theo phương án đã chốt ở 8b.

**Nợ mang theo**
- Đổi `Shop.Slug` làm vỡ URL site: làm `shop_slug_history` + redirect (REFACTOR-DB-001 #4), hoặc ghi rõ
  là chấp nhận.
- Mở thẳng `/media/…` trên thanh địa chỉ ra 404 HTML vì SSR chặn request dạng document (MEDIA-001-ACCEPT L3).
- CSRF chỉ dựa `SameSite=Lax` trong khi `{slug}.vsite.vn` cùng *site* với `admin.vsite.vn` (IDENTITY-002)
  — xem lại trước khi site shop ra công khai.

**Xong khi:** publish xong thì `{slug}.vsite.local` thấy bản mới; sửa draft không lộ ra ngoài; rollback
xong thì site về bản cũ; xem nguồn trang thấy HTML đã render ở server và hydrate không cảnh báo.

> **Mốc review.** Sau 8c là lần đầu một website shop chạy end-to-end. Dừng ở đây để người duyệt xem lại
> #96–#98 trên thứ đang chạy trước khi làm tiếp.

## 7. `NavigationConfig`

**Gồm:** nguồn `page:{id}` + `custom`, màn kéo-thả, `Header` của `SiteLayout` nhận danh sách đã resolve
(`05` §8), `schemaVersion` (#94). Snapshot (8b) bắt đầu mang `navigation` thật.
Nguồn `serviceGroup:{id}` thêm ở Bước 9, `sysProductListing`/`productCategory:{id}` thêm ở 10c.
Job dọn mục mồ côi nằm ở 8d. Trong lúc chờ, renderer chỉ bỏ qua mục mồ côi.

**Xong khi:** sắp menu ở Portal, publish, site công khai hiện đúng menu; mục trỏ tới trang đã xoá không
hiện ra.

## 8a. Hangfire — hạ tầng job (dùng chung)

**Gồm:** Hangfire lưu trên Postgres. Dashboard chỉ `PlatformAdmin`. Quy ước job chạy **ngoài** request
không có tenant context: job đặt `TenantContext` theo từng shop, hoặc dùng helper truy vấn có trong
allowlist (#90) — **không** rải `IgnoreQueryFilters()`. Trả luôn 3 job của `03` (IDENTITY-001 §B).

**Xong khi:** 3 job của `03` chạy theo lịch; người không phải `PlatformAdmin` không mở được dashboard;
có test khẳng định job xử lý shop A không chạm dữ liệu shop B.

## 8d. Job của builder

**Gồm:** prune `SitePublication` giữ 20 bản (không xoá bản current) · dọn file media mồ côi theo
retention (#55, #72 — MEDIA-001 #3) · dọn mục menu mồ côi · dọn draft của page đã xoá mềm (WEBSITE-001 #13).

**Xong khi:** website có hơn 20 bản thì còn đúng 20 và bản current vẫn còn; ảnh clone không còn bản
publish nào tham chiếu thì file bị dọn, ảnh còn được tham chiếu thì không.

## 9. `Service` + Binding Resolver (`06`)

**Chốt trước:** Binding Resolver **chạy ở đâu** (SSR loader `apps/web` gọi API, hay BE resolve sẵn) và
**chữ ký** của nó. Chữ ký nhận `{ shopId, locale }` ngay từ đầu (#98), dù dữ liệu chưa dịch. Đọc dữ liệu
bind dùng đúng mô hình đọc công khai đã chốt ở 8b.

**Gồm**
- `ShopServiceGroup` → `Service`. Có luôn `DetailPageId`, vì `Page` đã có nên không cần tách đợt như
  `06` §9. Màn quản lý dịch vụ ở Portal.
- Sanitize `Description` bằng profile `basic`. Ảnh dữ liệu nghiệp vụ nguồn `Service`/`ServiceGroup` (#73).
- Manifest `ServiceGrid`/`ServiceList`/`PriceTable`/`ServiceHighlight`/`ServiceGroupTabs`.
  Nguồn menu `serviceGroup:{id}`.
- Xoá `Service` → quét tree tìm `binding.serviceId`, chỉ cảnh báo (`06` §7).

**Xong khi:** thêm hoặc sửa `Service` ở Portal thì site công khai đổi theo **không cần publish**; xoá
`Service` đang được bind thì Portal cảnh báo trước khi xoá.

## C. Thư viện component đợt 2

Registry mới có 5 loại. Template theo lĩnh vực (11) và AI ghép trang (A2, A3) đều cần đủ khối để ghép.

**Chốt trước**
- **Danh sách component và variant của đợt này**, lấy từ `01` §7 cộng các khối trang giới thiệu hay dùng:
  lưới thẻ (icon/ảnh + tiêu đề + mô tả), chia đôi ảnh–chữ, các bước, dải kêu gọi hành động, lời chứng
  thực, chỉ số, FAQ, giờ mở cửa, liên hệ + bản đồ, slide ảnh, before/after, video; mở rộng `Hero`
  (nhãn nhỏ, nút thứ hai). `ContactForm` vẫn ở M3; nhóm `Service` đã có từ Bước 9.
- **Quy ước mô tả cho AI.** `description` của mỗi component và variant phải nói **khi nào nên dùng**,
  không chỉ nó là gì — chất lượng AI ghép trang phụ thuộc vào đây. Nếu cần field mới trong manifest thì
  đó là đổi contract Registry (additive, #43), phải qua người duyệt.
- **Video nhúng bằng gì.** Sanitizer đang cấm `iframe`; cần một prop có whitelist nguồn (chỉ nhận ID
  video, cùng cách với `TrackingSnippets` ở `05` §2), không mở HTML tự do.

**Gồm:** manifest + component + variant cho danh sách đã chốt; ảnh preview của từng variant; preset ảnh
mới (nếu có) thêm vào `config/image-presets.json`.

**Xong khi:** dựng lại được một trang giới thiệu tham chiếu khoảng 12 section chỉ bằng kéo-thả; mọi
component mới qua snapshot SSR↔CSR; `check-additive` xanh.

## 11. `WebsiteTemplate` + khởi tạo từ template

Template là điểm xuất phát của AI tạo site (A2): AI sửa một cây đã hợp lệ và đã được người duyệt, không
dựng từ trang trắng.

**Cách soạn (#107):** AI sinh cây trang theo lĩnh vực → developer mở bằng chính builder trên một shop nội
bộ, sửa và duyệt → xuất thành template. Không viết tay JSON.

**Chốt trước:** kho ảnh mẫu theo lĩnh vực lấy từ đâu (bản quyền), và copy thành `MediaAsset` của shop
thế nào (`05` §10).

**Gồm:** `WebsiteTemplate`, lệnh xuất template từ shop nội bộ, khởi tạo website từ template (seed copy
một lần, #45), bộ template đầu tiên cho các lĩnh vực dịch vụ chính. Điền tên shop, số điện thoại, địa chỉ
bằng thay chuỗi — bước này **chưa** cần AI.

**Xong khi:** shop mới chọn một template thì có website đủ trang; mọi ảnh trong đó là `MediaAsset` của
chính shop; sửa template sau đó không đổi website đã tạo (#45).

## A0. Hạ tầng AI

**Chốt trước**
- **Model và nhà cung cấp**; dữ liệu nào của shop được gửi đi.
- **Giới hạn lượt và chi phí theo shop** (liên quan mô hình giá, `02` §6 #8).
- **Thao tác của AI được áp ở đâu.** Operations Engine chỉ có ở client (`builder-core`), BE chỉ validate
  cả cây. Đề xuất: BE là cổng gọi model (giữ khoá, giới hạn lượt, ghi log) và trả về thao tác; Portal áp
  bằng `builder-core` rồi lưu qua API đang có — giữ đúng **một** engine (#14). Phương án kia là port
  engine sang BE để chạy nền.
- **Vị trí module** trong `Docs/architecture/dependency-map.json`.

**Gồm:** module AI ở BE; công cụ cho model sinh từ Registry (`type`/`variant` là enum — `oneOf` theo
type, nợ TOOLING-001); vòng tự sửa khi validate fail (#15 bước 4); log chi phí theo shop.

**Nợ mang theo:** siết `isKnownPropPath`/index của op `update` trước khi AI dùng `dispatch` (WEBSITE-002 m2).

**Xong khi:** một lời gọi thử trả về danh sách thao tác hợp lệ trên một cây mẫu; thao tác sai schema
được tự sửa mà người dùng không thấy lỗi; vượt giới hạn lượt thì bị từ chối với `error_code` riêng.

## A1. Form câu hỏi theo lĩnh vực (#107)

**Gồm**
- Schema form là **dữ liệu**, tập kiểu trường **đóng** (chữ ngắn, chọn một, chọn nhiều, số, ảnh, file).
- Bộ form soạn sẵn cho lĩnh vực phổ biến: AI sinh, developer duyệt, lưu trong repo — cùng quy trình với
  template. Lĩnh vực chưa có sẵn thì AI sinh form lúc chạy theo cùng schema.
- Không hỏi lại thứ hồ sơ shop đã có (tên, địa chỉ, giờ mở cửa).
- Câu trả lời được lưu thành bản tóm tắt có cấu trúc, là đầu vào của A2 và A4.

**Xong khi:** shop thuộc lĩnh vực có sẵn thấy form đã duyệt; lĩnh vực lạ nhận form AI sinh và form đó
qua được validate schema; trả lời xong thì có bản tóm tắt lưu theo shop.

## A2. AI tạo website dịch vụ từ template + câu trả lời (#105, #106)

**Gồm**
- Chọn template theo lĩnh vực → AI sinh thao tác sửa cây (viết lại nội dung, bỏ hoặc đổi thứ tự section,
  chọn theme) → qua đúng lớp validate của `PUT draft`.
- Lượt tạo được ghi cả dữ liệu kinh doanh demo (`ShopServiceGroup`, `Service`) qua **đúng handler mà
  Portal dùng** — tenant isolation và validate không đổi (#106).
- Trạng thái thiết lập trên `Website` và dấu nội bộ "do AI tạo" trên bản ghi dữ liệu kinh doanh. Dấu này
  không hiện cho người dùng; nó để "tạo lại" và "xoá dữ liệu mẫu" không xoá nhầm dữ liệu thật.
- Tạo lại chỉ được khi shop chưa sửa tay và chưa publish.
- Không cảnh báo, không chặn publish nội dung demo (#106).

**Xong khi:** shop dịch vụ trả lời form rồi nhận website nháp đủ trang, có dịch vụ mẫu, mở được trong
editor và publish được; kết quả AI không hợp lệ không bao giờ lọt vào draft; tạo lại chỉ xoá bản ghi có
dấu AI; shop đã sửa tay thì không tạo lại được.

## A3. Chat sửa trong editor (#14–#16)

Dùng **sau** khi đã có website (từ A2 hoặc từ template). Chỉ sinh thao tác trên cây trang — không ghi dữ
liệu kinh doanh (#106).

**Gồm:** pipeline #15 (ngữ cảnh = cây rút gọn + Registry + khối đang chọn), chọn khối trước khi chat
(#16), một lượt chat = một bước undo, nút Giữ / Hoàn tác, AI không chắc thì hỏi lại.

**Nợ mang theo:** chặn phím tắt editor khi có modal (WEBSITE-002 m4); hai lớp undo của RichText (m5/m6).

**Xong khi:** chọn một khối, gõ yêu cầu, thấy thay đổi trên canvas; Hoàn tác một lần về nguyên trạng;
yêu cầu mơ hồ thì AI hỏi lại thay vì sửa bừa.

---

## Marketplace (Phase 1) — sau lõi builder (#96)

Thứ tự phụ thuộc theo `02` §1.2: `ServiceCategory` → `Listing` → `Review`/`Lead`/index ES.

| Bước | Nội dung | Chốt trước |
|---|---|---|
| M0 | Mô hình **ghi** không có tenant context cho `Review`/`Lead` và **đọc xuyên shop** cho tìm kiếm (review kiến trúc P1). Nửa "đọc công khai một shop" đã chốt ở 8b | — chặn mọi bước M |
| M1 | `administrative_unit` (#93) + `ServiceCategory` + màn quản trị taxonomy cho `PlatformAdmin` | |
| M2 | Hồ sơ shop đầy đủ (địa chỉ, toạ độ, giờ mở cửa — `04` §2) · `Listing`: ảnh theo #79/#81 kèm bản TS của hàm tên `thumb_`/`fthumb_` (MEDIA-001 #4), đủ 3 `TargetKind` vì `Page` đã có · **outbox** (REFACTOR-DB-001 #4) · Portal chế độ rút gọn cho shop `ExternalOnly` (`01` §4.2) | |
| M3 | `Lead` — gồm form trên website shop (#47) + component `ContactForm` — và `Review`. Kèm đăng nhập khách trên `vsite.vn` | `02` §6 #10 (SLA khiếu nại) |
| M4 | **Elasticsearch** index + sync qua outbox · tìm kiếm bản đồ · trang hồ sơ `vsite.vn/shop/{slug}` · landing SEO khu vực | |
| M5 | Kiểm duyệt + job kiểm link chết (8a) | `02` §6 #9 |

`Contact` page trước M3 chỉ có thông tin liên hệ, chưa có form.

## 10. `Product` — sau Marketplace, tách ba task

- **10a.** `ShopProductCategory` → `ShopAttribute` → `ShopAttributeOption` → `CategoryAttribute` (#95).
- **10b.** `Product` → `ProductAttributeValue` → `ProductVariant`, ảnh `ImageUrls` (#79–#81).
  **Chốt trước:** UX sinh variant tự động (`05` §25 #6) — quyết định này ảnh hưởng API.
- **10c.** Index + facet + sync trên Elasticsearch, dùng lại outbox (M2) và ES (M4). Kèm `System` page
  `ProductListing`/`ProductDetail`/`ProductSearch` + route (tạo bù cho website đã có), binding
  `ProductGrid`, nguồn menu sản phẩm.

## A4. AI tạo shop có sản phẩm từ file mẫu (#105, #106)

**Chốt trước:** định dạng file mẫu — các sheet danh mục, thuộc tính (kiểu, có lọc hay không), sản phẩm,
biến thể. Ràng buộc đã chốt vẫn giữ: tối đa 3 trục biến thể (#52), thuộc tính `Text` không lọc (#50).

**Gồm:** form A1 có thêm trường tải file mẫu; đọc và validate file, báo lỗi theo dòng; lượt tạo ghi danh
mục, thuộc tính, sản phẩm, biến thể qua handler của 10a/10b, có dấu "do AI tạo" như A2; template cho
shop bán hàng.

**Xong khi:** shop điền file mẫu, tải lên, nhận website nháp có trang danh sách và chi tiết sản phẩm với
đúng dữ liệu trong file; file sai được báo lỗi theo dòng trước khi ghi bất cứ thứ gì.

---

## Trước ra mắt — chưa xếp bước

Những việc dưới đây không thuộc bước nào ở trên nhưng thiếu thì không ra mắt được. Phải xếp chỗ trước M5.

| Việc | Hiện trạng |
|---|---|
| Gửi email thật | Chỉ có `LoggingEmailSender`. Kèm: link verify/reset trỏ về trang FE thay vì API JSON (REFACTOR-API-001 #4), handoff verify-email từ domain shop (IDENTITY-001 §C) |
| Deploy production | Chưa có: wildcard TLS `*.vsite.vn`, `UseForwardedHeaders` (#101), object storage S3, backup. Lần deploy đầu là mốc đảo #89 và #102 |
| Đăng nhập social / Zalo (#6) | Schema `ExternalLogin` đã có, luồng OAuth chưa. Chờ `02` §6 #5 |
| Tên miền riêng (#9, #10) | Phase 3. Cần bảng `ShopDomain` + Caddy On-Demand TLS |

## Đa ngôn ngữ (#98, `05` §26)

Thiết kế xong rồi rải vào các bước. **Cái nào đổi sau là phải migrate hoặc vỡ URL thì làm sớm.**

| Ở đâu | Việc |
|---|---|
| 5a | `Website.DefaultLocale` + `Locales`, `config/locales.json`, chặn `Slug` trùng mã locale |
| 6 | Renderer nhận `locale` + `defaultLocale` |
| 8b | Snapshot mang `defaultLocale` + `locales` |
| 9 | Chữ ký Binding Resolver nhận `locale` |
| **L** (chưa xếp) | **Bật tính năng:** `page_translation`, overlay `i18n` trên node, nhãn menu theo locale, UI dịch trong editor, URL `/{locale}/…` + `hreflang` |
