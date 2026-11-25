# JOBS-001 — Hangfire: hạ tầng job dùng chung + quyền `PlatformAdmin` (Bước 8a)

> **Đã chốt 2026-11-24 — chưa code**
>
> **Lane C.** Hạ tầng mới (Hangfire + schema riêng trên Postgres), cơ chế đặt tenant ngoài request (#116),
> đổi luồng phát token Identity (claim `role`, #114), module mới `Platform` + nhóm route `/api/platform/*`
> (chạm contract → Gate 1, #115), thêm một nút ở Portal. Cổng: Gate 1 → Gate 2. Tuần tự S1 (BE) → S2 (FE).
>
> **Người thực thi dùng skill:** `superpowers:subagent-driven-development` hoặc
> `superpowers:executing-plans`. Mỗi bước có checkbox `- [ ]`.

**Mục tiêu:** hệ thống có chỗ chạy job nền theo lịch, với quy ước tenant rõ ràng để 8d, M5, 10c dùng lại;
3 job dọn/kiểm của Identity chạy hằng ngày; super admin (`PlatformAdmin`) bấm một nút ở Portal để mở
dashboard Hangfire, người khác không mở được.
**Nhu cầu dữ liệu:** [`data-needs.md`](data-needs.md) · **Spec:** `DesignIdeal/step.md` §8a ·
`backend/docs/modules/identity.md` §Quyết định #114 · `Docs/tasks/IDENTITY-001/changelog.md` §B ·
**Quyết định liên quan:** #114, #115, #116, #21 (.2, .5), #90, #3, #27, #91, #19, #1, #111

## 1. Yêu cầu (đã chốt với người 2026-11-24)

- **Người dùng / vai trò:** `PlatformAdmin` (super admin / dev) — xem, chạy lại job. Không người dùng cuối
  nào chạm trực tiếp; job chạy ngầm.
- **Điểm chạm:**
  - Hangfire server chạy **trong process `Vsite.Api`** (chỉ một deployment — `backend/CLAUDE.md`), lưu trên
    Postgres hiện có, schema riêng `hangfire`.
  - **Cơ chế tenant cho job (#116):** `ITenantScopeFactory.BeginShopScope(shopId)` + khung fan-out cha/con.
  - Token `vsite-portal` mang claim `role = "PlatformAdmin"` khi `User.RoleId` là PlatformAdmin (#114).
  - Module `Platform` (#115): nhóm route `/api/platform/*` gắn policy `AuthPolicies.PlatformAdmin`; endpoint
    đổi JWT lấy cookie vào dashboard.
  - Dashboard `/hangfire`: đủ thao tác (trigger, retry, xoá), chỉ mở ở host `admin.*`, kiểm cookie.
  - Portal: nút "Job nền" ở header, chỉ hiện khi claim `role` = PlatformAdmin (đọc từ token).
- **3 job của `03` (ngoại lệ (a) của #116 — bảng không tenant, set-based):**
  1. Xoá `PendingRegistration` có `ExpiresAt < now - đệm`.
  2. Xoá `RefreshToken` + `PasswordResetToken` có `ExpiresAt < now - đệm` — **không** theo `RevokedAt`.
  3. Quét `User` có `PrimaryIdentityKind = Zalo` mà không có `ExternalLogin` → chỉ log Warning.
  - Đệm cấu hình được, **mặc định 0** (xoá ngay khi hết hạn). Chạy hằng ngày.
- **Xong nghĩa là (step.md 8a):** 3 job chạy theo lịch; người không phải `PlatformAdmin` không mở được
  dashboard; có test khẳng định job xử lý shop A không chạm dữ liệu shop B — qua `BeginShopScope` và **một
  job mẫu chạy trên bảng tenant** (3 job của `03` không đi qua tenant).
- **Không làm trong task này:** job của builder (8d) · màn quản trị / thống kê nền tảng (#114 mục 7) · cấp
  role PlatformAdmin qua UI (vẫn sửa DB tay) · kênh thông báo (email/Slack) cho integrity job · outbox (M2) ·
  cấu hình Caddy production cho `/hangfire` (ghi nợ "Trước ra mắt").

## 2. Hiện trạng (khảo sát 2026-11-24)

| Khả năng | Trạng thái | Ở đâu | Ghi chú |
|---|---|---|---|
| Hangfire | ❌ chưa có | [Directory.Packages.props](../../../backend/Directory.Packages.props) | Không có gói Hangfire nào |
| Đặt tenant ngoài request | ❌ chưa có | [TenantContext.cs:10-14](../../../backend/src/Vsite.Api/Tenancy/TenantContext.cs#L10-L14) | `internal set`, chỉ middleware ghi — [TenantResolutionMiddleware.cs:25-55](../../../backend/src/Vsite.Api/Tenancy/TenantResolutionMiddleware.cs#L25-L55); đăng ký Scoped ở [Program.cs:113-114](../../../backend/src/Vsite.Api/Program.cs#L113-L114) |
| Filter tenant đọc `ShopId` | ✅ có | [TenantQueryFilterExtensions.cs:32-52](../../../backend/src/Vsite.Infrastructure/Persistence/TenantQueryFilterExtensions.cs#L32-L52) | Đọc `AppDbContext.TenantContext.ShopId` mỗi query; `null` ⇒ rỗng (fail-closed) |
| Danh sách shop | ✅ có | [Shop.cs:21](../../../backend/src/Vsite.Domain/Shop/Entities/Shop.cs#L21) | `Shop : BaseAuditableEntity` — **không** tenant, chỉ filter xoá mềm ⇒ liệt kê không cần `IgnoreQueryFilters()`. `ShopStatus` Draft/Active/Suspended/Closed; shop mới mặc định `Draft` (#111) |
| Role `PlatformAdmin` | 🟡 chỉ seed | [RoleSeed.cs:21](../../../backend/src/Vsite.Infrastructure/Persistence/Seed/RoleSeed.cs#L21), [WellKnownRoles.cs:11](../../../backend/src/Vsite.Domain/Identity/WellKnownRoles.cs#L11) | Có dòng role + GUID cố định; không code nào kiểm |
| Role của user | ✅ có | [User.cs:47](../../../backend/src/Vsite.Domain/Identity/Entities/User.cs#L47) `RoleId` | Nguồn duy nhất theo #114 mục 1 |
| Claim trong access token | 🟡 thiếu `role` | [JwtTokenService.cs:20-48](../../../backend/src/Vsite.Infrastructure/Identity/JwtTokenService.cs#L20-L48) | sub/jti/email/phone/name/avatar/ownerShopIds |
| Nơi dựng `AccessTokenRequest` | ✅ 2 chỗ | [LoginHandler.cs:106-113](../../../backend/src/Vsite.Application/Identity/Auth/Commands/Login/LoginHandler.cs#L106-L113), [RefreshTokenHandler.cs:55-61](../../../backend/src/Vsite.Application/Identity/Auth/Commands/RefreshToken/RefreshTokenHandler.cs#L55-L61) | Cả hai tính `ownerShopIds` từ DB → chỗ tính role (#114 mục 3). Record: [IJwtTokenService.cs:3-10](../../../backend/src/Vsite.Application/Identity/Interfaces/IJwtTokenService.cs#L3-L10) |
| Validate JWT | 🟡 | [AuthenticationSetup.cs:30-43](../../../backend/src/Vsite.Api/Auth/AuthenticationSetup.cs#L30-L43) | `MapInboundClaims = false`, `ValidateAudience = false`; chưa đặt `RoleClaimType` |
| Policy | 🟡 | [AuthPolicies.cs:7](../../../backend/src/Vsite.Domain/Authorization/AuthPolicies.cs#L7), [AuthenticationSetup.cs:46-48](../../../backend/src/Vsite.Api/Auth/AuthenticationSetup.cs#L46-L48) | Chỉ `RequireGlobalScope`; hằng audience ở [AudienceHelpers.cs:15-16](../../../backend/src/Vsite.Domain/Authorization/AudienceHelpers.cs#L15-L16) |
| 403 khi policy fail | 🟡 | [ProblemDetailsAuthorizationMiddlewareResultHandler.cs:33-36](../../../backend/src/Vsite.Api/ExceptionHandling/ProblemDetailsAuthorizationMiddlewareResultHandler.cs#L33-L36) | Luôn `INSUFFICIENT_SCOPE` — chưa phân biệt theo policy |
| Nhóm route + OpenAPI document | ✅ mẫu | [Program.cs:42-58](../../../backend/src/Vsite.Api/Program.cs#L42-L58), [Program.cs:198-203](../../../backend/src/Vsite.Api/Program.cs#L198-L203) | Chưa có document/nhóm `platform` |
| Module boundary | ✅ | [dependency-map.json](../../../Docs/architecture/dependency-map.json), `SharedSegments` ở [ModuleBoundaryTests.cs:25-31](../../../backend/tests/ArchitectureTests/ModuleBoundaryTests.cs#L25-L31) | Chưa có `Platform`, chưa có segment `Jobs` |
| Route ngoài `/api` | 🟡 | [ApiRoutePrefixTests.cs:21](../../../backend/tests/IntegrationTests/Common/ApiRoutePrefixTests.cs#L21) | Chỉ cho `openapi/`; phải thêm `hangfire` |
| Host `admin` → audience Portal | ✅ có | [TenantResolutionMiddleware.cs:29-33](../../../backend/src/Vsite.Api/Tenancy/TenantResolutionMiddleware.cs#L29-L33) | Dùng để giới hạn dashboard chỉ ở host admin |
| Allowlist `IgnoreQueryFilters()` | ✅ | [IgnoreQueryFiltersAllowlistTests.cs:21-35](../../../backend/tests/ArchitectureTests/IgnoreQueryFiltersAllowlistTests.cs#L21-L35) | Task này **không** thêm file nào (3 job không tenant, danh sách shop không cần) |
| Entity token có `ExpiresAt` | ✅ có | [RefreshToken.cs:32](../../../backend/src/Vsite.Domain/Identity/Entities/RefreshToken.cs#L32), [PasswordResetToken.cs:28](../../../backend/src/Vsite.Domain/Identity/Entities/PasswordResetToken.cs#L28), [PendingRegistration.cs:31](../../../backend/src/Vsite.Domain/Identity/Entities/PendingRegistration.cs#L31) | Cả ba là `BaseEntity` — không filter tenant, không xoá mềm |
| Index phục vụ dọn | 🟡 | [PendingRegistrationConfiguration.cs:17](../../../backend/src/Vsite.Infrastructure/Persistence/Configurations/Identity/PendingRegistrationConfiguration.cs#L17) có; [RefreshTokenConfiguration.cs:14-15](../../../backend/src/Vsite.Infrastructure/Persistence/Configurations/Identity/RefreshTokenConfiguration.cs#L14-L15), [PasswordResetTokenConfiguration.cs:14](../../../backend/src/Vsite.Infrastructure/Persistence/Configurations/Identity/PasswordResetTokenConfiguration.cs#L14) **không** | Migration thêm index `expires_at` cho 2 bảng |
| Zalo integrity | 🟡 | [User.cs:38](../../../backend/src/Vsite.Domain/Identity/Entities/User.cs#L38) `PrimaryIdentityKind` | Zalo Login chưa bật → job thường trả 0 |
| Portal biết user là admin | ❌ | [session-store.ts:14-30](../../../apps/portal/src/stores/session-store.ts#L14-L30) | FE không decode token; token chỉ ở mutator — [axios-instance.ts:32](../../../packages/api-sdk/src/mutator/axios-instance.ts#L32) |
| Header Portal | ✅ chỗ đặt nút | [_authenticated.tsx:29-51](../../../apps/portal/src/routes/_authenticated.tsx#L29-L51) | |
| Proxy Portal dev | 🟡 | [vite.config.ts:62-66](../../../apps/portal/vite.config.ts#L62-L66) | Chỉ `/api`, `/media`; thiếu `/hangfire` |
| Integration test host | ✅ | [AppApiFactory.cs:52-60](../../../backend/tests/IntegrationTests/Common/AppApiFactory.cs#L52-L60) | Testcontainers Postgres; cần tắt Hangfire server trong test |
| Docker trên máy | ✅ | `docker info` → 29.8.0 | Testcontainers chạy được; `DOCKER-TEST-DEBT.md` rỗng |

### Phát hiện khi khảo sát

1. **3 job của `03` không chạm dữ liệu tenant** — ba entity đều `BaseEntity`. Test A/B của step.md 8a vì
   thế phải dùng job mẫu (người chốt — #116).
2. **`TenantContext` chỉ middleware ghi được**, nên cơ chế `BeginShopScope` phải sống cùng assembly
   (`Vsite.Api.Tenancy`), hoặc đổi chỗ `TenantContext`. Plan chọn cách đầu (T2) — ít xáo trộn nhất.
3. **Shop mới mặc định `Draft` và chưa có luồng kích hoạt** — job cha chỉ lấy `Active` sẽ bỏ sót gần hết shop.
   Người chốt: lấy mọi shop chưa xoá mềm (#116).
4. **Xoá `RefreshToken` khi hết hạn làm mất tín hiệu "reuse"** cho token đó —
   [RefreshTokenHandler.cs:35-39](../../../backend/src/Vsite.Application/Identity/Auth/Commands/RefreshToken/RefreshTokenHandler.cs#L35-L39)
   chỉ thu hồi cả họ khi còn thấy dòng đã revoke. Token đã hết hạn thì kẻ gian cũng không dùng được; rủi ro
   là mất dấu vết. Người chọn đệm cấu hình được, mặc định 0. **Không** dọn theo `RevokedAt` (#116).
5. **Hangfire tự tạo schema** (`hangfire.*`) ngoài EF migration — không theo quy ước snake_case/tên ràng
   buộc. Ghi vào `backend/CLAUDE.md` để không ai "sửa" bằng migration.
6. **Dashboard là endpoint ngoài `/api`** — cùng loại ngoại lệ với `/media`, `/openapi`.
7. **Production chưa có Caddy** — `/hangfire` trên host `admin.*` phải được route sang BE khi deploy (cùng
   dòng nợ "Caddy phải route `/media/*` và `/api/*`" của WEBSITE-005).

## 3. Flow

### Flow 1: Admin mở dashboard job

```text
1. Đăng nhập Portal (admin.vsite.local)                    ✅ LoginHandler.cs:106-113
2. BE phát token vsite-portal + claim role=PlatformAdmin   ❌ T1 — AccessTokenRequest + JwtTokenService
3. Refresh tính lại role từ DB                             ❌ T1 — RefreshTokenHandler.cs:55-61
4. Portal hiện nút "Job nền" khi token có role             ❌ F1 — api-sdk đọc claim token hiện tại
5. Bấm → POST /api/platform/jobs/dashboard-session         ❌ T3 — nhóm /platform + policy + endpoint
     → 204 + Set-Cookie HttpOnly, Path=/hangfire, ≤ 15 phút
6. Mở tab /hangfire (qua proxy Vite / Caddy)               ❌ T4 (map dashboard) + F1 (proxy dev)
7. Filter kiểm host admin + cookie → dashboard             ❌ T4
```

Nhánh lỗi (đã chốt):
- Không token / token hết hạn ⇒ 401 `UNAUTHORIZED` (mutator tự refresh như mọi request).
- Token không có role, hoặc audience ≠ `vsite-portal` ⇒ 403 `PLATFORM_ADMIN_REQUIRED`; Portal báo và ẩn nút.
- `/hangfire` thiếu cookie / cookie hết hạn / giả / host không phải `admin.*` ⇒ 401 của Hangfire; vào lại
  bằng nút ở Portal.
- Role bị gỡ trong DB ⇒ token cũ vẫn qua tới hết TTL (≤ 15 phút, #114 mục 4); cookie dashboard đã cấp cũng
  sống ≤ 15 phút. Refresh kế tiếp không còn claim ⇒ nút biến mất.

### Flow 2: Job dọn hằng ngày (ngoại lệ (a) của #116)

```text
1. Vsite.Api khởi động → Hangfire server (nếu Jobs:ServerEnabled) + đăng ký 3 recurring job   ❌ T5
2. 03:00 giờ VN: xoá theo lô dòng expires_at < now - đệm ở 3 bảng                             ❌ T5 (+ index)
3. Zalo integrity: đếm User Zalo thiếu ExternalLogin; > 0 → log Warning (số lượng + ≤ 20 UserId) ❌ T5
```

Nhánh lỗi: job ném lỗi ⇒ Hangfire retry theo mặc định, hiện ở dashboard. Không có dòng nào ⇒ job kết thúc,
log Information số dòng đã xoá (0).

### Flow 3: Job theo từng shop (khung cho 8d — #116)

```text
1. Job cha theo lịch: lấy Id mọi shop chưa xoá mềm (một helper duy nhất)       ❌ T2
2. Enqueue mỗi shop một job con với tham số shopId                             ❌ T2
3. Job con: BeginShopScope(shopId) → DI scope mới, TenantContext đặt một lần   ❌ T2
4. Trong scope, job query bình thường — Global Query Filter giới hạn về shopId ✅ TenantQueryFilterExtensions.cs:32-52
```

Nhánh lỗi: shop đã xoá mềm giữa lúc cha enqueue và con chạy ⇒ con kết thúc không làm gì (log Information).
Con lỗi ⇒ chỉ shop đó retry. `BeginShopScope` gọi trên scope đã có tenant ⇒ `InvalidOperationException`.
8a chỉ có **job mẫu trong test** dùng khung này; job thật đầu tiên là 8d.

## 4. Đã chốt trong session lập plan

| # | Chốt | Ai chốt |
|---|---|---|
| #114 | Quyền `PlatformAdmin` = role claim + nhóm `/platform/*` + cookie dashboard (định nghĩa ở `identity.md`) | người (2026-11-23) |
| #115 | **Module `Platform`** chứa nhóm route `/api/platform/*` (#114) và về sau màn quản trị/thống kê nền tảng. Khai ở `dependency-map.json`, `dependsOn` khởi đầu `[Identity]`; cần đọc thêm module nào thì thêm đúng module đó (người duyệt), đọc qua Public Contract interface / helper allowlist (#114 mục 7). `Jobs` **không** là module: namespace hạ tầng dùng chung (`SharedSegments` ở Infrastructure); job cụ thể nằm trong module sở hữu dữ liệu (vd. `Vsite.Application.Identity.Jobs`) | người (2026-11-24) |
| #116 | **Quy ước job nền ngoài request.** Mặc định mỗi lần chạy chỉ xử lý **một shop** (fan-out): job cha theo lịch lấy `ShopId` của **mọi shop chưa xoá mềm** qua một helper duy nhất, enqueue mỗi shop một job con có tham số `shopId`; job con chạy trong `ITenantScopeFactory.BeginShopScope(shopId)` (DI scope mới, `TenantContext` đặt **đúng một lần**), query bình thường để Global Query Filter bảo vệ; lỗi shop nào retry shop đó. **Ngoại lệ chạy một lần cho nhiều shop:** (a) bảng không tenant (`RefreshToken`, `PasswordResetToken`, `PendingRegistration`, `User`…), set-based; (b) dọn dẹp set-based trên bảng tenant mà mỗi dòng tự quyết bởi dữ liệu của chính nó — file phải thuộc allowlist `IgnoreQueryFilters()`. **Cấm** nạp dữ liệu nhiều shop vào bộ nhớ rồi xử lý chung. Dọn token theo `ExpiresAt < now - đệm` (đệm cấu hình, mặc định 0), **không** theo `RevokedAt` | người (2026-11-24) |
| — | 3 job của `03`: xét toàn hệ, không theo shop; đệm mặc định 0; integrity Zalo chỉ log | người (2026-11-24) |
| — | Portal biết admin bằng cách đọc claim `role` của token hiện tại (không thêm field response) | người (2026-11-24) |
| — | Dashboard đủ thao tác, chỉ host `admin.*` | người (2026-11-24) |
| — | Lối vào dashboard = nút ở Portal (S2 FE) | người (2026-11-24) |
| — | Phạm vi: không làm 5 feedback WEBSITE-007 ở đây — làm task riêng sau | người (2026-11-24) |

Nội dung #115/#116 ở đây là định nghĩa tạm; T7 chuyển sang tài liệu bền (`backend/CLAUDE.md` §Job nền,
`backend/docs/modules/platform.md`) và sửa cột "Định nghĩa đầy đủ" ở `DECISIONS.md`.

## 5. Trình tự session

- **S1 — Backend (T1–T7).** Thứ tự: claim + policy (T1) → tenant scope + fan-out (T2, không phụ thuộc
  Hangfire server, test được ngay) → module Platform + endpoint cookie (T3) → Hangfire + dashboard (T4) →
  3 job (T5) → test A/B qua job mẫu (T6) → đóng S1 (T7, Gate 1).
- **Gate 1:** contract mới `platform.v1` (một endpoint) + identity không đổi shape (claim nằm trong JWT,
  không trong OpenAPI). Chỉ thêm + không câu hỏi ⇒ promote ngay (#89).
- **S2 — Frontend (F1–F2)**, mở sau Gate 1: `gen:api` → nút ở header → proxy → nghiệm thu tay với BE thật.

## 6. Ràng buộc toàn cục

| Ràng buộc | Giữ bằng | Ở task |
|---|---|---|
| Claim `role` chỉ phát cho `vsite-portal` (#114 mục 2) | `PlatformAdminClaimTests` "shop/main audience không có role" | T1 |
| Refresh tính lại role từ DB (#114 mục 3) | `PlatformAdminClaimTests` "gỡ role ⇒ refresh không còn claim" | T1 |
| Policy kiểm claim **và** audience (#114 mục 5) | `PlatformPolicyTests` "token shop có role ⇒ 403" | T1, T3 |
| Mọi route `/api/platform/*` mang policy (#114 mục 6) | `PlatformRoutePolicyTests` (duyệt `EndpointDataSource`) | T3 |
| Không nhánh bypass PlatformAdmin trong filter dùng chung (#114 mục 6) | review — grep `PlatformAdmin` trong `Vsite.Api/Tenancy/**` phải rỗng; ghi thành ca trong `PlatformRoutePolicyTests` | T3 |
| `TenantContext` đặt đúng một lần (#116) | `TenantScopeFactoryTests` "đặt lần hai ném" | T2 |
| Job shop A không chạm shop B (step.md 8a, #116) | `ShopJobIsolationTests` (Postgres thật) | T6 |
| Không thêm `IgnoreQueryFilters()` | `IgnoreQueryFiltersAllowlistTests` (không sửa allowlist) | T2–T6 |
| Ranh giới module (#1, #115) | `ModuleBoundaryTests` + `dependency-map.json` | T3 |
| Endpoint dưới `/api`, có `operationId` (#91) | `ApiRoutePrefixTests` (thêm ngoại lệ `hangfire/` có lý do) | T3, T4 |
| ProblemDetails có `error_code` (#19) | `PlatformPolicyTests` khẳng định `PLATFORM_ADMIN_REQUIRED` | T3 |
| File generated không sửa tay | hook `guard-write.mjs` | F1 |
| Claim ở FE chỉ để vẽ UI | review — F1 không dùng claim để quyết gì khác | F1 |

## 7. Review focus — lỗi dễ lọt khi spec im lặng

| # | Tình huống | Hành vi đúng | Test ở |
|---|---|---|---|
| R1 | Token `shop:{id}` của user có `RoleId = PlatformAdmin` gọi `/api/platform/*` | 403 — claim không phát cho audience shop, policy kiểm audience | T1, T3 |
| R2 | `/hangfire` mở từ host shop (`spa-abc.vsite.local`) hoặc host `api.*` kèm cookie hợp lệ chép tay | 401 — filter kiểm `TenantContext.AudienceKind == Portal` | T4 |
| R3 | Cookie dashboard bị sửa / hết hạn / ký bằng purpose khác | 401; không ném 500 | T4 |
| R4 | `BeginShopScope` gọi trong scope request đã có tenant, hoặc gọi hai lần | `InvalidOperationException`; tenant cũ không bị ghi đè | T2 |
| R5 | Shop bị xoá mềm sau khi job cha enqueue | Job con no-op; filter fail-closed vẫn đúng | T2 |
| R6 | Lịch 03:00 VN trên Windows dev vs Linux prod | Dùng IANA `Asia/Ho_Chi_Minh` (ICU), test resolve được múi giờ | T5 |
| R7 | Dọn token đụng dòng còn hạn trong cùng giây | Điều kiện `<` nghiêm ngặt với `now - đệm`; ca biên trong test | T5 |
| R8 | Integration test khởi động Hangfire server, chạy job thật giữa chừng | `Jobs:ServerEnabled=false` trong `AppApiFactory`; test gọi job trực tiếp | T4 |

## 8. Sơ đồ file

```text
backend/
  Directory.Packages.props                                    sửa  Hangfire.Core/AspNetCore/PostgreSql
  src/Vsite.Domain/Authorization/AuthPolicies.cs              sửa  + PlatformAdmin
  src/Vsite.Domain/Identity/WellKnownRoles.cs                 sửa  + PlatformAdminCode = "PlatformAdmin"
  src/Vsite.Application/Common/Interfaces/ITenantScopeFactory.cs     mới
  src/Vsite.Application/Common/Jobs/IShopJob.cs                       mới  (job con theo shop)
  src/Vsite.Application/Identity/Interfaces/IJwtTokenService.cs      sửa  AccessTokenRequest + Role
  src/Vsite.Application/Identity/Auth/Commands/Login/LoginHandler.cs sửa
  src/Vsite.Application/Identity/Auth/Commands/RefreshToken/RefreshTokenHandler.cs sửa
  src/Vsite.Application/Identity/Jobs/
    ExpiredTokenCleanupJob.cs  PendingRegistrationCleanupJob.cs  ZaloIdentityIntegrityJob.cs   mới
  src/Vsite.Application/Platform/Commands/CreateJobsDashboardSession/…  mới (nếu cần handler)
  src/Vsite.Infrastructure/Identity/JwtTokenService.cs        sửa  claim role
  src/Vsite.Infrastructure/Jobs/
    JobsOptions.cs  HangfireSetup.cs  ShopFanOut.cs  ShopJobRunner.cs  RecurringJobs.cs        mới
  src/Vsite.Infrastructure/Persistence/Configurations/Identity/{RefreshToken,PasswordResetToken}Configuration.cs  sửa  index expires_at
  src/Vsite.Infrastructure/Persistence/Migrations/{ts}_AddTokenExpiresAtIndexes.cs           mới
  src/Vsite.Api/Tenancy/TenantContext.cs                      sửa  đặt-một-lần
  src/Vsite.Api/Tenancy/TenantScopeFactory.cs                 mới
  src/Vsite.Api/Auth/AuthenticationSetup.cs                   sửa  RoleClaimType + policy
  src/Vsite.Api/ExceptionHandling/ProblemDetailsAuthorizationMiddlewareResultHandler.cs  sửa  mã theo policy
  src/Vsite.Api/Platform/PlatformEndpoints.cs                 mới  nhóm /platform + dashboard-session
  src/Vsite.Api/Platform/JobsDashboardCookie.cs               mới
  src/Vsite.Api/Platform/JobsDashboardAuthorizationFilter.cs  mới
  src/Vsite.Api/Program.cs                                    sửa  AddOpenApi("platform"), Hangfire, map
  src/Vsite.Api/appsettings*.json                             sửa  Jobs:*
  tests/ArchitectureTests/ModuleBoundaryTests.cs              sửa  SharedSegments + "Jobs"
  tests/IntegrationTests/Common/AppApiFactory.cs              sửa  Jobs:ServerEnabled=false
  tests/IntegrationTests/Common/ApiRoutePrefixTests.cs        sửa  ngoại lệ hangfire/
  tests/IntegrationTests/Identity/PlatformAdminClaimTests.cs  mới
  tests/IntegrationTests/Identity/IdentityJobsTests.cs        mới
  tests/IntegrationTests/Platform/{PlatformPolicyTests,PlatformRoutePolicyTests,JobsDashboardTests}.cs  mới
  tests/IntegrationTests/Jobs/{TenantScopeFactoryTests,ShopFanOutTests,ShopJobIsolationTests}.cs     mới
  docs/modules/platform.md                                    mới
  docs/modules/identity.md, CLAUDE.md                         sửa  (T7)
Docs/architecture/dependency-map.json                         sửa  + Platform
contracts/openapi/platform.v1.json                            mới  (qua promote, không ghi tay)
packages/api-sdk/src/mutator/axios-instance.ts (hoặc file cạnh) sửa  đọc claim token hiện tại
apps/portal/src/routes/_authenticated.tsx                     sửa  nút "Job nền"
apps/portal/src/components/jobs-dashboard-button.tsx mới
apps/portal/vite.config.ts                                    sửa  proxy /hangfire
```

## 9. Session S1: Backend

### T1: Claim `role` + policy `PlatformAdmin` (#114 mục 1–5)

- `AccessTokenRequest` thêm `IReadOnlyCollection<string> Roles`. Login và refresh tính từ DB: `user.RoleId ==
  WellKnownRoles.PlatformAdminId` **và** audience = `vsite-portal` ⇒ `["PlatformAdmin"]`, còn lại rỗng.
- `JwtTokenService` thêm claim `"role"` cho từng phần tử.
- `AuthenticationSetup`: `TokenValidationParameters.RoleClaimType = "role"`; policy
  `AuthPolicies.PlatformAdmin = RequireRole("PlatformAdmin") + RequireAssertion(aud == vsite-portal)`.
- [ ] Test trước `tests/IntegrationTests/Identity/PlatformAdminClaimTests.cs`: login portal admin ⇒ có claim ·
  login portal user thường ⇒ không · login trên host shop với user admin ⇒ không · login `vsite-main` ⇒ không ·
  refresh sau khi đổi `RoleId` về PlatformUser ⇒ token mới không có claim · refresh khi vẫn admin ⇒ có claim
- [ ] FAIL → hiện thực → PASS (`dotnet test backend/tests/IntegrationTests --filter PlatformAdminClaim`)
- [ ] Commit `feat(identity): claim role PlatformAdmin cho token vsite-portal + policy (#114)`

### T2: Tenant scope ngoài request + khung fan-out (#116)

- `TenantContext`: thay `internal set` bằng `internal void Set(TenantAudienceKind, Guid?)` ném
  `InvalidOperationException` nếu đã đặt. `TenantResolutionMiddleware` dùng `Set`.
- `ITenantScopeFactory` (Application/Common/Interfaces): `AsyncServiceScope BeginShopScope(Guid shopId)`.
  Hiện thực `Vsite.Api.Tenancy.TenantScopeFactory` (cùng assembly với `TenantContext`): `CreateAsyncScope()` →
  `Set(Shop, shopId)`. Đăng ký ở `Program.cs`.
- `IShopJob { Task ExecuteAsync(CancellationToken) }` — job con đọc shop qua `ITenantContext`, không nhận
  `shopId` trong logic.
- `Vsite.Infrastructure.Jobs.ShopFanOut`: `ListShopIdsAsync()` (mọi shop chưa xoá mềm, chỉ `Id`, không
  `IgnoreQueryFilters()`), `EnqueuePerShop<TJob>()` → mỗi shop một `IBackgroundJobClient.Enqueue<ShopJobRunner>(r
  => r.RunAsync<TJob>(shopId, CancellationToken.None))`.
- `ShopJobRunner.RunAsync<TJob>(shopId)`: kiểm shop còn tồn tại (chưa xoá mềm) ⇒ không thì log + return;
  `BeginShopScope` → resolve `TJob` trong scope → `ExecuteAsync`.
- `ModuleBoundaryTests.SharedSegments["Infrastructure"]` (và `["Application"]` nếu `Common/Jobs` chưa đủ) thêm
  `"Jobs"` kèm comment lý do (#115).
- [ ] Test trước `tests/IntegrationTests/Jobs/TenantScopeFactoryTests.cs`: scope mới có `ShopId` đúng · đặt hai
  lần ném · hai scope song song không lẫn `ShopId` · scope request (middleware) vẫn chạy như cũ
- [ ] Test trước `tests/IntegrationTests/Jobs/ShopFanOutTests.cs`: liệt kê gồm Draft/Active/Suspended/Closed,
  loại shop xoá mềm · enqueue đúng một job mỗi shop (fake `IBackgroundJobClient`) · runner no-op khi shop đã
  xoá mềm
- [ ] FAIL → hiện thực → PASS · `dotnet test backend/tests/ArchitectureTests`
- [ ] Commit `feat(jobs): BeginShopScope + khung fan-out theo shop (#116)`

### T3: Module `Platform` + `POST /api/platform/jobs/dashboard-session` (#114 mục 6, 8 · #115)

- `dependency-map.json`: `"Platform": { "dependsOn": ["Identity"], "phase": 2 }` + `_notes` trỏ #115.
- `Program.cs`: `AddOpenApi("platform", …)` (đủ 3 transformer như module khác); `api.MapPlatformEndpoints()`.
- `PlatformEndpoints`: `MapGroup("/platform").RequireAuthorization(AuthPolicies.PlatformAdmin).WithGroupName("platform")`;
  `POST /jobs/dashboard-session` `.WithName("createJobsDashboardSession")` → 204, `Produces` 401/403 ProblemDetails.
- `JobsDashboardCookie`: `IDataProtector` purpose `Vsite.Platform.JobsDashboard.v1`, payload `{ userId, expiresAt }`,
  `expiresAt = now + 15'` (hằng, ≤ TTL access token). Cookie `vsite_jobs_dashboard`: `HttpOnly`, `SameSite=Strict`,
  `Path=/hangfire`, `Secure` ngoài Development, `MaxAge` 15'.
- `ProblemDetailsAuthorizationMiddlewareResultHandler`: policy `PlatformAdmin` fail ⇒ 403 `PLATFORM_ADMIN_REQUIRED`
  (giữ `INSUFFICIENT_SCOPE` cho policy khác).
- [ ] Test trước `tests/IntegrationTests/Platform/PlatformPolicyTests.cs`: admin portal ⇒ 204 + Set-Cookie đúng
  thuộc tính · không token ⇒ 401 `UNAUTHORIZED` · user thường ⇒ 403 `PLATFORM_ADMIN_REQUIRED` · token shop có user
  admin ⇒ 403 · token `vsite-main` ⇒ 403
- [ ] Test trước `tests/IntegrationTests/Platform/PlatformRoutePolicyTests.cs`: mọi endpoint có route bắt đầu
  `api/platform/` mang policy `PlatformAdmin` · file trong `Vsite.Api/Tenancy/` không chứa `PlatformAdmin`
- [ ] FAIL → hiện thực → PASS · `ModuleBoundaryTests` + `ApiRoutePrefixTests` xanh
- [ ] Commit `feat(platform): module Platform + endpoint cookie dashboard job (#114, #115)`

### T4: Hangfire + dashboard

- Gói: `Hangfire.Core`, `Hangfire.AspNetCore`, `Hangfire.PostgreSql` — bản stable mới nhất đã phát hành ≥ 2 tuần,
  kiểm advisory (`dotnet list package --vulnerable`). Pin ở `Directory.Packages.props`.
- `JobsOptions` (`Jobs:ServerEnabled` mặc định `true`, `Jobs:WorkerCount` mặc định 2,
  `Jobs:TimeZone` = `Asia/Ho_Chi_Minh`, `Jobs:TokenCleanupGrace` = `00:00:00`).
- `HangfireSetup.AddJobs(config)`: storage PostgreSql (connection string hiện có, `SchemaName = "hangfire"`,
  `PrepareSchemaIfNecessary = true`); `AddHangfireServer` chỉ khi `ServerEnabled`.
- `Program.cs`: `app.MapHangfireDashboard("/hangfire", new DashboardOptions { Authorization = [filter],
  IsReadOnlyFunc = _ => false, AppPath = null })`.
- `JobsDashboardAuthorizationFilter` (`IDashboardAsyncAuthorizationFilter`): `TenantContext.AudienceKind == Portal`
  **và** cookie giải mã được, chưa hết hạn. Mọi lỗi giải mã ⇒ `false`, không ném.
- `ApiRoutePrefixTests.AllowedOutsideApi` thêm `"hangfire"` kèm comment lý do. `AppApiFactory` đặt
  `Jobs:ServerEnabled=false`.
- [ ] Test trước `tests/IntegrationTests/Platform/JobsDashboardTests.cs`: host admin + cookie hợp lệ ⇒ 200 · không
  cookie ⇒ 401 · cookie hết hạn ⇒ 401 · cookie bị sửa ⇒ 401 · cookie hợp lệ trên host shop ⇒ 401 · trên host
  `api.*` ⇒ 401
- [ ] FAIL → hiện thực → PASS
- [ ] Commit `feat(jobs): Hangfire trên Postgres + dashboard chỉ PlatformAdmin (#114)`

### T5: 3 job Identity + index + lịch (#116 ngoại lệ (a))

- Migration `AddTokenExpiresAtIndexes`: `ix_refresh_token_expires_at`, `ix_password_reset_token_expires_at`.
- `PendingRegistrationCleanupJob`, `ExpiredTokenCleanupJob` (hai bảng): `ExecuteDeleteAsync` theo lô 5.000 (lặp tới
  khi xoá < 5.000) với `ExpiresAt < now - grace`; log Information số dòng mỗi bảng.
- `ZaloIdentityIntegrityJob`: `Users.Where(Zalo && !ExternalLogins.Any(…))` — đếm + lấy ≤ 20 `Id`; > 0 ⇒ log
  Warning. Không sửa dữ liệu.
- `RecurringJobs.Register(IRecurringJobManager, JobsOptions)`: 3 job, cron `0 3 * * *`, múi giờ từ options.
  Gọi sau `app.Build()` khi `ServerEnabled`.
- [ ] Test trước `tests/IntegrationTests/Identity/IdentityJobsTests.cs`: xoá đúng dòng hết hạn, giữ dòng còn hạn
  (ca biên `ExpiresAt == now - grace` giữ lại) · token đã revoke nhưng còn hạn **không** bị xoá · grace = 1 ngày ⇒
  giữ dòng hết hạn < 1 ngày · lô > 5.000 xoá hết · Zalo thiếu `ExternalLogin` ⇒ có log Warning, đủ thì không ·
  `TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")` resolve được
- [ ] FAIL → hiện thực → PASS · `dotnet ef migrations has-pending-model-changes` sạch
- [ ] Commit `feat(identity): job dọn token/đăng ký hết hạn + integrity Zalo (#116)`

### T6: Test cách ly "job shop A không chạm shop B" (step.md 8a)

- Job mẫu **chỉ trong project test** `SampleShopJob : IShopJob` — đọc và cập nhật một entity tenant có sẵn (vd.
  `MediaAsset` hoặc `Page`: đổi một field vô hại), đếm số dòng nó thấy.
- [ ] Test `tests/IntegrationTests/Jobs/ShopJobIsolationTests.cs` (Postgres thật): seed shop A, B cùng dữ liệu ·
  `ShopJobRunner.RunAsync<SampleShopJob>(A)` ⇒ chỉ dòng của A đổi, B nguyên · job thấy đúng số dòng của A ·
  chạy cho shop xoá mềm ⇒ không đổi gì · fan-out qua `ShopFanOut` + chạy tuần tự từng job con ⇒ mỗi shop đổi
  đúng dữ liệu của mình
- [ ] PASS (khung đã có từ T2 — test này phải đỏ nếu bỏ `BeginShopScope`: thử tạm rồi hoàn lại, ghi vào changelog)
- [ ] Commit `test(jobs): job theo shop không chạm dữ liệu shop khác (#116)`

### T7: Kết thúc S1

- [ ] `dotnet test backend/vsite.sln` xanh toàn bộ (Docker có sẵn — không ghi nợ)
- [ ] `pnpm contract:export platform identity` → skill `contract-sync` → `contract-diff.md` → Gate 1 (#89)
- [ ] `backend/CLAUDE.md`: mục "Job nền" (quy ước #116, ngoại lệ, schema `hangfire` không qua EF migration, cờ
  `Jobs:*`) · `backend/docs/modules/platform.md` (#115, route, cookie) · `identity.md` (#114 ⇒ ✅ phần đã code, 3 job)
- [ ] `DECISIONS.md`: cột "Định nghĩa đầy đủ" của #115/#116 trỏ sang tài liệu bền; trạng thái #114–#116
- [ ] `changelog.md` (lệch có chủ đích / chưa làm xong) · `00-INDEX` §3 dòng 8a, §4 bỏ dòng IDENTITY-001 §B
- [ ] Nợ "Trước ra mắt": Caddy route `/hangfire` trên host `admin.*`; Data Protection key ring bền khi deploy
- [ ] `brief.md` cho S2 (endpoint, mã lỗi, claim, proxy)

## 10. Session S2: Frontend (mở SAU Gate 1)

### F1: Nút "Job nền"

- `pnpm gen:api` (sinh hook `createJobsDashboardSession`).
- `@vsite/api-sdk`: hàm đọc claim `role` của access token **hiện tại** (base64url payload, không verify; lỗi ⇒
  `[]`), kèm cách để UI phản ứng khi token đổi sau refresh.
- `jobs-dashboard-button.tsx` ở header `_authenticated.tsx`: ẩn khi không có role; bấm ⇒ mutation ⇒ 204 mở
  `/hangfire` tab mới; 403 ⇒ thông báo + ẩn; lỗi khác ⇒ thông báo, bấm lại được.
- `vite.config.ts`: proxy `/hangfire` → `http://localhost:5270`, `changeOrigin: false`.
- [ ] Test trước: đọc claim (token có / không role, token hỏng, sau khi đổi token) · nút ẩn/hiện · 204 mở tab ·
  403 · lỗi mạng (MSW)
- [ ] `pnpm --filter portal test` + typecheck + lint · `pnpm --filter @vsite/api-sdk test`
- [ ] Commit `feat(portal): nút mở dashboard job cho PlatformAdmin (#114)`

### F2: Nghiệm thu tay với BE thật (không MSW)

- [ ] Đặt `RoleId` của một user dev = PlatformAdmin (SQL tay), đăng nhập lại ⇒ thấy nút ⇒ vào dashboard
- [ ] User chủ shop thường ⇒ không thấy nút; gọi endpoint bằng devtools ⇒ 403
- [ ] `/hangfire` không cookie ⇒ chặn; mở từ host shop ⇒ chặn
- [ ] "Trigger now" 3 job ⇒ chạy thành công, log đúng số dòng
- [ ] Gỡ role ⇒ sau ≤ 15' + refresh nút biến mất

## 11. Đóng task (trước Gate 2)

- [ ] Integration tắt MSW (F2) · `change-reviewer` toàn nhánh (model mạnh nhất — chạm bảo mật/tenant) ·
  `review.md` kèm "Ma sát quy trình"
- [ ] `pnpm build && pnpm test` · `dotnet test backend/vsite.sln` · `pnpm check:docs`
- [ ] Nhắc người sửa `step.md` nếu cần (8a xong; 8d dùng khung #116)

## 12. Giả định tôi đã tự đặt (chưa hỏi, cần duyệt)

| # | Giả định | Vì sao | Nếu sai thì |
|---|---|---|---|
| G1 | Task ID `JOBS-001` | Hạ tầng dùng chung, không thuộc module có sẵn | Đổi tên thư mục |
| G2 | Lịch 03:00 giờ VN, cả 3 job; lô 5.000 dòng | Giờ thấp tải; lô tránh khoá bảng lâu | Đổi trong `JobsOptions` |
| G3 | Cookie dashboard 15', Data Protection purpose riêng, `SameSite=Strict`; tên `vsite_jobs_dashboard` | Khớp TTL access token (#114 mục 4); Strict chặn CSRF vào thao tác dashboard | Đổi hằng |
| G4 | 403 dùng mã mới `PLATFORM_ADMIN_REQUIRED`, policy khác giữ `INSUFFICIENT_SCOPE` | FE cần phân biệt "không phải admin" | Dùng `INSUFFICIENT_SCOPE` cho gọn |
| G5 | Endpoint `POST /api/platform/jobs/dashboard-session`, `operationId` `createJobsDashboardSession`, 204 không body | Tên theo nghiệp vụ (#91) | Chốt lại ở Gate 1 |
| G6 | Claim role tính bằng `RoleId == WellKnownRoles.PlatformAdminId` (GUID cố định) thay vì join `Role.Code` | Seed cố định GUID, bỏ một join | Join `Role.Code` nếu role có thể đổi id |
| G7 | Zalo integrity log ≤ 20 `UserId` | Log không phình | Đổi hằng |
| G8 | Portal không hiện trạng thái job (data-needs Q3) | Dashboard đã có | Thêm vào task màn quản trị |
| G9 | `TenantContext.Set` đặt `AudienceKind = Shop` cho job | Filter chỉ đọc `ShopId`; giữ một nghĩa "đang trong phạm vi một shop" | Thêm giá trị `Job` vào `TenantAudienceKind` |
| G10 | Hangfire storage dùng chung connection string và database với `AppDbContext` | Một deployment, một Postgres | Tách database riêng |
| G11 | Module `Platform` khai `phase: 2` | Làm cùng giai đoạn builder | Đổi số |

## 13. Verification tổng

- `dotnet test backend/vsite.sln` — xanh, gồm `ModuleBoundaryTests`, `IgnoreQueryFiltersAllowlistTests`
  (không đổi allowlist), `ApiRoutePrefixTests`, `ShopJobIsolationTests`.
- `pnpm build && pnpm test` · `pnpm check:docs`.
- Gate 1: `contracts/openapi/platform.v1.json` mới qua promote; `contract.lock` cập nhật.
- F2 đạt 5/5 trên trình duyệt thật.
- Chạy app: log khởi động có đăng ký 3 recurring job; schema `hangfire` tồn tại trong DB dev.
