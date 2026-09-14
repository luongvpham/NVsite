# IDENTITY-001 — changelog: lệch so với thiết kế

Ghi lại mọi chỗ thực thi khác với `DesignIdeal/03-identity-entity-design.md` cùng nguyên nhân, để
người duyệt tài liệu gốc biết chỗ nào là **lệch có chủ đích** (giữ nguyên) và chỗ nào là **việc chưa
làm xong** (theo dõi ở task sau).

**Viết bổ sung ngày 2026-09-14**, sau khi task đã merge (`feature/identity-phase3`) — file này đáng
lẽ phải có từ lúc Gate 2. Nội dung dựng lại bằng cách đối chiếu `03` với code thật, không phải ghi
lúc làm, nên **mục "chưa làm xong" có thể chưa đủ** — bổ sung khi phát hiện thêm.

**Đọc kèm:**
- `backend/docs/modules/identity.md` — quy ước vận hành module (invariant, Tenant Resolution
  Middleware, nợ bàn giao bảng `Shop`). Chi tiết kỹ thuật sâu hơn file này.
- `Docs/tasks/IDENTITY-001/contract-diff.md` — Gate 1, khác biệt ở tầng API contract.

> ⚠️ `backend/docs/modules/identity.md` §Trạng thái trỏ test tới
> `backend/tests/Identity.IntegrationTests/TokenScopeTests.cs` — **đường dẫn đó đã sai** sau lần gộp
> 8 project → 4 (2026-09-13). Đường dẫn thật: `backend/tests/IntegrationTests/Identity/TokenScopeTests.cs`.

---

## Lệch có chủ đích (giữ nguyên, không phải bug)

### 1. Không có cột `PasswordSalt` trên `User`, `UserShop`, `PendingRegistration`

- **Thiết kế nói gì:** `03` §3.1, §3.3, §5 đều vẽ cặp cột `PasswordSalt` + `PasswordHash`. Ở §5 cột
  này còn là `NOT NULL`.
- **Thực thi:** chỉ có `PasswordHash`. Dùng `Microsoft.AspNetCore.Identity.PasswordHasher<User>`.
- **Nguyên nhân:** output của hasher này đã tự chứa salt + iteration count trong **một** chuỗi. Giữ
  cột `PasswordSalt` thì nó luôn rỗng — một cột chết mà agent sau sẽ tưởng là phải điền. Đúng khuyến
  nghị "không tự cài salt+hash" ở `03` §9 rủi ro #2.
- **Điều kiện đảo lại:** nếu đổi sang thư viện hash cần lưu salt riêng thì thêm lại cột, cả 3 bảng.

### 2. `RefreshToken` và `PasswordResetToken` là entity mới, `03` không có

- **Thiết kế nói gì:** `03` §3 liệt kê 5 entity (`User`, `ExternalLogin`, `UserShop`, `Shop`,
  `Role`) + `PendingRegistration` ở §5. Không có hai bảng token.
- **Thực thi:** thêm `RefreshToken` và `PasswordResetToken`, cùng khuôn với `PendingRegistration`
  (lưu `TokenHash` chứ không lưu raw, có `ExpiresAt`, one-time qua `ConsumedAt`/`RevokedAt`).
- **Nguyên nhân:** Quyết định #3 yêu cầu refresh token **rotation, lưu DB, revoke được** — không có
  bảng thì không hiện thực được. `03` §6.4 yêu cầu reset password **scoped theo audience/shop** —
  cần lưu `Audience` + `ShopId` của token. Đây là chi tiết triển khai của quyết định đã chốt, không
  phải entity nghiệp vụ mới.
- **Đáng chú ý:** cả hai đều có `Audience` (`vsite-main` | `shop:{shopId}` | `vsite-portal`) và
  `ShopId` **nullable**, và **không** kế thừa `ShopEntity` — vì null ở đây nghĩa là "không thuộc
  shop nào", khác ngữ nghĩa tenant-scoped mà `ShopEntity` yêu cầu. `RefreshToken.ShopId` là thứ làm
  cho Quyết định #32 thành thật: dò password ở Shop C không khoá được tài khoản ở Shop A.

### 3. `Shop` là bản trích tối thiểu, tạm do module Identity sở hữu

