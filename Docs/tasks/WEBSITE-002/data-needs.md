# WEBSITE-002 — Nhu cầu dữ liệu (Bước 5b, FE)

> Contract đã chốt ở WEBSITE-001 (`website.v1`, `media.v1`). File này đối chiếu **từng màn** với field
> có sẵn để chắc không cần vòng `WEBSITE-001-D{n}`. Mọi field dưới đây **đã có** trong contract.

## Màn: Tổng quan Website (`/shops/$shopId/website`)
**Toàn trang:** `getWebsite` → `WebsiteDto.status`, `homePageId`, `defaultLocale` (chỉ hiển thị). 404
`WEBSITE_NOT_FOUND` = empty "Tạo website". `createWebsite` không body.

## Màn: Danh sách trang (cùng route)
**Mỗi item** (`listPages` → `PageDto[]`, không phân trang): `id`, `title`, `slug` (null với NotFound),
`kind`, `systemType`, `isHomePage`, `isEnabled`, `noIndex`, `isDirty`, `deletable`, `updatedAt`.
Sắp theo `sortOrder` (chỉ đọc). Empty: chỉ có trang chủ + System → gợi ý "Tạo trang".

## Màn: Thuộc tính trang (dialog)
`CreatePageRequest`/`UpdatePageRequest`: `slug`, `title`, `seoTitle?`, `seoDescription?`, `ogImageId?`
(bản Library), `noIndex`, `isEnabled`. `UpdatePage` thay toàn bộ, `slug = null` = không đổi. Ảnh OG hiển
thị qua `getAssetsByIds` (media).

## Màn: Editor (`/shops/$shopId/website/pages/$pageId`)
`getPageDraft` → `tree` (Zod `treeEnvelopeSchema`), `version` (opaque), `isDirty`, `updatedAt`,
`page {id, kind, systemType}`. `savePageDraft {tree, version}` → `{version, isDirty, sanitized}`. Lỗi:
`ProblemDetails.error_code` + `params.errors[] {path, code, message}`, `params.truncated`;
409 `params.currentVersion|updatedAt|updatedByUserId`. Tiêu đề trang lấy từ `listPages` (draft không có
`title`). Ảnh trong cây: `getAssetsByIds` để dựng `mediaMap`.

## Màn: Theme panel (tab trong editor)
`getTheme` → `presetKey?`, `tokens` (Zod `themeTokensSchema`), `updatedAt`. `updateTheme {presetKey?,
tokens}`. Tập giá trị: `config/theme-tokens.json`.

## Câu hỏi mở
| # | Câu hỏi | Quyết định |
|---|---|---|
| 1 | Draft không có `title` — editor lấy tên trang ở đâu? | Từ cache `listPages` (cùng shop, đã tải) — không cần field mới |
| 2 | Có cần endpoint sắp xếp trang (`sortOrder`)? | Không trong 5b — chỉ đọc |
| 3 | `presetKey` lấy giá trị từ đâu? | Chưa có danh mục preset → gửi lại nguyên giá trị đang có |
| 4 | 409 hoà giải có cần tree trong ProblemDetails? | Không — `GET draft` lại (brief) |
