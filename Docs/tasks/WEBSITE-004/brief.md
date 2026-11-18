# WEBSITE-004 — FE brief (Bước 8c: `apps/web` render site + màn publish Portal)

## Contract

Đã promote (Gate 1 gọn, #89) — `contracts/contract.lock`; SDK đã `pnpm gen:api`.
`contracts/openapi/website.v1.json` + `contracts/openapi/media.v1.json`. Chi tiết thay đổi: [`contract-diff.md`](contract-diff.md).

| operationId | Route | Ai gọi |
|---|---|---|
| `publishWebsite` | `POST /api/shops/{shopId}/website/publications` body `{ note? }` → `201 PublicationDto` | Portal (Owner) |
| `listPublications` | `GET /api/shops/{shopId}/website/publications` → `PublicationDto[]` (≤ 50, version giảm dần, không kèm snapshot) | Portal (Owner) |
| `rollbackPublication` | `POST /api/shops/{shopId}/website/publications/{publicationId}/rollback` → `200 PublicationDto` | Portal (Owner) |
| `getPublishedSite` | `GET /api/site` → `200 PublishedSiteDto` · `304` · `404 SITE_NOT_FOUND` | **`apps/web` SSR**, không token |

`PublicationDto = { id, version, publishedAt, publishedByUserId?, note?, isCurrent }`. `WebsiteDto` có thêm `currentPublicationId` (nullable).

---

## A. Đọc site công khai (`apps/web`, SSR)

### 1. Tenant đến từ header `Host`, không từ tham số (#108)

`GET /api/site` **không có** `shopId`/`host` ở path, query hay body — thêm vào cũng bị BE bỏ qua hoàn toàn. BE suy shop từ **nhãn đầu của header `Host`**
(`spa-abc` trong `spa-abc.vsite.vn`), như mọi request khác (`TenantResolutionMiddleware`).

- SSR lấy host từ **URL gốc của request trình duyệt** (subdomain `{slug}.vsite.vn`) rồi gọi BE với header `Host` bằng đúng host đó.
  `createApiClient` đã nhận `headers` (`packages/api-sdk/src/mutator/axios-instance.ts`).
- **Chưa kiểm thật:** gửi `Host` tự đặt từ Node (fetch/undici/axios có thể bỏ hoặc ghi đè `Host`) — **kiểm sớm ở 8c**, trước khi dựng loader. Dev proxy
  (`apps/web/vite.config.ts`, `changeOrigin: false`) giữ nguyên Host của trình duyệt nên chạy dev bằng `{slug}.vsite.local` là đủ.
- **Custom domain (`spa-abc.com`) CHƯA hoạt động ở BE:** middleware chỉ tra `Shop.Slug` từ nhãn đầu; bảng `ShopDomain` chưa có (G2). 8c chỉ cần chuyển host đúng cách.
- Không gửi token. Có gửi token global thì kết quả y hệt; token của một shop khác trên Host này bị BE chặn `403 TOKEN_SHOP_MISMATCH` — SSR **đừng** forward cookie/token của khách.
- Đừng forward `X-Forwarded-Host`/`Forwarded` để "chọn shop": BE bỏ qua.

### 2. `ETag` / `304`

- Response `200` (trạng thái `Published`) có `ETag` (strong, `"{publicationId}-{shop.UpdatedAt ms}"`) và `Cache-Control: no-cache`. **Hai header này không có trong OpenAPI** — đọc từ `response.headers`.
- SSR gửi lại `If-None-Match: <ETag đã giữ>`; khớp → `304` không body, vẫn có `ETag` + `Cache-Control`. Chấp nhận `W/"…"`, danh sách phân tách dấu phẩy, và `*`.
- ETag đổi khi **publish/rollback** *hoặc* khi **đổi tên/logo shop** (dữ liệu `shop` sống nằm cùng response). Vì vậy 304 là an toàn, đừng tự suy "snapshot không đổi nên giữ cache".
- **Response `Suspended` KHÔNG có `ETag`** ⇒ không bao giờ 304; luôn render lại.
- HTML không cache ở CDN (#109); cache ở tầng dữ liệu (ETag + con trỏ Redis phía BE).

### 3. Trạng thái

| Tình huống | Response |
|---|---|
| Đã publish | `200 { status: "Published", shop, publication: { id, version, publishedAt, snapshot } }` |
| Website `Suspended` **hoặc** shop `Suspended` | `200 { status: "Suspended", shop, publication: null }` — **không có snapshot**, không ETag. Render trang "tạm ngưng" |
| Host không phải shop (`admin.…`, host chính, `www.`), slug không tồn tại, shop `Closed`, chưa có website, website `Draft`/chưa publish lần nào | `404 SITE_NOT_FOUND` — **một mã duy nhất** (G4). Đừng cố phân biệt; render một trang 404 chung |

Shop ở trạng thái `Draft` **không** chặn site (shop mới tạo mặc định `Draft`).

### 4. `snapshot` là `unknown` — parse bằng Zod

`PublishedSiteDto.publication.snapshot` ra schema rỗng trong OpenAPI (cùng tiền lệ `tree`/`tokens`). Parse bằng **`siteSnapshotSchema`** từ `@vsite/builder-components`
(đã export, kèm type `SiteSnapshot`); parse lỗi = coi như lỗi 500 phía SSR (BE bảo đảm hợp lệ theo schema này, có test).

Shape v1 (**khác `05` §7**): `schemaVersion`, `version`, `defaultLocale`, `locales`, `theme { presetKey, tokens }`, `navigation { items: [] }` (rỗng tới Bước 7),
`seo { defaultTitle, defaultDescription, ogImageId, faviconAssetId }`, `tracking`, **`media`**, `pages[]`.

- `pages[]` đã sắp xếp (`SortOrder`, rồi `Title`); mỗi trang có **`id`** (link nội bộ trong tree trỏ theo id), `slug` (`""` = trang chủ, **`null` = trang `NotFound`**), `kind`, `systemType`, `title`, `seo`, `tree`.
- **Chỉ có trang đang bật.** Trang chủ có thể vắng (G10) → hiện `NotFound`.
- **`snapshot.media` thay cho `mediaMap`**: `imageId → { storageKey, preset }`. `imageId` trong tree và khoá `media` đều là GUID **chữ thường** và khớp nhau — tra thẳng, không cần chuẩn hoá. `preset = null` nghĩa là file gốc
  (ảnh OG/favicon: G9, chưa có phái sinh `1200x630`; dùng file gốc cho `og:image`).
- Ảnh đã xoá mềm sau publish **vẫn có** trong `media` (file sống tới job prune 8d) — render bình thường.
- `shop = { name, logoUrl }` là dữ liệu sống, không nằm trong snapshot.
- **BE không trả năm © và không trả anchor `#nodeId`** (G3): `SiteLayout` nhận `year` từ bên gọi; anchor là việc của renderer — cả hai thuộc 8c.
- Snapshot bất biến: sửa draft/theme/SEO/xoá trang sau publish **không** đổi response này.

---

## B. Màn publish trong Portal

### 5. Publish

`POST …/website/publications` body `{ note? }` (tuỳ chọn, ≤ 200 ký tự sau trim; chuỗi chỉ có khoảng trắng → `null`). `201` + `PublicationDto`.
Header `Location` có nhưng trỏ tài nguyên chưa có `GET` — **dùng body**.

- Publish luôn được phép dù không draft nào bẩn (G5). Website `Draft` → thành `Published`; `Suspended` → bản mới vẫn ghi nhưng giữ `Suspended` (G6).
- **`422 PUBLISH_BLOCKED`** — chặn cả lần publish, không ghi gì. ProblemDetails có `error_code: "PUBLISH_BLOCKED"` và:

  ```json
  "params": {
    "errors": [
      { "pageId": "…|null", "pageTitle": "…|null", "path": "/children/0/props/image", "code": "REQUIRED_PROP_MISSING", "message": "…" }
    ],
    "truncated": false
  }
  ```

  - `code` ∈ `REQUIRED_PROP_MISSING` (prop bắt buộc theo variant), `MEDIA_INVALID_IMAGE_REFERENCE` (ảnh đã xoá/không thuộc shop — báo theo **trang**, `path = ""`, không chỉ ra node, G12), và các mã cấu trúc cây của validator.
  - `pageId`/`pageTitle` = **`null`** với lỗi cấp website (favicon / OG mặc định); các lỗi này đứng **đầu** danh sách.
  - Tối đa 100 mục; `truncated = true` khi bị cắt (hoặc validator tự dừng sớm) — hiển thị "còn lỗi khác".
  - **Lỗi có thể đến hai đợt:** trang có cây sai cấu trúc chỉ báo lỗi cấu trúc trước; sửa xong mới tới lỗi prop/ảnh. Cho phép publish lại sau mỗi lần sửa.
  - Trang đang **tắt** không bị kiểm.
- `409 PUBLISH_CONFLICT` — hai lần publish cùng lúc hoặc draft bị lưu giữa chừng; **không tự thử lại**, báo người dùng bấm lại.
- `404 WEBSITE_NOT_FOUND` (chưa có website) · `403 WEBSITE_OWNER_REQUIRED` · `422` nếu `note` quá dài.

### 6. ⚠️ Version của draft đổi sau publish (R9)

Publish hạ cờ `isDirty` của các draft đang bẩn ⇒ dòng `page_draft` bị ghi ⇒ **`version` của draft đó đổi**. Response publish **không** trả version mới.
Editor đang mở mà autosave bằng version cũ sẽ nhận `409 DRAFT_CONFLICT`. **Sau publish (và sau rollback), FE phải nạp lại draft/pages** (query lại để lấy `version` mới)
trước khi cho autosave tiếp. Draft vốn sạch không bị ghi nên version không đổi.

### 7. Rollback & danh sách

- `POST …/{publicationId}/rollback` → `200 PublicationDto` của bản đang chạy. **Không sinh bản mới**, không đụng nội dung draft; mọi draft đang sạch bị đánh `isDirty = true`
  (bản nháp giờ khác bản đang chạy, G7) ⇒ cũng **nạp lại version** như mục 6. Rollback về chính bản đang chạy: `200`, không ghi gì.
- Bản của shop khác / không tồn tại → `404 PUBLICATION_NOT_FOUND`. `409 ROLLBACK_CONFLICT` nếu draft bị lưu giữa chừng (thêm ngoài plan — xem `contract-diff.md`).
- Publish sau rollback → `version = max + 1` (không tái dùng số).
- `GET …/publications`: trần 50, không phân trang (G11); đúng một mục `isCurrent`. `WebsiteDto.currentPublicationId` cũng cho biết bản đang chạy.
- Không có endpoint gỡ xuất bản / đình chỉ website ở 8b.

---

## C. Xoá ảnh Library — cảnh báo đang dùng

`GET …/media/library/{assetId}/references` nay có thêm **4 `kind`**:

| `kind` | Ý nghĩa | Field kèm |
|---|---|---|
| `SitePublication` | Ảnh nằm trong một hoặc nhiều bản publish (gộp thành **một** mục) | **`isCurrent`**: `true` nếu bản đang chạy dùng ảnh; `false` nếu chỉ bản cũ |
| `PageOgImage` | Ảnh OG của một trang (mỗi trang một mục) | `pageId`, `pageTitle` |
| `WebsiteOgImage` | Ảnh OG mặc định của website | — |
| `WebsiteFavicon` | Favicon của website | — |

- `isCurrent` là `bool?` — chỉ có với `SitePublication`, các kind khác `null`.
- Cùng ảnh làm cả OG lẫn favicon ⇒ **hai** mục. Ảnh OG/favicon cũng xuất hiện thêm ở `SitePublication` nếu đã publish (hai mục khác loại là chủ đích).
- **Xoá library KHÔNG kéo theo clone** (R11): ảnh trong site đã publish **không** biến mất; `references` có nhiệm vụ cảnh báo *trước* khi xoá. Có `SitePublication` + `isCurrent = true` ⇒ cảnh báo mạnh (site đang chạy dùng ảnh);
  `isCurrent = false` ⇒ cảnh báo nhẹ (chỉ bản cũ, còn rollback được).

---

## D. Việc FE tự kiểm / chưa có

- Kiểm `Host` tự đặt từ Node (mục A.1) — 8c.
- Anchor `#nodeId`, năm © — 8c (G3).
- `navigation.items` rỗng cho tới Bước 7; dữ liệu bind `Service`/`Product` là Bước 9.
- Prune 20 bản là 8d; custom domain là sau (G2).
