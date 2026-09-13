# Docker test debt

> Nợ test cần Docker daemon (Testcontainers, hoặc `docker compose up` cho Postgres/Redis thật) mà
> session hiện tại KHÔNG chạy được — sandbox không có Docker. Máy có Docker đọc file này, chạy lại,
> rồi XOÁ đúng mục đã pass. Đây là nguồn sự thật DUY NHẤT cho loại nợ này — đừng ghi rải rác ở
> CLAUDE.md từng module hay báo miệng qua chat.

**Quy ước:**
- Thêm mục MỚI ở cuối danh sách "Pending" bên dưới, đừng sửa mục người/task khác đang chờ.
- Mỗi mục bắt buộc có: lệnh chạy chính xác (copy-paste được), lý do cần Docker, ngày thêm, task
  liên quan (nếu có), và tiêu chí "coi là xong".
- Chạy **thật** (không chỉ build) và **pass thật** → xoá nguyên mục khỏi file này. Lịch sử đã nằm ở
  git log của code/test liên quan, không cần giữ lại ở đây.
- Nếu FAIL → đừng xoá. Hoặc sửa bug rồi chạy lại tới khi pass, hoặc ghi rõ lỗi + để mục đó lại cho
  người tiếp theo (kèm ngày cập nhật gần nhất).
- File rỗng (chỉ còn "Pending" trống) là trạng thái mong muốn — không phải lỗi.

---

## ⚠️ 2026-09-13 — cả 3 mục dưới đây cần chạy LẠI sau refactor kiến trúc

Backend vừa gộp 8 project → 4 (`Vsite.Domain`/`Application`/`Infrastructure`/`Api`), hợp nhất
`IdentityDbContext` + `AppDbContextBase` thành MỘT `AppDbContext`, và **sinh lại migration từ đầu**
(`InitialSchema`, thay cho `InitialIdentitySchema` đã xoá). Đây là thay đổi thật ở tầng DB và DI —
kết quả test từ trước ngày này không còn giá trị tham chiếu, kể cả mục nào từng pass.

Đường dẫn lệnh trong cả 3 mục đã được cập nhật theo cấu trúc mới. Build + `ArchitectureTests` +
`ComponentSchemaTests` đã xanh trên máy không Docker; chỉ còn phần cần Docker là chưa xác nhận.

## Pending

### 1. Identity — Phase 1 entity/migration/tenant-isolation (IDENTITY-001)

Ngày thêm: 2026-09-13

```
cd backend
dotnet test tests/IntegrationTests
```

**Cần Docker vì:** `Testcontainers.PostgreSql` tự khởi động container Postgres thật cho mỗi lần
chạy test — không cần `docker compose up` trước, Testcontainers tự quản container riêng, chỉ cần
Docker daemon đang chạy.

**Coi là xong khi:** cả 8 test pass thật —
`InfrastructurePipelineTests.DbContext_connects_to_real_postgres_and_can_migrate`,
`TenantIsolationTests.Query_scoped_to_shop_A_does_not_see_shop_B_data`,
`TenantIsolationTests.Query_with_unresolved_tenant_sees_nothing_fail_closed`,
`DbConstraintTests.Migration_applies_and_seeds_seven_fixed_roles`,
`DbConstraintTests.Composite_FK_rejects_Platform_role_id_assigned_to_UserShop`,
`DbConstraintTests.Check_constraint_rejects_email_without_verified_at`,
`DbConstraintTests.Check_constraint_rejects_primary_identity_email_without_email`,
`DbConstraintTests.Unique_partial_index_rejects_duplicate_normalized_email`.

### 2. Identity — Tenant Resolution Middleware end-to-end (IDENTITY-001)

Ngày thêm: 2026-09-13

Chưa có test tự động nào cho `Vsite.Api.Tenancy.TenantResolutionMiddleware` (chỉ code-review, chưa
chạy thật với DB thật) — verify thủ công theo các bước sau (chưa có endpoint tạo Shop nên phải
insert tay), hoặc viết integration test mới (`WebApplicationFactory` + `Testcontainers.PostgreSql`)
thay thế bước thủ công này nếu tiện hơn khi thực hiện.