- **Thiết kế nói gì:** `03` §3.4 đã tự khai là bản trích, nguồn sự thật đầy đủ ở `04` §2.1 (#39.5).
- **Thực thi:** `Vsite.Domain/Identity/Entities/Shop.cs` — đúng 6 field của bản trích, không thêm.
- **Nguyên nhân:** `UserShop` cần FK hợp lệ, mà module `Shop` đầy đủ chưa làm.
- **⚠️ Nợ bàn giao:** khi làm module `Shop`, phải chuyển entity + `IShopLookupService` + đảo
  `dependency-map.json` thành `Shop.dependsOn = ["Identity"]` **cùng lúc**. Chi tiết ở
  `backend/docs/modules/identity.md` §"Nợ kỹ thuật đã biết".

### 4. Tên ràng buộc DB khác tài liệu (nội dung thì đúng đủ)

- **Thiết kế nói gì:** `03` §4 đặt tên `ux_user_email`, `uq_role_id_scope`, `fk_user_role`,
  `fk_usershop_role`, `uq_usershop`, `uq_external`.
- **Thực thi:** chỉ 2 CHECK constraint giữ đúng tên (`ck_user_email_verified`,
  `ck_user_primary_identity`, khai tay ở `UserConfiguration`). Còn lại dùng tên mặc định của EF Core:

  | `03` §4 | Tên thật trong DB | Đủ nội dung? |
  |---|---|---|
  | `[1]` `ux_user_email` | `IX_User_EmailNormalized` (unique, `filter: "EmailNormalized" IS NOT NULL`) | ✅ |
  | `[2]` `ck_user_email_verified` | giữ nguyên tên | ✅ |
  | `[3]` `ck_user_primary_identity` | giữ nguyên tên | ✅ |
  | `[4]` `uq_role_id_scope` | `AK_Role_Id_Scope` (alternate key) | ✅ |
  | `[4]` `fk_user_role` / `fk_usershop_role` | FK mặc định EF trên `(RoleId, RoleScope)` → `Role(Id, Scope)` | ✅ |
  | `[5]` `uq_usershop` | `IX_UserShop_UserId_ShopId` (unique) | ✅ |
  | `[6]` `uq_external` | `IX_ExternalLogin_Provider_ProviderUserId` (unique) | ✅ |

- **Nguyên nhân:** đặt tên tay cho index/FK trong EF Core phải viết thêm `.HasDatabaseName()` ở mọi
  chỗ và dễ lệch giữa migration với model snapshot. Tên không phải ràng buộc nghiệp vụ.
- **Hệ quả cần biết:** **đừng grep tên trong `03` §4 để kiểm tra ràng buộc có tồn tại hay không** —
  sẽ ra rỗng và tưởng là thiếu. Grep theo bảng trên.
- `RoleScope` là computed column `stored: true` (`HasComputedColumnSql("'Platform'", stored: true)`),
  đúng tinh thần `GENERATED ALWAYS AS` của `03` §3.1/§3.3.

### 5. `UserShop` tự khai navigation `Shop`, không lấy từ base class

- **Thiết kế nói gì:** `03` không nói về base class.
- **Thực thi:** `ShopAuditableEntity` (`Vsite.Domain.Common`) chỉ có `ShopId` kiểu `Guid`, không có
  navigation. `UserShop.Shop` khai riêng trong entity.
- **Nguyên nhân:** namespace dùng chung không được biết entity của module nào (Quyết định #1).

### 6. Login bằng email/password tại domain shop chưa có `UserShop` → 401, không tự tạo membership

- **Thiết kế nói gì:** `03` §3.3 — *"Mọi lần authenticate thành công trong context của một shop →
  upsert `UserShop`"*, bảng liệt kê `LoggedInOnShop` cho tình huống "đã có tài khoản vsite, nay đăng
  nhập tại `spa-abc.com`".
- **Thực thi:** trả 401. `LoginHandler` chỉ cập nhật `LastActiveAt` cho membership **đã tồn tại**.
- **Nguyên nhân:** với email/password, credential của shop nằm ở `UserShop.PasswordHash` — chưa có
  `UserShop` thì **không có gì để so khớp**, nên không thể "đăng nhập thành công" rồi mới upsert.
  Câu §3.3 chỉ áp dụng được cho Social Login (identity đến từ provider, không cần credential local)
  — mà Social Login chưa làm. Người dùng phải `/auth/register` trên chính domain shop đó trước.
- **⚠️ Cần sửa ở `03`:** câu "mọi lần authenticate → upsert" hiện đọc như luật chung; nó chỉ đúng
  cho nhánh social. Nên ghi rõ điều kiện thay vì để agent sau hiện thực nhầm.

---

## Việc chưa làm xong so với đặc tả (nợ kỹ thuật, không phải lệch có chủ đích)

### A. Ba trong bốn giá trị `UserShopSource` chưa bao giờ được ghi

`03` §3.3 định nghĩa 4 nguồn. Code chỉ tạo `UserShop` ở đúng **một** chỗ
(`VerifyEmailHandler.cs:67`), luôn với `Source = RegisteredOnShop`.

| `Source` | Tình huống theo `03` §3.3 | Trạng thái |
|---|---|---|
| `RegisteredOnShop` | Đăng ký tài khoản mới tại domain shop | ✅ |
| `LoggedInOnShop` | Đã có tài khoản vsite, nay đăng nhập tại domain shop | ❌ — xem lệch #6, chờ Social Login |
| `InvitedByShop` | Owner mời làm staff | ❌ — Phase 4 ("Phân quyền nhân viên", #39.1) |
| `ShopCreator` | Tạo shop mới | ❌ — **chưa có endpoint tạo Shop** |

`ShopCreator` là chỗ đáng chú ý nhất: chưa có đường nào tạo `Shop` qua API (mọi integration test
trong `backend/tests/IntegrationTests/Identity/` phải insert row `Shop` bằng tay qua `AppDbContext`
để test). Task `SHOP-001` đang có `plan.md` cho việc này.

### B. Không có Hangfire — ba job `03` yêu cầu đều chưa tồn tại

Không có gói Hangfire nào trong `backend/src`. Ba việc phụ thuộc nó:

| Nguồn | Job | Hệ quả khi thiếu |
|---|---|---|
| `03` §5 | Dọn `PendingRegistration` hết hạn (gộp chung job dọn token, Quyết định #3) | Bảng phình dần; `IX_PendingRegistration_ExpiresAt` đã tạo sẵn để phục vụ job này |
| `03` §4 | Integrity job quét `User` có `PrimaryIdentityKind = 'Zalo'` mà thiếu `ExternalLogin` | Lớp phòng vệ thứ 3 của nhánh Zalo chưa có — hiện chỉ còn aggregate invariant |
| `03` §5 | Dọn `RefreshToken` / `PasswordResetToken` hết hạn | Như trên |

Chưa cấp bách vì Zalo Login chưa bật và lưu lượng bằng 0, nhưng phải làm trước launch.

### C. Verify-email từ domain shop chưa có bước handoff code

- `03` §5 bullet cuối: link verify luôn trỏ `api.vsite.vn/auth/verify-email`; verify xong phải
  **redirect lần hai về domain shop kèm handoff code**, không kèm token (khuôn Quyết định #6).
- Không có `handoff` ở bất kỳ đâu trong `backend/src`. Chưa chặn được gì vì custom domain cũng chưa
  resolve, nhưng phải làm cùng lúc với Social Login / module `Shop`.

### D. `ShopCustomerDto` chưa tồn tại

`03` §3.3 đặt ràng buộc bắt buộc: *"Portal không bao giờ serialize thẳng entity `User` ra response"*,
phải có DTO riêng cho góc nhìn shop. Chưa có endpoint nào cho chủ shop xem danh sách khách, nên chưa
vi phạm — nhưng đây là loại lỗi tài liệu tự cảnh báo là AI agent sẽ mắc. **Khi làm màn "Khách hàng"
ở Portal, đọc bảng hai cột ở `03` §3.3 trước.**

### E. (Đã xong 2026-09-14) Toàn bộ nợ test Docker đã chạy pass thật

`Docs/DOCKER-TEST-DEBT.md` nay rỗng. 8 test Phase 1 + 6 `TokenScopeTests` + 3 test
`TenantResolutionTests`/`ShopLookupCacheTests` mới (thay cho bước verify thủ công
`TenantResolutionMiddleware` — sửa hosts file + `dotnet run` + curl tay) đều chạy pass thật với
Docker (`dotnet test tests/IntegrationTests` → 17/17 pass, chạy lặp lại 3 lần để loại flaky).
`ShopLookupCacheTests` nằm ở collection + `IdentityApiFactory` riêng (không dùng chung với
`TenantResolutionTests`/`TokenScopeTests`) vì nó tắt hẳn container Postgres giữa chừng để verify
cache Redis — dùng chung factory với test khác từng làm connection pool Npgsql dính request ngay
sau lúc restart (flaky thật, không phải lý thuyết — bắt được lúc viết test này).

**Hai bug phát hiện khi chạy lần đầu với Docker thật (đã sửa, không phải lệch thiết kế):**

1. **Bug thật ở production code — `AuthenticationSetup.cs`:** thiếu `bearerOptions.MapInboundClaims
   = false`. Mặc định `JwtSecurityTokenHandler` tự đổi tên claim `sub` → `ClaimTypes.NameIdentifier`
   khi validate token, khiến mọi `context.User.FindFirst("sub")` (ở `CurrentUserContext` và
   `ShopMembershipValidationMiddleware`) luôn null — request có token hợp lệ vẫn 500 hoặc bị từ chối
   nhầm với `SHOP_MEMBERSHIP_REVOKED`. Ảnh hưởng **mọi** endpoint yêu cầu authentication, không chỉ
   test. Đã sửa bằng một dòng; hành vi JWT không đổi (vẫn đọc đúng `sub`/`aud` như token phát ra).
2. **Bug ở test fixture — `AppApiFactory.cs` (`IdentityApiFactory`):** trước đây nhận `PostgresFixture`
   qua constructor injection giữa hai `ICollectionFixture` trong cùng một collection. xUnit không đảm
   bảo thứ tự khởi tạo giữa các `ICollectionFixture` (`Type.GetInterfaces()` không có thứ tự xác
   định) — fail thật với `"unresolved constructor arguments: PostgresFixture postgres"` dù container
   Postgres đã start và ready đúng. Sửa bằng cách cho `IdentityApiFactory` tự dựng Postgres container
   riêng (giống cách nó đã tự dựng Redis), bỏ phụ thuộc chéo giữa hai fixture.

### F. Social Login chưa làm (Quyết định #6)

Bảng `ExternalLogin` đã có đủ schema + unique `(Provider, ProviderUserId)`, nhưng không có luồng
OAuth/callback/handoff nào. `03` §6.2 và §6.3 (bổ sung email cho user Zalo-only) chưa có code.
Chặn bởi: cần `ShopDomain` (module `Shop` đầy đủ) để tra callback URL theo slug.

---

## Đúng đặc tả, đã xác nhận bằng code (ghi lại để không phải kiểm lại)

- **7 role cố định** (`RoleSeed.cs`) khớp đúng bảng `03` §3.5 — 2 Platform + 5 Shop, GUID cố định
  qua `WellKnownRoles`, seed bằng `HasData` trong migration chứ không phải endpoint.
- **Cả 6 ràng buộc DB của `03` §4 đều tồn tại thật ở migration** — xem bảng ở lệch #4.
- **`User` không có cột `GoogleId`/`FacebookId`/`ZaloId`** và không có `ShopId` — đúng §3.1.
- **`UserShop` không có `Username`/`UsernameNormalized`** — đúng ghi chú "đã bỏ" ở §3.3.
- **`UserShop` là entity tenant-scoped duy nhất** hiện tại; Global Query Filter fail-closed khi
  `ITenantContext.ShopId == null` (`TenantQueryFilterExtensions`).
- **Không có cột `IsOwner`**; ownership qua `UserShop.RoleId → Role.Code = 'Owner'` — đúng §3.5.
- **`PendingRegistration` dùng bảng staging riêng**, không dùng `User.Status = PendingVerification`
  — đúng §5 và Quyết định #30.
- **`LastActiveAt` chỉ ghi lúc login thành công** (`LoginHandler.cs:87`), không ghi mỗi request —
  thoả cảnh báo ở §3.3.

---

## Cần cập nhật ở tài liệu gốc

Ba chỗ trong `DesignIdeal/03-identity-entity-design.md` nên sửa để agent sau không hiện thực nhầm:

1. **§3.1 / §3.3 / §5** — bỏ cột `PasswordSalt` khỏi ba sơ đồ, ghi chú dùng `PasswordHasher`.
2. **§3.3** — câu *"Mọi lần authenticate thành công trong context của một shop → upsert `UserShop`"*
   phải ghi rõ chỉ áp dụng cho **Social Login**; nhánh email/password yêu cầu đăng ký trước.
3. **§3** — bổ sung `RefreshToken` và `PasswordResetToken` vào danh sách entity, hoặc ghi rõ chúng
   thuộc phạm vi triển khai Quyết định #3 và `03` §6.4.

Chưa sửa trong task này vì sửa tài liệu thiết kế là hành động cần người duyệt, không phải hệ quả
tự động của việc viết changelog.
