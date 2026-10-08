# REFACTOR-AUTHZ-001 — changelog

Task 2 của đợt review kiến trúc 2026-10-08: các handler Identity đọc membership bằng
`UserShops.IgnoreQueryFilters()` mà không lọc `!IsDeleted`/`Status == Active`. Trả nợ MEDIA-001
"Chưa làm xong" #15 và REFACTOR-BE-001 "Chưa làm xong" #1, #2. Nhánh tách từ
`feature/REFACTOR-DB-001`.

- **Lane:** A. Contract không đổi (`contract-diff.md`: 22 operation UNCHANGED). Hành vi đổi theo
  **Quyết định #90**: người duyệt chốt 4 câu hỏi trước khi code, định nghĩa ở
  `backend/docs/modules/identity.md`.
- **Banner:** `DesignIdeal/03` thêm mục stale cho §6.1.

---

## Đã làm

### 1. Membership hết hiệu lực không còn dùng được ở luồng auth (bug bảo mật)

Trước task này, membership đã xoá mềm hoặc `Suspended`/`Invited` vẫn làm được những việc sau.

| Handler | Lỗi trước đây | Sau |
|---|---|---|
| `LoginHandler` (audience shop) | Membership đã xoá/đình chỉ vẫn đăng nhập được, có token | 401 chung |
| `LoginHandler`, `RefreshTokenHandler` (`ownerShopIds`) | Gồm cả shop đã bị thu hồi quyền Owner | Chỉ Owner còn hiệu lực |
| `RefreshTokenHandler` | Vẫn cấp access token mới, chỉ middleware chặn request sau | 401 + thu hồi mọi token cùng (user, audience, shop) |
| `ChangePasswordHandler` | Đổi được mật khẩu của membership đã xoá (nếu đi vòng qua middleware) | 401 |
| `ForgotPasswordHandler` | Gửi mail reset cho membership đã xoá/đình chỉ | Âm thầm không gửi |
| `ResetPasswordHandler` | Token phát trước khi bị đình chỉ vẫn đổi được mật khẩu | `400 RESET_TOKEN_INVALID` |
| `RegisterHandler` + `VerifyEmailHandler` | Đăng ký lại khi membership đã xoá mềm: `INSERT` trùng unique `(user_id, shop_id)` → **500** | Khôi phục dòng cũ (Customer, Active, mật khẩu mới, `Source` giữ nguyên) |
| `RegisterHandler` | — | Suspended/Invited vẫn chặn 409 (giữ hành vi cũ, nay có test) |

### 2. Một helper duy nhất + test allowlist (sửa gốc rễ)

- **`Vsite.Application.Identity.UserShopQueries`:** ba extension trên `DbSet<UserShop>`:
  - `ActiveAcrossShops()`: chưa xoá mềm + `Active`, dùng cho mọi quyết định cấp quyền.
  - `AcrossShops()`: chưa xoá mềm, mọi trạng thái (Register cần tính cả Suspended).
  - `IncludingDeletedAcrossShops()`: chỉ VerifyEmail dùng, để khôi phục dòng cũ.
- **Giữ filter tenant cho `UserShop`.** Query theo shop sau này (màn "Khách hàng") vẫn fail-closed.
- **Đã chuyển sang helper:** 7 handler Identity, `ListShopsHandler` (module Shop) và
  `UserShopMembershipService`.
- **`IgnoreQueryFiltersAllowlistTests` (ArchitectureTests):**
  - Quét `backend/src`: mọi `.IgnoreQueryFilters(` chỉ được nằm trong 6 file của allowlist, mỗi
    file có lý do.
  - Báo lỗi luôn nếu allowlist có file không còn dùng.
  - Đã kiểm bằng cách thêm tạm một lời gọi ngoài allowlist: test đỏ đúng chỗ.

### 3. `UserShop` và `User` có hành vi

- **`UserShop`:**
  - `RoleId`, `PasswordHash`, `Status`, `LastActiveAt` chỉ gán lúc khởi tạo (`init`, field backing
    mà EF map theo convention).
  - Đổi trạng thái qua `SetPassword`, `MarkActive`, `RestoreAsCustomer`.
- **`User`:**
  - `PasswordHash`, `LastLoginAt` đổi qua `SetPassword`, `RecordLogin`.
  - Thêm `CanSignIn` (= `Status == Active`).
  - Các field hồ sơ vẫn setter public, vì chưa có luồng sửa hồ sơ nào để đặt invariant.
- Không đổi schema, không có migration.

### 4. Phát hiện thêm khi làm: `User.Status` không được kiểm ở đâu cả

- `UserStatus` có `Suspended`/`Deleted`, nhưng Login/Refresh không kiểm, nên tài khoản bị đình
  chỉ **toàn cục** vẫn đăng nhập được.
- Đã sửa trong task này vì cùng bản chất: Login → 401 chung, Refresh → 401 + thu hồi,
  ForgotPassword → không gửi mail.
- Đã gộp vào Quyết định #90.

### 5. Vòng sửa sau review độc lập

- **Khôi phục membership thu hồi refresh token cũ của scope shop đó.** Xoá mềm không thu hồi token,
  nên trước khi sửa, một refresh token cũ chưa dùng tới sẽ sống lại sau khi khách đăng ký lại.
