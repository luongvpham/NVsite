# WEBSITE-001 — contract diff

## ⚠️ BREAKING / REMOVED
- **BREAKING** `GET /api/shops/{shopId}/media/library/{assetId}/references` (media.v1.json) — chưa deploy production thì cứ sửa BE + regen FE, KHÔNG tạo v2:
  - ~ schema 'MediaReferenceKind' đổi (enum/type/items/allOf…)

## NEW_ENDPOINT
- GET /api/shops/{shopId}/website
- POST /api/shops/{shopId}/website
- GET /api/shops/{shopId}/website/pages
- POST /api/shops/{shopId}/website/pages
- DELETE /api/shops/{shopId}/website/pages/{pageId}
- PUT /api/shops/{shopId}/website/pages/{pageId}
- GET /api/shops/{shopId}/website/pages/{pageId}/draft
- PUT /api/shops/{shopId}/website/pages/{pageId}/draft
- GET /api/shops/{shopId}/website/theme
- PUT /api/shops/{shopId}/website/theme

## ADDITIVE
(không có)

## UNCHANGED
9 operation không đổi.

## Auth policy
- **Mọi** endpoint `website` (kể cả GET): `RequireShopOwner("WEBSITE_OWNER_REQUIRED")` — #100.
- **Mọi** endpoint `media` (10 route) đổi từ member → `RequireShopOwner("MEDIA_OWNER_REQUIRED")`. Shape contract không đổi,
  nhưng **hành vi đổi**: Manager/Staff/Accountant/Customer nhận 403. Thêm `403` vào `responses` nơi còn thiếu.

## `media.v1` — thay đổi (nhãn BREAKING của tool chỉ do thêm giá trị enum)
- `MediaReferenceKind` thêm `PageDraft`; `MediaReferenceDto` thêm `pageId?`, `pageTitle?` (additive, nullable).

## Orval thử trên staging (không đụng `generated/`)
Chạy được cho `website` + `media`; **không có** type trùng `…Dto2`. `JsonElement` ra `export interface JsonElement {}`.

## Giả định tôi đã tự đặt (không hỏi)
1. `tree` (savePageDraft) và `tokens` (updateTheme / getTheme) là `JsonElement` → schema rỗng `{}`; FE 5b dùng Zod của
   `packages/builder-components` để kiểu hoá, không dựa vào type Orval.
2. `updatePage` là **PUT thay toàn bộ** (form "Thuộc tính trang" gửi đủ trường), không phải PATCH.
3. `createPage` trả **201 + Location**; `createWebsite` trả 201.
4. `errors[]` `{path, code, message}` nằm ở ProblemDetails **`params.errors[]`** (+ `params.truncated`); `errors` dùng chung giữ nguyên.
5. `409 DRAFT_CONFLICT` kèm `params.currentVersion`, `updatedAt`, `updatedByUserId`; **không** trả tree.
6. `PUT draft` trả `{version, isDirty, sanitized}`, không trả lại tree; `sanitized=true` khi BE làm sạch richText.
7. `version` = `xmin` đổi sang số nguyên (opaque với FE, gửi lại nguyên văn).
8. `listPages` khi chưa có website trả `200 []` (OpenAPI vẫn khai 404 — thừa); chỉ `getWebsite` trả `404 WEBSITE_NOT_FOUND`.
9. Mã lỗi mới: `WEBSITE_OWNER_REQUIRED`, `WEBSITE_NOT_FOUND`, `WEBSITE_ALREADY_EXISTS`, `WEBSITE_REQUIRES_HOSTED_SHOP`,
   `PAGE_NOT_FOUND`, `PAGE_SLUG_TAKEN`, `PAGE_SLUG_RESERVED`, `PAGE_NOT_DELETABLE`, `TREE_INVALID`, `THEME_INVALID`,
   `TREE_BODY_TOO_LARGE` / `THEME_BODY_TOO_LARGE`, `DRAFT_CONFLICT`, `RICHTEXT_UNSAFE`, `LINK_URL_UNSAFE`,
   `SCHEMA_VERSION_UNSUPPORTED`, `CONCURRENCY_CONFLICT`, `CONFLICT_UNIQUE`, `TENANT_REQUIRED`; mã chi tiết trong `params.errors[].code`:
   xem `packages/builder-components/tests/fixtures/tree/README.md`.

## Câu hỏi cần anh quyết (→ dừng Gate 1, chưa promote, chưa viết `brief.md`)
1. **Promote cần tên người duyệt** (`promote.mjs WEBSITE-001 <approvedBy> website.v1.json media.v1.json`) — tôi không tự điền.
2. Chấp nhận `JsonElement` (schema rỗng) cho `tree`/`tokens`, hay muốn khai schema vỏ trong OpenAPI (nặng, lệch #60/#97)?
3. Trần `THEME_BODY_TOO_LARGE` = 64 KB và tree = `MaxBodyBytes` 1 MB + 4096 byte biên (413 vượt biên, 422 trong biên) — ngoài plan, giữ không?
4. `updatePage` = PUT (đề xuất) hay PATCH như plan Flow 3?
5. Media owner-only đổi hành vi cho Manager/Staff đang dùng được — xác nhận (#100).