⚠️ **Kiểm tra connection string TRƯỚC khi chạy.** `docker-compose.yml` dựng Postgres ở
`localhost:5432`, user `vsite`/`vsite_dev_only`; còn `backend/src/Vsite.Api/appsettings.Development.json`
đang trỏ `localhost:5433`, user `postgres`/`password` (Postgres cài sẵn trên máy chính, không phải
compose). Cả `dotnet ef` lẫn app đều đọc appsettings, nên hai bên luôn khớp nhau — nhưng nếu máy
bạn dùng compose thì phải sửa appsettings (hoặc đổi port trong compose) cho khớp, nếu không mọi
bước dưới đây sẽ fail ở bước kết nối.

1. `docker compose up -d postgres redis` (từ root repo) — hoặc dùng Postgres/Redis sẵn có trên máy.
2. Thêm vào hosts file Windows (`C:\Windows\System32\drivers\etc\hosts`, cần quyền Admin):
   ```
   127.0.0.1  spa-abc.vsite.local
   ```
3. `cd backend && dotnet tool run dotnet-ef database update --project src/Vsite.Infrastructure --startup-project src/Vsite.Api`
4. Insert một row `Shop` test (Slug = `spa-abc`) trực tiếp vào DB (psql hoặc bất kỳ client nào).
5. `dotnet run --project src/Vsite.Api`
6. `curl -X POST http://spa-abc.vsite.local:5270/auth/register -H "Content-Type: application/json" -d "{\"email\":\"a@test.com\",\"password\":\"Password123!\"}"`
   → query bảng `PendingRegistration` xác nhận `ShopId` = đúng Id của Shop `spa-abc` vừa tạo ở bước 4.
7. `curl -X POST http://localhost:5270/auth/register -H "Content-Type: application/json" -d "{\"email\":\"b@test.com\",\"password\":\"Password123!\"}"`
   → xác nhận `PendingRegistration.ShopId` = `null` (context vsite-main, không có nhãn host nào khớp shop).
8. (tuỳ chọn) `curl -X POST http://admin.vsite.local:5270/auth/register ...` sau khi thêm hosts
   entry `admin.vsite.local` → hiện tại Register không phân biệt Portal/Main (audience chỉ ảnh
   hưởng Login/ForgotPassword), nên bước này chỉ để xác nhận middleware không lỗi, không cần check
   riêng.
9. (cache) `IShopLookupService` cache slug→ShopId qua Redis (30 phút found / 1 phút not-found) —
   gọi lại bước 6 LẦN NỮA ngay sau đó, xác nhận vẫn ra đúng `ShopId` (phục vụ từ cache, không lỗi).
   Muốn xác nhận cache THẬT SỰ được dùng (không chỉ tình cờ đúng): tắt Postgres
   (`docker compose stop postgres`) rồi gọi lại bước 6 — vẫn phải ra đúng `ShopId` vì phục vụ từ
   Redis, không chạm DB. Bật lại Postgres sau khi xong.

**Coi là xong khi:** bước 6, 7, 9 cho đúng `ShopId` như mô tả — middleware resolve tenant từ Host
header chính xác, không lẫn giữa hai context.

### 3. Identity — Phase 3 token-scope tests qua HTTP pipeline thật (IDENTITY-001)

Ngày thêm: 2026-09-13

```
cd backend
dotnet test tests/IntegrationTests --filter "FullyQualifiedName~TokenScopeTests"
```

**Cần Docker vì:** `IdentityApiFactory` (`WebApplicationFactory<Program>`) dựng CẢ Postgres
(dùng chung `PostgresFixture` qua collection) LẪN Redis (`Testcontainers.Redis`, container riêng)
— toàn bộ pipeline HTTP thật (`TenantResolutionMiddleware` → JWT auth →
`ShopMembershipValidationMiddleware` → `RequireGlobalScope`) cần cả hai chạy thật, không mock được.
Đã build thành công, đã chạy thử ở sandbox không Docker và xác nhận lỗi DUY NHẤT là
`DockerEndpointAuthConfig`/"Docker is either not running" — nghĩa là wiring DI/fixture đúng, chỉ
thiếu Docker daemon.

**Coi là xong khi:** cả 6 test trong `TokenScopeTests` pass thật —
`GetMe_allows_main_token`, `GetMe_allows_shop_token`, `ListMyShops_allows_main_token`,
`ListMyShops_rejects_shop_token_with_insufficient_scope` (khẳng định 403 +
`error_code: INSUFFICIENT_SCOPE` — trọng tâm Quyết định #32/03 §7.1),
`ChangePassword_allows_main_token`, `ChangePassword_allows_shop_token`.
