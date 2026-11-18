# WEBSITE-004 — contract diff

> Gate 1 gọn trước production (#89). Sinh bằng `node tools/contract-sync/diff-report.mjs WEBSITE-004 website media`
> từ `pnpm contract:export website media`; phần dưới "Nhãn tool" là nguyên văn output, phần còn lại viết tay.
> Đối chiếu tay: `git diff --no-index` giữa `contracts/openapi/{module}.v1.json` và `.staging/` — **chỉ có dòng thêm**, ngoại trừ một dấu phẩy
> sau `"homePageId"` trong mảng `required` của `WebsiteDto` (hệ quả của việc thêm phần tử). Không `REMOVED`, không đổi kiểu, không đổi tên.
> Thử Orval trên file staging (thư mục tạm): không có type `…Dto2`.

## Nhãn tool (chỉ để tham khảo)

- **BREAKING** `GET /api/shops/{shopId}/media/library/{assetId}/references` — `MediaReferenceKind` đổi
- **BREAKING** `GET` + `POST /api/shops/{shopId}/website` — `WebsiteDto.currentPublicationId`
- Ba nhãn trên là **nợ classifier TOOLING-001** (thêm field/giá trị enum vào DTO *response* bị coi là phá vỡ). Đối chiếu tay: cả ba là additive cho consumer
  (field response mới, giá trị enum mới). Lưu ý nhỏ: `currentPublicationId` nằm trong `required` nhưng `nullable` (cùng kiểu với `homePageId`/`defaultOgImageId` hiện có).
- `isCurrent` (media) **không** hiện trong báo cáo tool dù đã có trong staging — chỉ `MediaReferenceKind` được nêu; cùng một thay đổi, đã đối chiếu tay.

## Thêm mới

### `website.v1.json` — 4 operation, 5 schema

| Operation | Route | Auth | Status khai báo |
|---|---|---|---|
| `publishWebsite` | `POST /api/shops/{shopId}/website/publications` | `RequireGlobalScope` + `RequireOwner` (#100) | 201 · 401 · 403 · 404 · 409 · 422 |
| `listPublications` | `GET /api/shops/{shopId}/website/publications` | như trên | 200 · 401 · 403 · 404 |
| `rollbackPublication` | `POST /api/shops/{shopId}/website/publications/{publicationId}/rollback` | như trên | 200 · 401 · 403 · 404 · **409** |
| `getPublishedSite` | `GET /api/site` | **`AllowAnonymous`**; tenant suy từ header `Host` (#108), không có tham số định danh shop | 200 · 304 · 404 |

Schema mới: `PublicationDto`, `PublishWebsiteRequest`, `PublishedSiteDto`, `PublishedShopDto`, `PublishedPublicationDto`.
`PublishedPublicationDto.snapshot` ra schema rỗng (`JsonElement`, cùng tiền lệ `tree`/`tokens` — FE parse bằng Zod `siteSnapshotSchema`).
`PublishedSiteDto.publication` nullable (null khi `Suspended`). Field thêm: `WebsiteDto.currentPublicationId` (`uuid`, nullable).

### `media.v1.json`

- `MediaReferenceKind` + 4 giá trị: `SitePublication`, `PageOgImage`, `WebsiteOgImage`, `WebsiteFavicon`.
- `MediaReferenceDto.isCurrent` (`boolean`, nullable; chỉ có với `SitePublication`).

## Giả định tôi đã tự đặt (không hỏi)

1. **Header `ETag` / `Cache-Control: no-cache` của `GET /api/site` KHÔNG được khai báo trong OpenAPI** (chỉ có `200`/`304` không header). Chúng đặt lúc chạy; FE đọc qua
   `response.headers`. Chưa có tiền lệ khai header trong contract hiện có. Ghi trong `brief.md`.
2. **Một mã `404 SITE_NOT_FOUND`** cho mọi trường hợp "không có site" (G4). `ProblemDetails` không liệt kê mã trong schema (như mọi endpoint khác).
3. **`PUBLISH_BLOCKED` (422)**: `params.errors[]`/`params.truncated` không mô tả trong OpenAPI (tiền lệ `TREE_INVALID`); shape nằm ở `brief.md`.
   `pageId`/`pageTitle` nullable (lỗi cấp website: favicon / OG mặc định).
4. **`409 ROLLBACK_CONFLICT`** ở rollback — plan chỉ ghi 200/401/403/404; thêm vì draft bị lưu giữa lúc đọc và lúc ghi cờ thì trả xung đột thay vì 500 (changelog T5).
5. **`Location` của `201` publish** trỏ `/api/shops/{shopId}/website/publications/{id}` dù chưa có `GET` một bản; header không khai trong OpenAPI. FE dùng body (changelog T4).
6. `If-None-Match: *` cũng cho 304; response `Suspended` không có `ETag` nên không bao giờ 304 (changelog T6).
7. Cùng ảnh làm OG lẫn favicon của website ⇒ `references` trả **hai** mục (`WebsiteOgImage` + `WebsiteFavicon`) — sẽ ghi `DECISIONS.md` nếu Gate 1 duyệt.
8. `references` gộp mọi bản publish thành tối đa **một** mục `SitePublication`, `isCurrent` = bản đang chạy có dùng ảnh.

Cả 4–5 đều là mở rộng thêm ở response (không đổi shape đã có); sửa được trước production không cần `v2` (#89, #102).

## Câu hỏi cần anh quyết

Không có câu hỏi chặn. Các mục 4, 5, 7 ở trên là chỗ người duyệt có thể đổi ý — đổi trước production thì sửa BE + regen, không `v2`.

## Người duyệt đã quyết

| Mục | Quyết định | Ngày |
|---|---|---|
| Promote theo Gate 1 gọn (#89: chỉ thêm, không câu hỏi, không `REMOVED`) | Promote ngay; người duyệt đọc lại file này sau | 2026-11-18 |
| Giả định 7 — cặp OG/favicon báo hai mục | Duyệt → **#110** | 2026-11-18 |
| Giả định 4 — `409 ROLLBACK_CONFLICT` | Giữ. Nay có nghĩa thật: `Website` có concurrency token, rollback chạy song song với publish nhận 409 (review I1). Không đổi contract (409 đã khai) | 2026-11-18 |
| Giả định 5 — header `Location` của `201` publish | Giữ — cùng quy ước `createPage` (`Location` trỏ `/pages/{id}`, cũng không có `GET` một trang). FE dùng body. (Đề xuất bỏ header bị rút lại khi rà lần hai) | 2026-11-18 |
| `listPublications` trả mảng (review I2) | Giữ mảng có trần cứng, ghi ngoại lệ của #19 ở `backend/docs/modules/website.md` | 2026-11-18 |
