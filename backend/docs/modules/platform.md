# Platform — vsite

> 📌 **Mọi đường dẫn endpoint trong tài liệu này tương đối với prefix `/api`** (Quyết định #91) — vd. `/platform/jobs/dashboard-session`
> nghĩa là `/api/platform/jobs/dashboard-session`. Ngoại lệ: dashboard `/hangfire` **không** có prefix (xem bên dưới).

## Trạng thái (JOBS-001, Bước 8a)

Module `Platform` mới có **một endpoint**: đổi JWT của `PlatformAdmin` lấy cookie vào dashboard Hangfire. Contract `platform.v1` đã
qua Gate 1 (promote, `gate1-pre-production-89`). Nút ở Portal (S2 FE) đã code — brief: `Docs/tasks/JOBS-001/brief.md`.
Lệch so với plan và nợ: `Docs/tasks/JOBS-001/changelog.md`. Màn quản trị/thống kê nền tảng (#114 mục 7) **chưa làm** — module này sinh ra để chứa chúng.

## Quyết định #115 — Module `Platform`

Người duyệt chốt 2026-11-24.

- **Phạm vi:** nhóm route `/api/platform/*` (#114) và về sau màn quản trị/thống kê nền tảng.
- **Khai ở** `Docs/architecture/dependency-map.json`: `"Platform": { "dependsOn": ["Identity"], "phase": 2 }`. `dependsOn` khởi đầu chỉ có `Identity`
  (quyền đến từ role claim và audience của token do Identity phát). Cần đọc thêm module nào thì thêm **đúng module đó** vào `dependsOn` (người duyệt).
- **Đọc chéo module** chỉ qua Public Contract interface (`Vsite.Application/{Module}/Interfaces/`) hoặc helper trong allowlist `IgnoreQueryFilters()`
  (#114 mục 7) — không `IgnoreQueryFilters()` rải rác, không tự query bảng module khác.
- **`Jobs` không phải module.** Là namespace hạ tầng dùng chung (`ModuleBoundaryTests.SharedSegments`); job cụ thể nằm trong module sở hữu dữ liệu
  (vd. `Vsite.Application.Identity.Jobs`). Quy ước job: `backend/CLAUDE.md` §Job nền (#116).

Quyết định #116 (quy ước job nền) định nghĩa đầy đủ ở `backend/CLAUDE.md` §Job nền, không lặp lại ở đây.

## Route, policy, endpoint

`Vsite.Api/Platform/PlatformEndpoints.cs`: `MapGroup("/platform").WithGroupName("platform").RequireAuthorization(AuthPolicies.PlatformAdmin)` —
policy gắn **ở cấp nhóm** (#114 mục 6); `PlatformRoutePolicyTests` duyệt `EndpointDataSource` và khẳng định mọi route `api/platform/*` mang
policy `PlatformAdmin` và không `AllowAnonymous`, đồng thời khẳng định không file nào dưới `Vsite.Api/Tenancy/` chứa chuỗi `PlatformAdmin`.

Policy `AuthPolicies.PlatformAdmin` kiểm **cả** claim `role = "PlatformAdmin"` **và** audience `vsite-portal`
(API `ValidateAudience = false` nên không được dựa vào bước validate token). Claim chỉ phát cho token `vsite-portal`; quy tắc đầy đủ ở #114
(`backend/docs/modules/identity.md`).

| operationId | Route | Thành công | Lỗi |
|---|---|---|---|
| `createJobsDashboardSession` | `POST /platform/jobs/dashboard-session` (không body) | `204` + `Set-Cookie: vsite_jobs_dashboard` | `401 UNAUTHORIZED` · `403 PLATFORM_ADMIN_REQUIRED` |

### Mã lỗi

| Status | `error_code` | Khi nào |
|---|---|---|
| 401 | `UNAUTHORIZED` | Không token / token hết hạn (mutator FE tự refresh như mọi request) |
| 403 | `PLATFORM_ADMIN_REQUIRED` | Token `vsite-portal` không có role, token `vsite-main`, hoặc token `shop:*` gửi từ host shop |
| 403 | `TOKEN_SHOP_MISMATCH` | Token `shop:*` gửi tới host `admin.*` — `ShopMembershipValidationMiddleware` chặn trước policy. Portal không giữ token shop nên FE không gặp |

⚠️ **Nhánh `PlatformAdmin` trong `ProblemDetailsAuthorizationMiddlewareResultHandler` chỉ chọn mã lỗi, không phải bypass.** Policy đã fail rồi;
handler nhìn endpoint metadata (`IAuthorizeData.Policy == "PlatformAdmin"`) để trả `PLATFORM_ADMIN_REQUIRED` thay vì `INSUFFICIENT_SCOPE` (mọi policy
khác giữ `INSUFFICIENT_SCOPE`). Đừng đọc nó như "PlatformAdmin thì cho qua" và đừng thêm nhánh như vậy vào bất kỳ filter/handler dùng chung nào (#114 mục 6).
Giả định ngầm: nhóm `/platform` hiện chỉ có một policy.

## Cookie dashboard

`Vsite.Api.Platform.JobsDashboardCookie` (singleton):

| Thuộc tính | Giá trị |
|---|---|
| Tên | `vsite_jobs_dashboard` |
| Mục đích | Trình duyệt mở `/hangfire` không gửi Bearer ⇒ đổi JWT (đã qua policy) lấy cookie ngắn hạn để filter của dashboard kiểm |
| Nội dung | Data Protection, purpose `Vsite.Platform.JobsDashboard.v1`, payload `{ userId, expiresAt }` (không log) |
| `HttpOnly` | có |
| `SameSite` | `Strict` |
| `Path` | `/hangfire` |
| `Secure` | có ngoài môi trường Development |
| Sống | 15 phút (`Max-Age=900`, hằng `JobsDashboardCookie.Lifetime`, ≤ TTL access token); hết hạn khi `now >= expiresAt` |
| `Domain` | **không đặt — host-only** |

**Hệ quả host-only:** cookie chỉ về đúng host đã nhận `Set-Cookie`. `POST /api/platform/jobs/dashboard-session` và `GET /hangfire` phải đi tới
**cùng host `admin.*`** — dev: cả hai qua proxy Vite của Portal; production: Caddy route cả `/api/*` và `/hangfire` trên host `admin.*`.
`TryValidate` không bao giờ ném (cookie giả, sửa, hết hạn, ký bằng purpose khác ⇒ `false`).

**Nợ trước ra mắt:** key ring Data Protection mặc định theo máy/profile ⇒ phải persist khi deploy, nếu không restart làm cookie mất hiệu lực
(chỉ khiến người dùng bấm lại nút) và nhiều instance không giải mã chéo được.

## Dashboard `/hangfire`

`app.MapHangfireDashboard("/hangfire", …)` ở `Program.cs`, **cố ý ngoài `/api`** (ngoại lệ có comment trong `ApiRoutePrefixTests`, cùng loại với
`/media`, `/openapi`). **Không bao giờ map dưới `/api`.**

`Vsite.Api.Platform.JobsDashboardAuthorizationFilter` (`IDashboardAsyncAuthorizationFilter`, đặt ở `AsyncAuthorization`; `Authorization = []` để bỏ
mặc định `LocalRequestsOnly`) cho qua khi **cả hai**:
1. `ITenantContext.AudienceKind == Portal` — tức host `admin.*` (host shop và `api.*` ⇒ 401, kể cả mang cookie chép tay); tenant middleware chạy trước dashboard nên có sẵn;
2. cookie `vsite_jobs_dashboard` giải mã được và chưa hết hạn;
3. (JOBS-001-D1, W1 — CSRF) với method ghi (khác GET/HEAD/OPTIONS) phải **cùng origin**: có `Origin` ⇒ host[:port] của nó = `Host` request; không có `Origin` ⇒ chỉ nhận `Sec-Fetch-Site: same-origin`; `Origin` hỏng/"null"/khác host ⇒ 401. Cần vì `{slug}.vsite.*` và `admin.*` cùng site nên `SameSite=Strict` không chặn form-POST từ trang shop. Client không phải trình duyệt (không gửi Origin) bị từ chối có chủ đích.

Mọi lỗi giải mã ⇒ `false` (Hangfire trả 401), không ném 500. Dashboard đủ thao tác (trigger, retry, xoá — `IsReadOnlyFunc = _ => false`).

**Gỡ role:** token cũ còn qua tới hết TTL (≤ 15 phút, #114 mục 4); cookie đã cấp cũng sống ≤ 15 phút. Refresh kế tiếp không còn claim ⇒ nút biến mất.

## Test

`tests/IntegrationTests/Platform/`: `PlatformPolicyTests` (204 + thuộc tính cookie, 401, 403 theo từng loại token, R1), `PlatformRoutePolicyTests`
(không Docker), `JobsDashboardTests` (200/401 theo cookie và host, POST dashboard). `tests/IntegrationTests/Identity/PlatformAdminClaimTests.cs` cho claim.

Chạy: `dotnet test backend/tests/IntegrationTests --filter "FullyQualifiedName~Platform"`.
