# JOBS-001 — changelog (Bước 8a: Hangfire, quy ước job nền, quyền `PlatformAdmin`)

Ghi chỗ thực thi khác với [`plan.md`](plan.md) cùng nguyên nhân, để người duyệt biết chỗ nào là **lệch có chủ đích**
(giữ nguyên) và chỗ nào là **việc chưa làm xong**. Phạm vi: **S1 (backend) T1–T7 xong, Gate 1 đã promote.** S2 (FE)
chưa mở — việc FE ở [`brief.md`](brief.md).

**Đọc kèm:** [`contract-diff.md`](contract-diff.md) (Gate 1) · `backend/CLAUDE.md` §Job nền (#116) ·
`backend/docs/modules/platform.md` (#115, cookie, dashboard) · `backend/docs/modules/identity.md` §#114 và §Job nền của Identity.

**Commit:** `9054802` (T1) · `5e8a091` + `c126e98` (T2) · `ba8a254` (T3) · `68623e7` (T4) · `dbb2f77` + `a36314e` (T5) · `dc92b41` (T6) · S2/FE: `a7ba587` (orval platform + gen:api) · `cc67937` + `3b9a0af` (F1) · `695660b` (fix review F1) · `615f2fd` + commit ghi F2 (docs).

---

## Kiểm chứng

`dotnet test backend/vsite.sln`, chạy trên bản copy ở commit `dc92b41` (xem "Môi trường test" bên dưới):

| Project | Kết quả |
|---|---|
| `ArchitectureTests` | 22/22 |
| `IntegrationTests` | 1045 pass, 1 skip (có sẵn từ trước), 0 fail |
| `ComponentSchemaTests` | 12/12 |

Máy có Docker (Testcontainers chạy thật) ⇒ **không có nợ Docker**, `Docs/DOCKER-TEST-DEBT.md` không thêm mục.
`dotnet list package --vulnerable --include-transitive`: không có gói dễ tổn thương ở cả 7 project (sau khi pin Newtonsoft, mục 2).

**Gate 1:** `platform.v1` (một endpoint mới `createJobsDashboardSession`) đã promote với `approvedBy = gate1-pre-production-89`
(#89: chỉ thêm, không câu hỏi). `identity.v1` không đổi — claim `role` nằm trong JWT, không trong OpenAPI.

### Kiểm chứng S2/FE (2026-11-25)
`pnpm build && pnpm test` xanh 10/10 task: portal 434 test, `@vsite/api-sdk` 29 vitest + 7 barrel; typecheck + lint sạch; `pnpm check:docs` OK. Test viết trước, đỏ trước khi implement (api-sdk 15/15, portal không load được); vòng sửa review có 2 test đỏ trước (popup chặn, refresh cũ đè token mới).

### Bằng chứng T6 — test cách ly thật sự bắt được lỗi (plan §9 T6)

Test `ShopJobIsolationTests` (4 ca, Postgres thật) phải **đỏ nếu bỏ cơ chế đặt tenant**. Có hai lần thử tạm, đều ở bản copy,
đều đã hoàn lại:

1. **Fail-closed — `Set(Main, null)`** trong `TenantScopeFactory.BeginShopScope` (scope không có tenant shop): 3/4 ca đỏ với
   `Expected: 3 Actual: 0` (job thấy 0 dòng); ca shop xoá mềm vẫn xanh (job không bao giờ chạy). **Chứng minh hành vi
   fail-closed (không có tenant ⇒ rỗng), KHÔNG chứng minh phát hiện rò rỉ.**
2. **Rò rỉ thật — `isShopScoped = false`** trong `TenantQueryFilterExtensions` (bộ lọc tenant tắt cho mọi entity): 3/4 ca đỏ,
   job của shop B thấy **21 dòng thay vì 2**; ca "A đổi, B nguyên" đỏ ở dòng `Expected: 2` của B. Hoàn lại, chạy lại **4/4 xanh**.
   Đây mới là bằng chứng test bắt được rò rỉ chéo shop.

Bẫy khi thử: `robocopy` giữ mtime nên sau khi hoàn lại, build tăng dần giữ DLL cũ (7 fail giả) — phải `touch` file rồi build lại.
Lần chạy cuối là trên nguồn thật.

### Môi trường test (ghi lại để người sau khỏi vấp)

Một process `Vsite.Api.exe` (PID 4300 — dev server của người dùng, mở từ 2026-11-22) **khoá DLL trong `src/Vsite.Api/bin`**
nên không build được tại chỗ. Quyết định (Ruling 4): **không kill** process của người dùng; mọi build/test chạy trên bản copy
`C:/tmp/vsite-copy` (robocopy bỏ `.git`/`node_modules`/`bin`/`obj`, ~1.6 GB, xoá được). `--artifacts-path` không dùng được
vì test tìm repo root theo đường dẫn bin. Khi PID 4300 đã tắt thì chạy `dotnet test backend/vsite.sln` thẳng trong repo.

---

### Nghiệm thu F2 (BE thật, trình duyệt thật — người chạy 2026-11-25)

| # | Kiểm | Kết quả |
|---|---|---|
| 1 | Admin đăng nhập Portal, bấm "Job nền" → tab `/hangfire` (`http://admin.vsite.local:5173/hangfire`) | ✅ đạt |
| 2 | Chủ shop thường: không thấy nút; POST bằng devtools ⇒ 403 `PLATFORM_ADMIN_REQUIRED` | ✅ đạt |
| 3 | `/hangfire` không cookie / từ host shop bị chặn | ✅ đạt |
| 4 | "Trigger now" 3 recurring job chạy thành công, log đúng số dòng | ✅ đạt |
| 5 | Gỡ role bằng SQL ⇒ sau ≤15' + refresh nút biến mất | ✅ đạt |

Nguồn: người báo bằng lời ("các dòng 2–5 cũng ok"), agent không tự chứng kiến; không có chi tiết từng dòng.

## Lệch có chủ đích (giữ nguyên, không phải bug)

### JOBS-001-D1 (W1 CSRF) — dashboard chặn POST khác origin

- **Vấn đề:** `/hangfire` không chống CSRF cho thao tác ghi: `SameSite=Strict` không chặn request từ `{slug}.vsite.*` (cùng site với `admin.*`), `IAntiforgery` không đăng ký nên Hangfire bỏ kiểm token.
- **Thực thi:** `JobsDashboardAuthorizationFilter` thêm kiểm same-origin cho method ngoài GET/HEAD/OPTIONS, sau kiểm cookie + audience Portal: có `Origin` ⇒ host[:port] phải bằng `Host` request (không so scheme); không có `Origin` ⇒ chỉ nhận `Sec-Fetch-Site: same-origin`; còn lại (kể cả `Origin` hỏng/"null") ⇒ false ⇒ 401. GET/HEAD/OPTIONS giữ nguyên. Test: `JobsDashboardTests` (viết trước, đỏ 9/18, rồi xanh).
- **Vì sao không `AddAntiforgery()`:** Hangfire chỉ kiểm token khi có `IAntiforgery`, nhưng việc đó kéo thêm cookie/state antiforgery cho một dashboard chỉ-admin; kiểm Origin/Sec-Fetch-Site không thêm state, nằm gọn trong filter sẵn có và test được bằng integration test.
- **Cơ sở:** JS ajax và form POST của dashboard trình duyệt luôn gửi `Origin`; client không phải trình duyệt (không Origin, không Sec-Fetch-Site) bị từ chối có chủ đích.

### 1. `Hangfire.Core` thêm ở T2, không phải T4

- **Plan nói gì:** T4 mới thêm gói Hangfire.
- **Thực thi:** T2 thêm `Hangfire.Core` 1.8.25 (Ruling 1); T4 thêm `Hangfire.AspNetCore` 1.8.25 + `Hangfire.PostgreSql` 1.21.1.
- **Nguyên nhân:** `ShopFanOut` (T2) cần `IBackgroundJobClient` ngay. Chỉ đổi chỗ commit gói. Cả ba gói phát hành ≥ 2 tuần trước 2026-11-24.

### 2. Pin `Newtonsoft.Json 13.0.4`

- **Nguyên nhân:** `Hangfire.Core` kéo transitive `Newtonsoft.Json 11.0.1` dính advisory **GHSA-5crp-9r3c-p9vr** (High). Pin ở
  `Directory.Packages.props` + `PackageReference` ở `Vsite.Infrastructure`. Sau đó cả 7 project sạch.
- **Điều kiện đảo lại:** khi Hangfire.Core nâng phụ thuộc Newtonsoft lên bản đã vá thì bỏ pin.

### 3. `TenantContext.AttachShop` và `ShopMembershipEndpointFilter` trả 403 thay vì 500 trên host shop

- **Plan nói gì:** `TenantContext.Set` đặt một lần, thay hẳn `internal set`.
- **Thực thi:** thêm `internal void AttachShop(Guid)` bên cạnh `Set`. `ShopMembershipEndpointFilter` (SHOP-001, #31) hợp lệ khi
  gắn `ShopId` vào context Portal đã có audience — đi qua `Set` sẽ ném. `AttachShop` ném nếu `ShopId` đã đặt hoặc `Set`
  chưa gọi, nên tenant vẫn không bị ghi đè.
- **Hệ quả (review T2 phát hiện I1):** token global gọi route `{shopId}` trên **host shop** (đã có `ShopId` từ Host) làm
  `AttachShop` ném ⇒ 500. Sửa (Ruling 5): filter từ chối **trước** `AttachShop` bằng `ForbiddenAccessException("SHOP_ACCESS_DENIED")`
  = 403 + mã **có sẵn**, không thêm mã mới (tránh đổi contract). Trước đây hành vi này là lỗi tiềm ẩn chưa ai chạm.
  Test: `ShopEndpointTests.GetShop_from_a_shop_host_with_a_global_token_returns_403_SHOP_ACCESS_DENIED_not_500`.

### 4. R4 làm bằng "scope mới từ root, tenant của request không bị chạm" — không ném

- **Plan nói gì:** R4 — `BeginShopScope` gọi trong scope request đã có tenant ⇒ `InvalidOperationException`.
- **Thực thi (Ruling 6):** `TenantScopeFactory` luôn `CreateAsyncScope()` từ root provider ⇒ không bao giờ ghi đè được scope request;
  không kiểm `HttpContext`, không ném khi gọi trong request. Ném khi `Set` lần hai vẫn có, nhưng ở mức `TenantContext`.
- **Nguyên nhân:** bất biến cần giữ là "tenant của request không bị ghi đè" — đạt bằng cấu trúc, không cần nhánh phát hiện.
  Test `BeginShopScope_from_inside_a_request_scope_leaves_the_request_tenant_untouched` chứng minh.
- `BeginShopScope` còn từ chối `Guid.Empty` (`ArgumentException`); `EnqueuePerShop<TJob>` trả `int` (số job đã enqueue) thay vì `void`.

### 5. `InternalsVisibleTo IntegrationTests` ở `Vsite.Api`

- Test gọi `TenantContext.Set` (internal). Hệ quả đã biết: mở `Set` cho cả assembly test (review T2 ghi minor).

### 6. `JobsDashboardCookie` nhận `bool isDevelopment` thay vì `IHostEnvironment`

- Để dựng được trong unit test không cần host. Đăng ký bằng factory lambda ở `Program.cs`. `Secure = !isDevelopment`.
  Thêm `Produces(204)` và `WithTags("Platform")` ngoài danh sách của plan.

### 7. `PLATFORM_ADMIN_REQUIRED` nhận biết bằng endpoint metadata

- **Plan nói gì:** "policy `PlatformAdmin` fail ⇒ `PLATFORM_ADMIN_REQUIRED`".
- **Thực thi:** `AuthorizationPolicy` không mang tên policy ⇒ `ProblemDetailsAuthorizationMiddlewareResultHandler` đọc `IAuthorizeData.Policy`
  từ metadata của endpoint. Nhánh này **chỉ chọn mã lỗi** (authenticated + policy tên `PlatformAdmin` ⇒ `PLATFORM_ADMIN_REQUIRED`, còn lại
  `INSUFFICIENT_SCOPE`), **không phải bypass** (#114 mục 6).
- **Giả định ngầm (comment ở handler):** nhóm `/platform` hiện chỉ có một policy. Thêm policy thứ hai vào nhóm thì phải xét lại cách chọn mã.

### 8. Dashboard dùng `AsyncAuthorization`

- Hangfire `DashboardOptions.Authorization` là kiểu đồng bộ; filter của ta là `IDashboardAsyncAuthorizationFilter` ⇒ đặt ở `AsyncAuthorization`.
  `Authorization = []` được đặt rõ để **bỏ mặc định `LocalRequestsOnly`** (cổng thật là filter async).

### 9. Mã lỗi R1 theo host

- Token `shop:*` của user có role PlatformAdmin gọi `/api/platform/*`:
  - từ **host shop** ⇒ 403 `PLATFORM_ADMIN_REQUIRED` (policy chặn);
  - từ **host admin** ⇒ 403 `TOKEN_SHOP_MISMATCH` (`ShopMembershipValidationMiddleware` chặn trước policy).
  Portal không giữ token shop nên FE chỉ gặp `PLATFORM_ADMIN_REQUIRED`. Đã ghi ở `contract-diff.md` giả định A3.

### 10. `JobsOptions` đọc lazy — T4 đọc eager làm R8 hỏng âm thầm (lỗi gốc, T5 sửa)

- **Phát hiện ở T5:** T4 đọc `JobsOptions` eager từ `builder.Configuration` lúc `AddJobs`. `WebApplicationFactory.ConfigureAppConfiguration`
  áp **sau** khi `Program.cs` chạy tới đó ⇒ `Jobs:ServerEnabled=false` của mọi test factory bị lờ ⇒ worker Hangfire thật vẫn chạy trong test
  (R8 bị phá mà test T4 vẫn xanh). Bằng chứng: `Register` chạy trong `IdentityApiFactory` dù factory đặt `false`.
- **Sửa:** `JobsOptions` đọc lazy khi resolve; storage dùng `AddHangfire((sp, cfg) => …)`; worker bọc cổng bằng cách thay descriptor
  `IHostedService` do `AddHangfireServer` thêm bằng factory trả `DisabledHostedService` khi tắt (ném nếu thư viện đổi cách đăng ký).
  Test `Test_host_with_server_disabled_…` khẳng định không có `BackgroundJobServerHostedService`. Recurring job chỉ đăng ký khi `ServerEnabled`.

### 11. Khởi động chịu lỗi: `PrepareSchema` + `StartupConnectionMaxRetries`

- Hai khoá cấu hình thêm ngoài plan: `Jobs:PrepareSchema` (mặc định `true`) và `Jobs:StartupConnectionMaxRetries` (mặc định `2`, mặc định thư viện ~5 lần ≈ 32 s).
- Số đo stall khi dựng storage với DB không tới được: thư viện mặc định 32.0 s · `retries=2` 13.9 s · `retries=0` 4.1 s ném `NpgsqlException` ·
  `PrepareSchema=false` 0 s. Host test / build-time OpenAPI (`ServerEnabled=false`) không dựng storage ⇒ ~0 s.

### 12. Phát hiện sinh OpenAPI lúc build ở `Program.cs`

- `OpenApiGenerateDocumentsOnBuild` chạy host thật dưới entry assembly `GetDocument.Insider` và từng đăng ký worker vào DB dev lúc build.
  `Program.cs` có **một** chỗ nhận ra entry assembly này và đặt `Jobs:ServerEnabled=false` vào `builder.Configuration` trước `AddJobs`.
  (T4 đặt check ở Infrastructure — sai tầng, bỏ ở T5.) Dễ vỡ nếu tên entry assembly đổi — review T4 ghi minor.

### 13. Múi giờ: fallback `SE Asia Standard Time`

- **Plan nói gì (R6):** dùng IANA `Asia/Ho_Chi_Minh`, test resolve được.
- **Thực thi:** `backend/Directory.Build.props` đặt `InvariantGlobalization=true` (không ICU) ⇒ trên Windows dev
  `TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")` **không resolve**. `RecurringJobs.ResolveTimeZone` thử IANA trước, rồi fallback
  `SE Asia Standard Time` (cùng UTC+7, không DST nên lịch như nhau). Test khẳng định `tz.Id ∈ {"Asia/Ho_Chi_Minh","SE Asia Standard Time"}`.
- **⚠️ Linux prod:** image phải có **tzdata** (alpine mặc định không có) — nếu không, cả hai id đều không resolve và `Register` ném. Ghi ở "Trước ra mắt".

### 14. Hằng `ExternalLoginProviders`

- **Plan nói gì:** `Any(…)` để trống. T5 đầu tiên viết `"zalo"` (giả định). Review T5 (I1) phát hiện lệch `03` §3.2 (PascalCase `Zalo`) ⇒ job báo Warning giả mỗi ngày.
- **Sửa (Ruling 8):** thêm `Vsite.Domain.Identity.ExternalLoginProviders` (`Google`/`Facebook`/`Zalo`), dùng chung cho job và seed test.
  Không đổi schema, không migration. Hết giả định "zalo".

### 15. Grace là tham số của job, không inject `JobsOptions`

- `RunAsync(TimeSpan grace, CancellationToken)`: `JobsOptions` nằm ở Infrastructure, Application không được tham chiếu. `RecurringJobs.Register` truyền
  `options.TokenCleanupGrace`. Hệ quả: **đổi `Jobs:TokenCleanupGrace` chỉ có hiệu lực sau khi `Register` chạy lại** (mỗi lần khởi động —
  `AddOrUpdate` idempotent); lịch đã lưu trong storage mang giá trị cũ tới lúc đó.

### 16. Những điều nhỏ khác

- `AppApiFactory.cs` thực chứa `IdentityApiFactory`; `Jobs:ServerEnabled=false` đặt ở đó, `MediaApiFactory`, `ShopApiFactory` và 5 chỗ dựng host giả
  connection (8 chỗ).
- Lô xoá dùng `DELETE … WHERE id IN (SELECT id … ORDER BY id LIMIT 5000)` (EF `ExecuteDeleteAsync` không có LIMIT trực tiếp). Một câu SQL mỗi lô, không nạp dòng vào bộ nhớ.
- `SampleShopJob` (test) đăng ký trong `ServiceCollection` riêng của test (cùng mẫu `ShopFanOutTests`), không phải DI của host API; entity dùng là `MediaAsset.AltText`
  (không dùng `Page` vì cần FK Website).
- Fan-out trong test gọi job qua `job.Method.Invoke(runner, job.Args)` — bỏ qua serializer/activator của Hangfire (review T6 ghi minor).
- `appsettings*.json` **không** có mục `Jobs:*` (plan §8 dự kiến thêm) — mặc định nằm ở `JobsOptions`; chỉ ghi đè khi cần.
- Hằng cookie ngắn hạn 15 phút nằm ở `JobsDashboardCookie.Lifetime`, không phải cấu hình.
- Phát hiện khảo sát đúng như plan §2: 3 job `03` chạm bảng không tenant ⇒ test cách ly phải dùng job mẫu (người chốt).

---

### 17. (S2/FE) `scripts/lib/barrel.mjs` bỏ file zod rỗng
`platform.zod.ts` do Orval sinh ra chỉ có comment (endpoint không body/param) → `export *` gây TS2306 ở typecheck api-sdk lẫn portal. Sửa gốc ở script barrel (bỏ file không có `export`, có test), chạy lại `pnpm gen:api`; không sửa tay `generated/`. Entry `platformZod` giữ theo brief.

### 18. (S2/FE) Mutator: `refreshAccessToken` catch chỉ xoá token khi `epoch === sessionEpoch`
Lỗi có sẵn (refresh cũ hỏng đè token phiên đăng nhập mới) — lộ ra vì nút "Job nền" phản ứng theo token. Có test.

### 19. (S2/FE) `isForbidden` đọc AxiosError thô theo hình dạng
Portal không phụ thuộc axios. 403 nhận biết qua `response.status`, `ProblemDetails.status`, hoặc `error_code` `PLATFORM_ADMIN_REQUIRED`/`TOKEN_SHOP_MISMATCH`. 403 không status và error_code lạ sẽ hiện lỗi chung (BE luôn gửi status).

### 20. (S2/FE) `window.open` bị chặn popup
Reviewer chỉ ra `window.open` chạy sau await (Safari có thể chặn). Thêm: trả `null` ⇒ hiện liên kết `/hangfire` dự phòng. Brief chỉ định `window.open`, giữ nguyên.

### 21. (S2/FE) Đường dẫn file nút
Plan §8 ghi `apps/portal/src/components/platform/jobs-dashboard-button.tsx`; file thật là `apps/portal/src/components/jobs-dashboard-button.tsx` (không có thư mục `platform/`).

## Chưa làm xong / nợ

### Việc của session sau

1. ~~S2 FE (F1–F2)~~ — xong (mục S2/FE bên dưới).
2. ~~`pnpm gen:api` + entry `platform`~~ — xong ở `a7ba587`.
3. ~~`change-reviewer` toàn nhánh~~ — xong, xem `review.md`. **W1 (CSRF dashboard Hangfire, BE) chưa sửa** — chờ người quyết ở Gate 2.
4. Nhắc người sửa `DesignIdeal/step.md` (8a xong, 8d dùng khung #116) — S1 không được sửa file đó.

### Nợ "Trước ra mắt"

| Nợ | Chi tiết |
|---|---|
| **Caddy** | Route `/hangfire` trên host `admin.*` sang BE (cùng dòng nợ "Caddy phải route `/media/*` và `/api/*`" của WEBSITE-005). Cookie host-only nên POST `/api/platform/jobs/dashboard-session` và GET `/hangfire` phải đi qua **cùng host** |
| **Data Protection key ring bền** | Mặc định key ring theo máy/profile (trong container thì mất khi restart, nhiều instance thì không giải mã chéo được). Cookie 15 phút nên chỉ làm người dùng bấm lại nút — chấp nhận ở dev; deploy thật phải persist key |
| **tzdata trong image Linux** | Xem lệch 13 |
| **Postgres chết lúc boot khi `ServerEnabled=true`** | `Register` chờ ~14 s (2 lần thử lại) rồi theo chế độ suy giảm của thư viện **có thể không ném** (job không được lên lịch mà host vẫn chạy) hoặc crash. Chưa có runbook; quan sát lúc deploy đầu tiên |

### Minor hoãn từ review (ledger)

- T1: literal `"role"` còn nằm trong `JwtTokenService`/`AuthenticationSetup` (cùng style `"aud"`/`"ownerShopIds"`).
- T2: một test async thừa; test middleware viết tay chưa phủ nhánh admin/reserved; `InternalsVisibleTo` mở `Set` cho cả assembly test; test F2 dùng `Set(Portal, X)` thay vì `(Shop, X)` (cosmetic).
- T3: tamper-test có xác suất flaky ≈ 64⁻³ (bỏ qua).
- T4: dashboard POST mới chứng minh filter cho/chặn (trigger job thật chưa test — nghiệm thu tay F2); guard `GetDocument.Insider` dễ vỡ (lệch 12).
- T5: test dọn Zalo bỏ qua điều kiện provider khi seed.
- T6: fan-out gọi job bằng reflection (bỏ qua serializer/activator); test B không kiểm DB đổi bằng truy vấn riêng.
- Giả định G1–G11 của plan §12 giữ nguyên, **ngoại trừ** G9 (đặt `AudienceKind = Shop` cho job — giữ) và G10 (Hangfire dùng chung connection `Identity` và database — giữ); không giả định nào bị đảo.

### S2/FE — hoãn
- Interceptor vẫn gọi `onSessionExpired` khi refresh stale-epoch hỏng (logout rồi login nhanh lúc refresh cũ đang bay) — có sẵn, ngoài phạm vi nút; token mới đã không bị xoá nhưng store có thể bị clear. Cần để `refreshAccessToken` báo "stale".
- `popupBlocked` không reset khi nút bị ẩn (cosmetic).
- Portal MSW server mặc định chưa có handler platform (test tự override).
- Kiểm Safari/popup ở trình duyệt chưa được báo cáo (F2 chỉ xác nhận chung 5 dòng).
- Sau review toàn nhánh (Suggestion, chưa làm): `DisplayStorageConnectionString=false`; cân nhắc `hangfire` vào reserved-routes hoặc ghi "chỉ admin.*" ở nợ Caddy; mở rộng test dò bypass; `denied` không reset khi token đổi; ngoại lệ `ApiRoutePrefixTests` nên là `"hangfire/"`.
- Plan §6 dòng "Policy kiểm claim và audience" trích `PlatformPolicyTests` — test chứng minh thật là Theory `PlatformAdminClaimTests.Policy_requires_both_role_and_portal_audience`.

### Chưa nằm trong phạm vi (đúng plan §1)

Job của builder (8d) · màn quản trị/thống kê nền tảng và helper đọc chéo shop (#114 mục 7) · cấp role PlatformAdmin qua UI (vẫn sửa DB tay) ·
kênh thông báo cho integrity job (chỉ log Warning) · outbox (M2) · 5 feedback WEBSITE-007.

### Dọn

- `C:/tmp/vsite-copy` (~1.6 GB) xoá được. Schema `hangfire` có thể đã bị tạo trong DB dev bởi lần sinh OpenAPI build-time của T4 (trước fix lệch 12) — vô hại, schema do Hangfire quản lý.