- **ResetPassword chặn khi `User` không `Active`.** Token phát trước khi bị đình chỉ không đổi được
  mật khẩu.
- **`UserShopMembershipService` kiểm thêm `User.Status`.** Middleware và endpoint filter chạy mỗi
  request, nên token shop và mọi route `{shopId}` của tài khoản bị đình chỉ bị chặn ngay (403),
  không đợi access token hết hạn. Gần như không tốn thêm vì query này vốn đã chạy.
- **ChangePassword chặn `User` không `Active`.**
- **Login:** mọi nhánh thất bại tốn đúng một lượt PBKDF2 (hash một lượt khi không có hash để so).
  Trước đây "không có user/membership" trả lời nhanh hơn "sai mật khẩu" một cách đo được.
- **`IgnoreQueryFiltersAllowlistTests` siết lại:**
  - Bỏ comment trên toàn file rồi mới so khớp, nên bắt được lời gọi xuống dòng và lời gọi trần.
  - Khoá **số lời gọi** cho mỗi file. Đã kiểm: thêm một lời gọi thứ hai, tách dòng, vào một file đã
    được phép → test đỏ.
  - Loại `bin/obj/Migrations` theo đường dẫn tương đối.
- **Test thêm:**
  - Restore từ Owner+Suspended về Customer+Active, và refresh token cũ phải 401.
  - Đăng ký lại khi Invited → 409.
  - Reset sau khi membership bị xoá mềm.
  - Reset sau khi `User` bị đình chỉ.
  - Token shop của `User` bị đình chỉ bị chặn ở request kế tiếp.
- **Kiểm đột biến:** gỡ từng bản sửa I1/I2/I3 thì đúng test tương ứng đỏ.

### 6. Sửa test helper có sẵn: `TestEmailSpy`

- `ConcurrentBag` không giữ thứ tự, nên `ExtractLastTokenFor` có thể trả token của mail **cũ** khi
  một địa chỉ nhận nhiều mail (verify rồi reset).
- Đổi sang `ConcurrentQueue`.
- Bug có từ IDENTITY-001, chỉ lộ ra khi test mới gửi hai mail cho cùng một địa chỉ.

## Kiểm chứng (Docker thật, 2026-10-09)

- **`dotnet test backend/vsite.sln` (sau vòng sửa review):** xem số liệu cuối ở mục "Chạy lần cuối"
  bên dưới.
- **`MembershipLifecycleTests`:** 12 test end-to-end qua HTTP thật.
- **Kiểm đột biến:** cho `UserShopQueries` và `User.CanSignIn` quay về hành vi cũ (không lọc) thì
  **7/8 test đỏ**. Test còn lại (đăng ký lại khi Suspended → 409) vốn đã đúng, giữ làm test chống
  thoái lui.

### Chạy lần cuối (Docker thật, 2026-10-09, sau vòng sửa review)

`dotnet test backend/vsite.sln`:
- IntegrationTests **308 pass / 0 fail / 1 skip** (skip có sẵn của SHOP-001)
- ArchitectureTests 9/9
- ComponentSchemaTests 12/12

`has-pending-model-changes`: không đổi model, không cần migration. Contract: 22 operation UNCHANGED.

## Chưa làm / để task khác

1. **`ownerShopIds` chưa loại shop đã xoá mềm.** Module Identity không được tham chiếu `Shop`
   (`Identity.dependsOn = []`). Claim này chỉ dùng render UI shop switcher (#27, không dùng để
   authorize). Khi có luồng xoá shop, nên xoá mềm luôn membership của shop đó.
2. **Chưa có nghiệp vụ đình chỉ/khôi phục membership hay User.** `Status` hiện chỉ đặt được bằng SQL.
   Khi làm màn quản lý thành viên thì thêm method trên entity (`Suspend`, `Reactivate`) và thu hồi
   refresh token cùng lúc.
3. **Access token đã phát vẫn dùng được tới khi hết hạn (15 phút) sau khi `User` bị đình chỉ**, với
   token `vsite-main`, và token `vsite-portal` trên endpoint không có `{shopId}` (`GET /shops`,
   `/auth/me`). Token shop và mọi route `{shopId}` đã bị chặn ngay (mục 5). Chặn nốt thì cần kiểm
   `User.Status` mỗi request (thêm một query), cần người quyết.
4. **Các field bảo mật của `User` vẫn setter public:** `Status`, `RoleId`, và `IsDeleted` của base
   class. Code có thể khôi phục một dòng mà không qua `RestoreAsCustomer`. Nên làm cùng nghiệp vụ
   đình chỉ (mục 2).
5. **VerifyEmail không kiểm `User.CanSignIn`.** User bị đình chỉ vẫn tạo/khôi phục được membership,
   vô hại vì không đăng nhập được. Lúc khôi phục cũng không reset `LastActiveAt`.

## Giả định tôi đã tự đặt

- Mục 4 (`User.Status`) không hỏi riêng: cùng loại lỗi với câu hỏi đã chốt, và hành vi là hệ quả
  hiển nhiên của trạng thái `Suspended`. Đã gộp vào #90 để người duyệt thấy.
- `RestoreAsCustomer` đặt role **Customer**, kể cả khi membership cũ là Owner/Staff. Đăng ký lại là
  luồng của khách, không được tự khôi phục quyền quản trị.
