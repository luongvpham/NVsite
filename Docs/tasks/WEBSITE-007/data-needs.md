# WEBSITE-007 (Bước 7) — nhu cầu dữ liệu

> Viết từ góc nhìn bên dùng: màn Menu của Portal, preview Portal, và `apps/web`. Không viết contract ở
> đây — tên endpoint/field là đề xuất, chốt ở Gate 1. Quyết định nền: #113 (`DesignIdeal/DECISIONS.md`).

## Portal — màn "Menu" (khu website)

**Đọc** (`GET /api/shops/{shopId}/website/navigation`, Owner-only), toàn trang:

- `version`: số nguyên hoặc `null` (chưa từng lưu — menu đang là bản tự sinh). Gửi lại khi lưu.
- `isDirty`: menu có thay đổi chưa publish. Chưa từng lưu ⇒ `true`.
- `items[]` — **đã ghép** (#113): tự sinh nếu chưa có; trang chưa có trong menu nối cuối với `hidden: true`;
  mục trỏ trang đã xoá bị bỏ. Mỗi mục:
  - `key`: `"page:{pageId}"` hoặc `"custom"`
  - `label`: chuỗi hoặc `null` (`null` = dùng tên trang; `custom` bắt buộc có)
  - `url`: chỉ có ở `custom`
  - `hidden`: bool
  - `children[]`: cùng shape, lồng **tối đa 3 cấp** (cấp 3 không có `children`) — ba kiểu DTO tường minh, xem plan §4

Nhãn hiển thị cho `page:{id}` khi `label` null, cờ "trang đang tắt", "trang chủ": Portal lấy từ
`GET …/website/pages` **đã có** (`PageDto.title`, `isEnabled`, `isHomePage`, `systemType`) — không cần
field mới. Trang đang tắt vẫn nằm trong menu; Portal gắn nhãn "trang đang tắt — không hiện trên site".

**Lưu** (`PUT …/website/navigation`), vào `{ version, items }` (shape như trên), ra `{ version, isDirty }`:

- `409 NAVIGATION_CONFLICT` khi `version` lệch (kể cả gửi `null` mà đã có bản lưu) — params như
  `DRAFT_CONFLICT`: `currentVersion`, `updatedAt`.
- `422` lỗi theo mục ở `params.errors[]` `{ path, code, message }`, `path` là JSON Pointer vào `items`
  (vd. `/items/2/children/0/url`) để Portal tô đúng dòng. Mã con: `NAV_TOO_DEEP`, `NAV_PAGE_NOT_FOUND`,
  `NAV_PAGE_DUPLICATE`, `NAV_PAGE_NOT_ALLOWED` (`NotFound`), `NAV_URL_UNSAFE`, `NAV_LABEL_REQUIRED`,
  `NAV_LABEL_TOO_LONG`, `NAV_TOO_MANY_ITEMS`. Tên chốt ở Gate 1.
- Giới hạn: ≤ 12 mục cấp 1, ≤ 10 mục con mỗi mục (cấp 2 và cấp 3), nhãn ≤ 60 ký tự, URL ≤ 2048.

## Portal — tổng quan website + dialog publish

- Biết "menu chưa publish" để nhắc: đọc `isDirty` từ `GET …/navigation` ở trên. Không cần field mới
  ở `WebsiteDto`.
- Sau publish / rollback: refetch `GET …/navigation` (publish hạ `isDirty`, rollback bật lại).

## Preview Portal

- Dựng menu từ `items` đang sửa (chưa lưu cũng được) + `listPages`, qua cùng hàm `resolveNavigation`
  như `apps/web`; trang đang tắt bị bỏ như trên site.

## `apps/web` — `Header` của Shop Site

Trong `GET /api/site` (không đổi endpoint), `publication.snapshot.navigation`:

- `items[]` chỉ gồm mục **đang hiện**, thứ tự = vị trí mảng. Mỗi mục `{ key, label: string|null, url?,
  children?[] }` — **không** có `hidden`.
- Ghép với `snapshot.pages[]` (đã có `id`, `slug`, `systemType`, `title`): nhãn = `label ?? page.title`,
  URL qua `createResolveUrl`. Trang không có trong `pages` (tắt/xoá trước lúc publish) ⇒ bỏ mục; cha
  bị bỏ ⇒ bỏ cả nhóm con.
- `schemaVersion` của snapshot giữ **1**: `items` vốn rỗng, thêm shape mục là additive — snapshot cũ
  (`items: []`) vẫn hợp lệ.

---

## Câu hỏi mở — ĐÃ CHỐT (2026-11-20, người duyệt)

| # | Câu hỏi | Chốt |
|---|---|---|
| 1 | Snapshot lưu menu đã resolve hay lưu khoá? | **Lưu khoá, FE ghép** (#113) — giữ một nguồn URL (#103) |
| 2 | Menu chưa từng sắp thì sao, trang mới thì sao? | Tự sinh từ trang (trừ `NotFound`); trang mới ⇒ cuối, ẩn |
| 3 | Sửa menu có cần publish? | **Có**; rollback trả menu cũ, menu đang sửa bật `isDirty` |
| 4 | Menu con? | Tối đa 3 cấp (đổi từ "một cấp" 2026-11-21) |
| 5 | Nhắc "chưa publish" thế nào? | `IsDirty` riêng của menu; dirty cấp website (theme) ngoài phạm vi |
| 6 | URL `custom` cho phép gì? | http(s), `tel:`, `mailto:` — luật link đã có (BE `LinkUrlRules`, FE `isSafeLinkUrl`) |
| 7 | Một trang xuất hiện hai lần? | Không (`NAV_PAGE_DUPLICATE`); muốn thì dùng `custom` |
| 8 | Trang đã xoá / đang tắt trong menu? | Đã xoá ⇒ BE bỏ lúc đọc; đang tắt ⇒ giữ trong menu, không render |
