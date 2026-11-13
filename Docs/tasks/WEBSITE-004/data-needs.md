# WEBSITE-004 (Bước 8b) — nhu cầu dữ liệu

> Task **backend-only**. File này viết từ góc nhìn bên dùng contract: `apps/web` (8c) và màn publish của
> Portal (chưa xếp task). Không viết contract ở đây — chỉ nói bên dùng cần gì. Tên endpoint là đề xuất,
> chốt ở Gate 1.

## `apps/web` — render một trang của Shop Site (8c)

Một lượt gọi `GET /api/site` (không token, tenant theo header `Host`) phải đủ để SSR render bất kỳ trang nào:

- `status`: `Published` | `Suspended`. (`Draft` không bao giờ xuất hiện — là 404.)
- `shop`: `name`, `logoUrl?` — cho `SiteLayout` (`packages/builder-renderer/src/site-layout.tsx`).
- `publication` (null khi `Suspended`):
  - `id` (cũng là `ETag`), `version`, `publishedAt`
  - `snapshot`: `schemaVersion`, `version`, `defaultLocale`, `locales[]`, `theme` (tokens),
    `navigation.items` (rỗng tới Bước 7), `seo` (`defaultTitle`, `defaultDescription`, `ogImageId?`,
    `faviconAssetId?`), `tracking` (`ga4?`, `metaPixel?`), `pages[]`.
  - Mỗi `pages[]`: **`id`**, `slug` (`""` = trang chủ, `null` = `NotFound`), `kind`, `systemType?`,
    `title`, `seo` (`title?`, `description?`, `ogImageId?`, `noIndex`), `tree`.
  - Bảng ảnh: `imageId → { storageKey, preset }` cho mọi ảnh trong tree + ảnh OG + favicon — xem câu hỏi 1.
- Header: `ETag`; request có `If-None-Match` khớp → `304` không body.
- Lỗi cần phân biệt: `404 SITE_NOT_FOUND` (host lạ / chưa publish). Không có 401/403.

`pages[].id` **không có** trong shape của `05` §7 nhưng bắt buộc: link nội bộ trong tree trỏ theo id trang,
renderer tra qua `SitePageRef { id, slug, systemType, isEnabled }`
(`packages/builder-components/src/site-url.ts`). Trang tắt không có trong snapshot → link tới nó ra `NotFound`.

## Portal — nút "Xuất bản" (task FE sau)

- Vào: `note?` (tối đa 200 ký tự). Ra: `{ id, version, publishedAt, note }`.
- Thất bại `422 PUBLISH_BLOCKED`: `params.errors[]` mỗi phần tử `{ pageId, pageTitle, path, code, message }` —
  `path` là JSON Pointer trong tree của trang đó, để Portal mở đúng trang và tô đúng ô. Mã con dùng lại
  bộ mã của `PUT draft` + mã mới `REQUIRED_PROP_MISSING`.
- `409 PUBLISH_CONFLICT` khi tab khác vừa publish.
- Sau publish, `GET pages` trả `isDirty = false` cho các trang vừa xuất bản (field đã có trong `PageDto`).
- `WebsiteDto` cần thêm `currentPublicationId?` và (đã có) `status` để Portal biết site đã lên chưa.

## Portal — lịch sử xuất bản + rollback (task FE sau)

- Danh sách: `id`, `version`, `publishedAt`, `publishedByUserId`, `note?`, `isCurrent`. Mới nhất trước,
  không phân trang, không kèm snapshot.
- Rollback: vào `publicationId` (route); ra thông tin bản đang chạy. `404 PUBLICATION_NOT_FOUND`.

## Portal — xoá ảnh khỏi Media Library (đã có màn)

`references` báo thêm: "đang dùng trong bản đã xuất bản" (`kind = SitePublication`), "ảnh OG của trang X"
(`kind = PageOgImage`, kèm `pageId` + `pageTitle`), "ảnh OG mặc định / favicon của website"
(`kind = WebsiteOgImage` / `WebsiteFavicon`).

---

## Câu hỏi mở — ĐÃ CHỐT (2026-11-13, tôi chọn theo uỷ quyền "chọn phương án tối ưu"; người đọc lại ở pha 6)

| # | Câu hỏi | Chốt |
|---|---|---|
| 1 | Bảng ảnh `imageId → storageKey` cho site công khai lấy ở đâu? Endpoint tra ảnh hiện có là Owner-only (#100) | **Đóng băng trong snapshot lúc publish** (`snapshot.media`: `imageId → { storageKey, preset }`), gồm ảnh trong tree + ảnh OG + favicon. `storageKey` không đổi, đọc công khai không tốn query Media, `references` chỉ cần đọc bảng này |
| 2 | Shop không còn `Active` thì site công khai ra sao? | `Shop.Suspended` → như website `Suspended`; `Shop.Closed` → `404 SITE_NOT_FOUND`; **`Shop.Draft` không chặn** — shop mới tạo mặc định `Draft` (`backend/src/Vsite.Domain/Shop/Entities/Shop.cs:30`) và chưa có luồng kích hoạt, chặn thì mọi site mới đều 404 |
| 3 | `references` có báo ảnh nằm trong bản publish cũ không? | **Có, gộp một mục** `SitePublication` kèm `isCurrent`. Xoá ảnh đó thì rollback về bản cũ sẽ hỏng ảnh |
| 4 | Đổi tên/logo shop có cần publish lại mới thấy không? | **Không.** `shop` là dữ liệu sống ngoài snapshot; `ETag` gồm cả `Shop.UpdatedAt` nên SSR nhận được thay đổi |
| 5 | Sau publish, editor đang mở có cần làm gì? | **Có: nạp lại `version` của draft.** Publish hạ cờ `isDirty` nên `version` của các draft vừa publish đổi; autosave bằng version cũ nhận `409 DRAFT_CONFLICT` |
