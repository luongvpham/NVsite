# JOBS-001 — contract diff

> Gate 1 gọn trước production (#89): chỉ có `NEW_ENDPOINT` (document mới `platform`), không
> `REMOVED`/`BREAKING`, không câu hỏi mở ⇒ **promote ngay** (`approvedBy = gate1-pre-production-89`).
> Người duyệt đọc lại file này sau. Sinh bằng `node tools/contract-sync/diff-report.mjs JOBS-001`, phần
> dưới "UNCHANGED" điền tay. Export: `pnpm contract:export platform identity`.

## ⚠️ BREAKING / REMOVED
(không có)

## NEW_ENDPOINT
- POST /api/platform/jobs/dashboard-session

## ADDITIVE
(không có)

## UNCHANGED
39 operation không đổi (gồm toàn bộ `identity.v1` — claim `role` nằm trong JWT, không trong OpenAPI).

---

## Endpoint mới (document `platform` — file mới `contracts/openapi/platform.v1.json`, tag `Platform`)

| operationId | Method + path | OK | Lỗi khai |
|---|---|---|---|
| `createJobsDashboardSession` | `POST /api/platform/jobs/dashboard-session` | 204, không body, `Set-Cookie: vsite_jobs_dashboard` | 401 `UNAUTHORIZED` · 403 `PLATFORM_ADMIN_REQUIRED` |

Không request body, không schema mới ngoài `ProblemDetails` (cùng shape các module khác, có `error_code`).

Cookie (không nằm trong OpenAPI, FE không đọc được — HttpOnly): `HttpOnly`, `SameSite=Strict`,
`Path=/hangfire`, `Max-Age` 15 phút, `Secure` ngoài Development, host-only (không `Domain`).

Orval chạy thử trên staging (thư mục tạm): sinh `createJobsDashboardSession` /
`useCreateJobsDashboardSession` + MSW handler, **không** type trùng `…Dto2`.

## Auth policy

Nhóm `MapGroup("/platform").RequireAuthorization(AuthPolicies.PlatformAdmin)` — policy gắn ở **cấp nhóm**
(#114 mục 6), `PlatformRoutePolicyTests` khẳng định mọi route `api/platform/*` mang policy. Policy kiểm
**cả** claim `role = PlatformAdmin` **và** `aud = vsite-portal` (#114 mục 5). Claim chỉ phát cho token
`vsite-portal`, refresh tính lại từ `User.RoleId` (#114 mục 2–3).

## Giả định tôi đã tự đặt (không hỏi)

| # | Giả định | Nếu sai thì |
|---|---|---|
| A1 | Tên endpoint/operationId như plan G5 (`POST /api/platform/jobs/dashboard-session`, `createJobsDashboardSession`), 204 không body | Đổi tên ⇒ đổi contract, FE đổi hook |
| A2 | 403 của policy PlatformAdmin dùng mã mới `PLATFORM_ADMIN_REQUIRED` (plan G4); policy khác giữ `INSUFFICIENT_SCOPE` | Gộp về `INSUFFICIENT_SCOPE` |
| A3 | Token `shop:*` gửi tới host **admin** bị middleware membership chặn trước policy ⇒ 403 `TOKEN_SHOP_MISMATCH` (không phải `PLATFORM_ADMIN_REQUIRED`); gửi từ host shop ⇒ 403 `PLATFORM_ADMIN_REQUIRED`. Portal không bao giờ giữ token shop nên FE chỉ gặp `PLATFORM_ADMIN_REQUIRED` | FE coi mọi 403 trên endpoint này là "không có quyền" |
| A4 | Cookie host-only: POST phải đi tới **cùng host `admin.*`** phục vụ `/hangfire` (dev: proxy Vite `/api` + `/hangfire`; prod: Caddy) — nếu không, trình duyệt không gửi cookie | Đặt `Domain` cho cookie (nới phạm vi — cần duyệt) |

## Câu hỏi cần anh quyết
(không có)

## Người duyệt đã quyết

| Quyết định | Ở đâu |
|---|---|
| Quyền `PlatformAdmin` = role claim + nhóm `/platform/*` + cookie dashboard (người chốt 2026-11-23) | #114 — `backend/docs/modules/identity.md` |
| Module `Platform` chứa `/api/platform/*` (người chốt 2026-11-24) | #115 — `backend/docs/modules/platform.md` |
| Portal biết admin bằng claim `role` trong token, không thêm field response (người chốt 2026-11-24) | plan §4 · data-needs Q1 |
