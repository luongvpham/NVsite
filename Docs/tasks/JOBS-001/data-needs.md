# JOBS-001 (Bước 8a) — nhu cầu dữ liệu

> Viết từ góc nhìn bên dùng: header Portal và trình duyệt mở dashboard Hangfire. Không viết contract ở
> đây — tên endpoint/field là đề xuất, chốt ở Gate 1. Quyết định nền: #114
> (`backend/docs/modules/identity.md` §Quyết định #114).

## Portal — header (mọi màn sau đăng nhập)

**Cần biết:** user hiện tại có phải `PlatformAdmin` không — **chỉ để hiện/ẩn nút "Job nền"**.

- Nguồn: claim `role` trong access token `vsite-portal` đang giữ ở mutator `@vsite/api-sdk` (người chốt
  2026-11-24: đọc claim, không thêm field vào response login/refresh). Giá trị `"PlatformAdmin"` ⇒ hiện nút.
- Không verify chữ ký ở FE; BE tự kiểm ở mọi bước sau. Giống `ownerShopIds` (#27): claim chỉ để vẽ UI.
- Token được thay sau mỗi lần refresh ⇒ nút phải phản ánh token **hiện tại** (role bị gỡ thì sau lần
  refresh kế tiếp nút biến mất).
- Không có claim / token hỏng ⇒ coi như không phải admin (ẩn nút), không ném lỗi.

## Portal — bấm "Job nền"

**Gọi** `POST /api/platform/jobs/dashboard-session` (đề xuất; operationId đề xuất
`createJobsDashboardSession`), không body.

- `204` + `Set-Cookie` HttpOnly, path `/hangfire`, sống ≤ 15 phút ⇒ Portal mở `/hangfire` ở tab mới.
- `401` (không token / token hết hạn) ⇒ mutator tự refresh như mọi request; vẫn 401 ⇒ về `/login`.
- `403 PLATFORM_ADMIN_REQUIRED` (không phải admin, hoặc audience không phải `vsite-portal`) ⇒ thông báo
  "Bạn không có quyền xem job nền" và ẩn nút.
- Lỗi khác ⇒ thông báo chung, cho bấm lại.

FE không đọc gì từ response body (không có body).

## Trình duyệt — `/hangfire`

- Cùng origin với Portal (host `admin.*`) để cookie path `/hangfire` được gửi kèm. Dev: proxy Vite
  `/hangfire` → BE; prod: Caddy (ghi vào nợ "Trước ra mắt").
- Cookie thiếu / hết hạn / bị giả, hoặc host không phải `admin.*` ⇒ không vào được (401 của Hangfire).
  Muốn vào lại thì bấm nút ở Portal.
- Dashboard cho đầy đủ thao tác (trigger, retry, xoá) — người chốt 2026-11-24.

## Câu hỏi mở

| # | Câu hỏi | Quyết định |
|---|---|---|
| Q1 | Portal biết admin bằng gì? | Đọc claim `role` trong token (người chốt 2026-11-24) |
| Q2 | Dashboard thao tác tới đâu, host nào? | Đầy đủ thao tác; chỉ host `admin.*` (người chốt 2026-11-24) |
| Q3 | Có cần Portal hiện trạng thái job (lần chạy cuối, lỗi)? | Không — xem ở dashboard. Màn thống kê là task quản trị sau |
| Q4 | `403` khi token `shop:*` hoặc `vsite-main` có role? | Không xảy ra theo #114 mục 2 (claim chỉ phát cho `vsite-portal`), policy vẫn kiểm audience (#114 mục 5) ⇒ 403 |
