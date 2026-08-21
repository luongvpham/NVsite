# vsite — Thiết Kế Entity: Identity & Shop Membership

> Tài liệu này chốt mô hình `User` / `ExternalLogin` / `UserShop` / `Shop` / `Role`.
> **Supersede** Quyết định #28 trong `02-tech-stack-and-decision.md` (mô hình `UserShopCredential` + merge theo email/SĐT chưa verify).
> Mọi thay đổi so với tài liệu này phải được ghi nhận lại tại đây.

---

## 1. Nguyên tắc nền

Bảy nguyên tắc dưới đây chi phối toàn bộ phần còn lại. Nếu một thiết kế tương lai mâu thuẫn với chúng, thiết kế đó sai chứ không phải nguyên tắc sai.

| # | Nguyên tắc | Lý do |
|---|---|---|
| 1 | `User.Id` là identity toàn cục duy nhất của một người thật | Giữ đúng tinh thần Quyết định #5 — đổi tên/avatar một lần áp dụng mọi nơi, Analytics thấy được hành vi xuyên shop |
| 2 | **Credential có ở cả `User` và `UserShop`, nhưng token sinh ra từ chúng có năng lực khác nhau** | User cần password riêng theo shop (§3.3). An toàn không đến từ việc gom credential về một chỗ, mà từ việc **giới hạn token shop không chạm được vào credential global** (§7) |
| 3 | Email là **identity key**, UNIQUE toàn cục, và **luôn ở trạng thái đã verify** | Nếu tồn tại email chưa verify trong bảng `User`, kẻ tấn công squat được email người khác và chiếm luôn tài khoản khi nạn nhân login social |
| 4 | Mỗi `User` bắt buộc có **email đã verify** hoặc **`ExternalLogin` với Zalo** | Phải có ít nhất một kênh xác thực được sở hữu; nếu không thì không recover được tài khoản và không phân biệt được người thật |
| 5 | Email từ external provider chỉ dùng để auto-link khi **provider khẳng định đã verified** | Có email ≠ email đã verify. Google Workspace có thể trả `email_verified: false` |
| 6 | `UserShop` = membership + role + **credential riêng của shop đó**. Hồ sơ (tên, avatar, SĐT) **dùng chung** ở `User` | Password riêng giữ được ảo giác tách biệt và tránh làm hỏng autofill của trình duyệt; hồ sơ dùng chung tránh bắt user nhập lại N lần |
| 7 | Phone và CCCD là **thông tin hồ sơ**, không phải identity key, kể cả sau khi verify | Không unique, và verify chúng không tự động biến chúng thành khóa định danh |

---

## 2. Sơ đồ quan hệ

```
                        Role
                    (Scope: Platform | Shop)
                     ▲              ▲
                     │              │
        RoleId ──────┘              └────── RoleId
           │                                   │
         User ──1────N── ExternalLogin         │
           │                                   │
           1                                   │
           │                                   │
           N                                   │
        UserShop ──────────────────────────────┘
           │
           N
           │
           1
         Shop ──1────N── ShopDomain   (xem Quyết định #7)
```

```
User      1 ── N  ExternalLogin      (Google / Facebook / Zalo)
User      1 ── N  UserShop           membership
Shop      1 ── N  UserShop
User      N ── 1  Role               Scope = Platform
UserShop  N ── 1  Role               Scope = Shop
```

---

## 3. Entity

### 3.1 `User`

Tài khoản toàn cục của một người. **Không** chứa `ShopId`. **Không** chứa cột `GoogleId`/`FacebookId`/`ZaloId`.

```
User
─────────────────────────────────────────────
Id                    UUID          PK

FullName              string?
AvatarUrl             string?

Email                 string?
EmailNormalized       string?       UNIQUE (partial, WHERE NOT NULL)
EmailVerifiedAt       timestamp?

PrimaryIdentityKind   enum          NOT NULL  { Email, Zalo }

Phone                 string?
PhoneVerifiedAt       timestamp?
CCCD                  string?
CCCDVerifiedAt        timestamp?

PasswordSalt          string?
PasswordHash          string?

RoleId                UUID          NOT NULL  → Role (Scope = Platform)
RoleScope             enum          GENERATED ALWAYS AS ('Platform')

Status                enum          { Active, Suspended, Deleted }

CreatedAt             timestamp
UpdatedAt             timestamp
LastLoginAt           timestamp?
```

**Ghi chú thiết kế:**

- `Email` nullable vì user **Zalo-only** là hợp lệ — Zalo Login không trả email. Đây là trường hợp bình thường của thị trường Việt Nam, không phải ngoại lệ cần workaround.
- `PasswordSalt`/`PasswordHash` ở đây là credential của **`vsite.vn`**, tách biệt hoàn toàn với credential từng shop ở `UserShop`. Nullable vì user đăng ký bằng social, hoặc đăng ký tại domain shop và chưa từng đặt password cho `vsite.vn`.
- `EmailVerifiedAt` là **audit trail** (verify lúc nào), **không phải capability**. Không bao giờ dùng nó làm điều kiện để bỏ qua một bước xác thực.
- `PrimaryIdentityKind` tồn tại để DB tự chặn được nhánh Email (xem §4). Nhánh Zalo không CHECK xuyên bảng được nên phải đỡ bằng aggregate + integrity job.
- Đổi tên `DateVerifiedPhone`/`DateVerifiedCCCD` → `PhoneVerifiedAt`/`CCCDVerifiedAt` cho nhất quán với `EmailVerifiedAt`.

### 3.2 `ExternalLogin`

Ánh xạ identity của nhà cung cấp bên ngoài về một `User`. Là **child entity trong aggregate `User`**, không phải aggregate riêng.

```
ExternalLogin
─────────────────────────────────────────────
Id                    UUID          PK
UserId                UUID          NOT NULL → User

Provider              string        NOT NULL  { Google, Facebook, Zalo }
ProviderUserId        string        NOT NULL

CreatedAt             timestamp
LastLoginAt           timestamp?

UNIQUE (Provider, ProviderUserId)
```

- Identity ngoài = `Provider + ProviderUserId`, **không bao giờ là email**.
- Thêm provider mới về sau (Apple, TikTok...) **không** cần đụng bảng `User`.
- ⚠️ Zalo user ID là **app-scoped**. Nếu vsite sau này có Zalo app thứ hai, ID giữa hai app không khớp nhau — phải xử lý như hai provider riêng biệt hoặc dùng chung một app duy nhất.

### 3.3 `UserShop`

Membership thuần: user này thuộc shop nào, với vai trò gì.

```
UserShop
─────────────────────────────────────────────
Id                    UUID          PK

UserId                UUID          NOT NULL → User
ShopId                UUID          NOT NULL → Shop
RoleId                UUID          NOT NULL → Role (Scope = Shop)
RoleScope             enum          GENERATED ALWAYS AS ('Shop')

PasswordSalt          string?
PasswordHash          string?

Source                enum          NOT NULL
                      { RegisteredOnShop, LoggedInOnShop, InvitedByShop, ShopCreator }

Status                enum          { Active, Invited, Suspended }

CreatedAt             timestamp     -- lần đầu shop "có" user này
UpdatedAt             timestamp
LastActiveAt          timestamp?

UNIQUE (UserId, ShopId)
```

**Đã bỏ so với thiết kế ban đầu:** `Username`, `UsernameNormalized` — định danh đăng nhập ở shop là **email** (lấy từ `User.Email`), không dùng username riêng. Người dùng chỉ cần nhớ *"mật khẩu nào cho tiệm nào"*, không phải nhớ thêm cả tên đăng nhập.

**Vì sao giữ password riêng theo shop:**

1. **Ảo giác tách biệt là có chủ đích.** Khách vào `spa-abc.com` coi đó là "website của tiệm spa", không cần biết vsite tồn tại (đúng tinh thần Quyết định #4). Bắt họ dùng chung một mật khẩu với "một hệ thống nào đó" là phá vỡ trải nghiệm đó.
2. **Đổi mật khẩu không được lan sang chỗ khác.** Nếu dùng chung, user đổi mật khẩu ở `spa-abc.com` thì lần sau vào `salon-xyz.com` không đăng nhập được — họ không hiểu vì sao, vì họ có đổi gì ở đây đâu.
3. **Autofill của trình duyệt hoạt động đúng.** Chrome lưu credential theo origin. Khi đổi mật khẩu ở một origin, entry của origin kia vẫn giữ giá trị cũ → autofill điền sai → login fail. Password riêng theo shop khớp đúng với mô hình lưu trữ của trình duyệt.

**Vì sao điều này KHÔNG dựng lại lỗ hổng của Quyết định #28:**

Lỗ hổng cũ không nằm ở *chỗ chứa credential*, mà ở chỗ **credential mới được gắn vào `User.Id` đã tồn tại mà không cần chứng minh sở hữu**. Hai lớp chặn hiện tại:

- **Quyết định #30** — đăng ký bằng email/password **luôn** phải verify email, kể cả lần đăng ký thứ hai, thứ ba tại shop khác. Kẻ tấn công không nhận được mail → không có `UserShop` nào được tạo.
- **§7** — token `shop:{shopId}` **không** đọc/sửa được credential global. Password shop yếu nhất không kéo theo mất tài khoản.

Thiếu **một** trong hai lớp này là quay lại đúng lỗ hổng cũ.

**Hồ sơ dùng chung, không scoped theo shop.** `FullName`/`AvatarUrl`/`Phone` nằm ở `User`, sửa ở shop nào cũng áp dụng mọi nơi. Đây là lựa chọn có ý thức: bắt user nhập lại thông tin ở từng shop gây phiền hơn giá trị tách biệt mang lại, và với booking thì SĐT mới thường mới là số đúng — propagate là hành vi mong muốn. **Ghi lại để sau này không ai coi là bug.**

**Thời điểm tạo record — trigger là xác thực, không phải giao dịch:**

> **Mọi lần authenticate thành công trong context của một shop → upsert `UserShop` (role `Customer`).**
> Duyệt web ẩn danh → không tạo gì.

Theo Quyết định #31, muốn có role ở shop thì phải có `UserShop`; mà muốn booking/review thì phải đăng nhập trước. Nên trigger thật sự nằm ở bước **xác thực**, booking/review chỉ là hệ quả xảy ra sau đó.

| Tình huống | `UserShop` | `Source` |
|---|---|---|
| Đăng ký tài khoản mới tại `spa-abc.com` | ✅ tạo | `RegisteredOnShop` |
| Đã có tài khoản vsite, nay đăng nhập tại `spa-abc.com` | ✅ tạo | `LoggedInOnShop` |
| Owner mời làm staff | ✅ tạo, `Status = Invited` | `InvitedByShop` |
| Tạo shop mới | ✅ tạo, role `Owner` | `ShopCreator` |
| Xem website shop mà không đăng nhập | ❌ | — |
| Tìm kiếm trên marketplace, đăng nhập ở `vsite.vn` | ❌ | — |

Ranh giới ẩn danh/đã xác thực mới là chỗ cần chặn — nếu tạo record cho mọi lượt ghé thăm, bảng phình vô ích và thống kê bị nhiễu bởi membership rỗng.

**Phục vụ thống kê cho chủ shop:**

`Source` phân biệt được **khách tự đăng ký tại shop** (giá trị marketing cao nhất — họ đến vì shop) với **khách marketplace ghé qua** (đến vì nền tảng). `CreatedAt` + `LastActiveAt` cho phép đếm khách hoạt động theo kỳ.

Truy vấn phải **lọc theo role**, nếu không Owner/Staff bị đếm chung vào số khách hàng:

```sql
SELECT COUNT(*) FROM "UserShop" us
JOIN "Role" r ON r.Id = us.RoleId
WHERE us.ShopId = @shopId
  AND r.Code = 'Customer'
  AND us.Status = 'Active';
```

⚠️ **`LastActiveAt` không được ghi ở mỗi request** — đó là write thừa trên đường nóng. Chỉ update khi giá trị cũ đã quá ngưỡng (đề xuất > 1 giờ), hoặc gom qua Hangfire job từ event stream. Thống kê "hoạt động 30 ngày" không cần độ chính xác hơn mức đó.

**⚠️ Ranh giới dữ liệu — shop thấy được gì về user:**

Vì `UserShop` được tạo ngay khi user xác thực tại domain shop, shop lập tức "có" user đó trong danh sách. Phải định rõ góc nhìn của **chủ shop** (khác với góc nhìn của chính user — xem §7).

| Chủ shop **được** thấy | Chủ shop **không** được thấy |
|---|---|
| `FullName`, `AvatarUrl` | Danh sách shop khác mà user thuộc về |
| Email / SĐT — **chỉ khi** user đã có booking hoặc liên hệ với shop | Lịch sử booking / review ở shop khác |
| Booking, review của user **tại shop đó** | `PasswordHash` của bất kỳ shop nào (kể cả shop mình) |
| `CreatedAt`, `LastActiveAt`, `Source` của chính `UserShop` đó | `PrimaryIdentityKind`, `ExternalLogin`, `CCCD`, trạng thái verify |

**Ràng buộc bắt buộc:** Portal **không bao giờ** serialize thẳng entity `User` ra response. Phải có DTO riêng cho góc nhìn shop (ví dụ `ShopCustomerDto`), chỉ chứa đúng các trường ở cột trái. Đây là loại lỗi AI agent sẽ vi phạm nếu không nói trước — mapping "tiện tay" từ `User` là đường ngắn nhất, và nó rò dữ liệu.

---

### 3.4 `Shop`

```
Shop
─────────────────────────────────────────────
Id                    UUID          PK
Name                  string        NOT NULL
Slug                  string        NOT NULL  UNIQUE
Status                enum          { Draft, Active, Suspended, Closed }
CreatedAt             timestamp
UpdatedAt             timestamp
```

- Hồ sơ chi tiết (địa chỉ, toạ độ, giờ mở cửa, liên hệ, ảnh) tách sang entity riêng khi thiết kế module Shop đầy đủ.
- Thông tin domain nằm ở `ShopDomain` (Quyết định #7), không nhét vào `Shop`.
- `Slug` phải validate với `config/reserved-routes.json` (Quyết định #8 + #24).

### 3.5 `Role`

```
Role
─────────────────────────────────────────────
Id                    UUID          PK
Code                  string        NOT NULL
Name                  string        NOT NULL
Scope                 enum          NOT NULL  { Platform, Shop }
IsSystem              bool          NOT NULL
CreatedAt             timestamp
UpdatedAt             timestamp

UNIQUE (Code, Scope)
UNIQUE (Id, Scope)          -- phục vụ composite FK, xem §4
```

| Scope = Platform | Scope = Shop |
|---|---|
| `PlatformUser` | `Owner` |
| `PlatformAdmin` | `Manager` |
| | `Staff` |
| | `Accountant` |
| | `Customer` |

- **`Owner` không bao giờ được lưu ở `User.RoleId`** — quyền sở hữu luôn gắn với một shop cụ thể.
- Không dùng cột `IsOwner`. Ownership = `UserShop.RoleId → Role.Code = 'Owner'`.

---

## 4. Ràng buộc ở tầng Database

Những ràng buộc dưới đây phải nằm ở DB, không chỉ ở code. Lý do theo đúng nguyên tắc Quyết định #17 — *codegen > skill > CLAUDE.md > hy vọng agent nhớ*: DB constraint là tầng duy nhất mà migration tay, seed data, script sửa dữ liệu và AI agent viết use-case mới đều không lách qua được.

```sql
-- [1] Email là identity key toàn cục, nhưng chỉ khi có
CREATE UNIQUE INDEX ux_user_email
  ON "User" (EmailNormalized)
  WHERE EmailNormalized IS NOT NULL;

-- [2] Có email <=> đã verify. Không tồn tại email chưa verify trong bảng User.
ALTER TABLE "User" ADD CONSTRAINT ck_user_email_verified CHECK (
  (Email IS NULL     AND EmailVerifiedAt IS NULL) OR
  (Email IS NOT NULL AND EmailVerifiedAt IS NOT NULL)
);

-- [3] Nhánh Email của nguyên tắc "phải có email hoặc Zalo"
ALTER TABLE "User" ADD CONSTRAINT ck_user_primary_identity CHECK (
  PrimaryIdentityKind <> 'Email' OR Email IS NOT NULL
);

-- [4] Role scope phải khớp chỗ dùng
ALTER TABLE "Role"     ADD CONSTRAINT uq_role_id_scope UNIQUE (Id, Scope);
ALTER TABLE "User"     ADD CONSTRAINT fk_user_role
  FOREIGN KEY (RoleId, RoleScope) REFERENCES "Role" (Id, Scope);
ALTER TABLE "UserShop" ADD CONSTRAINT fk_usershop_role
  FOREIGN KEY (RoleId, RoleScope) REFERENCES "Role" (Id, Scope);

-- [5] Một user chỉ có một membership per shop
ALTER TABLE "UserShop" ADD CONSTRAINT uq_usershop UNIQUE (UserId, ShopId);

-- [6] Identity ngoài là duy nhất
ALTER TABLE "ExternalLogin" ADD CONSTRAINT uq_external UNIQUE (Provider, ProviderUserId);
```

**Ràng buộc [4] giải thích thêm:** FK thường không ngăn được `User.RoleId` trỏ vào một role có `Scope = Shop` (ví dụ gán nhầm `Owner` làm platform role). Cột generated `RoleScope` cố định giá trị + composite FK khiến việc gán sai **không compile được ở tầng DB**. Rẻ, và loại hẳn một lớp bug quyền.

**Nhánh Zalo không CHECK xuyên bảng được** (`User` phải có `ExternalLogin(Zalo)`). Ba lớp thay thế:

1. **Aggregate invariant** — `ExternalLogin` là child entity của aggregate `User`; tạo `User` Zalo-only và `ExternalLogin` trong **cùng một transaction, cùng một aggregate root**. Domain code không cho phép tạo `User` thiếu identity.
2. **`PrimaryIdentityKind`** — cột chống đỡ ở DB cho nhánh Email.
3. **Hangfire integrity job** — quét định kỳ `User` có `PrimaryIdentityKind = 'Zalo'` mà không có `ExternalLogin` tương ứng → alert. Bắt được bug do migration/script tay.

---

## 5. Bảng `PendingRegistration`

`User` **chỉ được tạo sau khi email đã verify xong**. Trong lúc chờ, dữ liệu đăng ký nằm ở bảng staging riêng.

```
PendingRegistration
─────────────────────────────────────────────
Id                    UUID          PK
Email                 string        NOT NULL
EmailNormalized       string        NOT NULL
PasswordSalt          string        NOT NULL
PasswordHash          string        NOT NULL
FullName              string?
ShopId                UUID?         -- context đăng ký (null = vsite.vn)
TokenHash             string        NOT NULL  -- HASH của token, không lưu raw
ExpiresAt             timestamp     NOT NULL
ConsumedAt            timestamp?
CreatedAt             timestamp
```

**Vì sao không dùng `User.Status = PendingVerification`:**

- Vi phạm ràng buộc [2] ở §4 — sẽ tồn tại `User` có email chưa verify.
- Mở đường **email squatting**: kẻ tấn công đăng ký `victim@gmail.com` không verify → chiếm luôn slot UNIQUE → nạn nhân thật không đăng ký được, và nếu hệ thống auto-link khi nạn nhân login Google thì nạn nhân bước thẳng vào tài khoản của kẻ tấn công.

**Ràng buộc:**

- Lưu `TokenHash`, không lưu token thô — DB rò rỉ không đồng nghĩa chiếm được tài khoản.
- TTL ngắn (đề xuất 15–30 phút), one-time (`ConsumedAt`).
- Hangfire dọn record hết hạn (gộp chung job dọn token đã có ở Quyết định #3).
- Nếu `EmailNormalized` đã tồn tại trong bảng `User` → **không** tạo `PendingRegistration`, trả về thông báo ở §6.1.
- Link verify **luôn** trỏ về `api.vsite.vn/auth/verify-email?token=...` (cố định). Nếu đăng ký từ custom domain, verify xong redirect lần hai về domain shop kèm **handoff code**, không kèm token — dùng lại đúng khuôn Quyết định #6, không phát sinh cơ chế mới.

---

## 6. Chính sách đăng ký & đăng nhập

### 6.1 Đăng ký bằng email + mật khẩu

Luồng giống nhau ở `vsite.vn` và ở domain shop; chỉ khác **password vừa nhập được lưu vào đâu**.

```
[1] User nhập email + password (tại vsite.vn HOẶC tại spa-abc.com)
        ↓
[2] EmailNormalized đã tồn tại trong bảng User?
        │
        ├── CÓ, và context = vsite.vn
        │     → DỪNG. "Email này đã có tài khoản. Vui lòng đăng nhập
        │              hoặc dùng Quên mật khẩu."
        │
        ├── CÓ, và context = shop, user CHƯA có UserShop ở shop này
        │     → vẫn tạo PendingRegistration + gửi magic link
        │       (user không hề biết email đã tồn tại — đúng ảo giác tách biệt)
        │
        ├── CÓ, và user ĐÃ có UserShop ở shop này
        │     → DỪNG. "Email này đã có tài khoản tại {tên shop}.
        │              Vui lòng đăng nhập hoặc dùng Quên mật khẩu."
        │
        └── KHÔNG → tạo PendingRegistration + gửi magic link
        ↓
[3] User bấm link → verify token (chưa dùng, chưa hết hạn, hash khớp)
        ↓
[4] Trong MỘT transaction:
      · Nếu chưa có User → tạo User
            Email, EmailVerifiedAt = now(), PrimaryIdentityKind = Email
            RoleId = PlatformUser
      · Nếu context = vsite.vn → ghi password vào User.PasswordHash
      · Nếu context = shop     → tạo UserShop(role Customer,
                                   Source = RegisteredOnShop)
                                   ghi password vào UserShop.PasswordHash
                                   KHÔNG đụng tới User.PasswordHash
        ↓
[5] Đánh dấu ConsumedAt.
```

**Điểm mấu chốt ở nhánh thứ hai của bước [2]:** vì luồng đăng ký **luôn** đi qua verify email, việc "email đã tồn tại" là **vô hình với người dùng** — họ chỉ thấy "xác minh email cho {tên shop}", giống hệt như đăng ký mới. Ảo giác tách biệt được giữ nguyên, đồng thời kẻ tấn công không nhận được mail nên không tạo được `UserShop` nào.

⚠️ Bước [4] **không được** ghi password vào `User.PasswordHash` khi context là shop. Nếu ghi, password yếu đặt ở một shop trở thành password đăng nhập `vsite.vn` — mở đúng đường tấn công mà §7 đang chặn.

⚠️ **Tuyệt đối không** làm biến thể "verify email xong thì ghi đè password global mà người dùng vừa nhập". Đó là password-reset ngầm: nạn nhân không nhận được mail cảnh báo, và nó dựng lại lỗ hổng của Quyết định #28.

### 6.2 Đăng nhập / đăng ký bằng external provider

```
[1] Authenticate với provider thành công
        ↓
[2] Tìm ExternalLogin (Provider, ProviderUserId)
        ├── CÓ → login vào User đã link. XONG.
        └── KHÔNG → xuống [3]
        ↓
[3] Provider có trả email VÀ khẳng định email_verified = true?
        ├── CÓ  → tìm User theo EmailNormalized
        │         ├── tìm thấy → auto-link ExternalLogin vào User đó ✓
        │         └── không thấy → tạo User mới
        │                          (Email, EmailVerifiedAt = now(),
        │                           PrimaryIdentityKind = Email)
        │                          + ExternalLogin, cùng transaction
        │
        └── KHÔNG → KHÔNG auto-link theo email trong mọi trường hợp
                  ├── Provider = Zalo → tạo User mới
                  │     Email = NULL, PrimaryIdentityKind = Zalo
                  │     + ExternalLogin(Zalo), cùng transaction
                  └── Provider khác  → tạo User mới không email,
                        hoặc mời user bổ sung + verify email sau
```

**Ràng buộc bắt buộc ở bước [3]:**

| Provider | Lưu ý |
|---|---|
| Google | **Phải đọc claim `email_verified`.** Tài khoản Google Workspace có thể trả `false`. Bỏ qua claim này làm vô hiệu toàn bộ thiết kế |
| Facebook | Tài khoản đăng ký bằng SĐT **không trả email** → phải xử lý nhánh không email, không được giả định luôn có |
| Zalo | Không trả email (chỉ `id`, `name`, `picture`; SĐT cần permission riêng). User Zalo-only là **case bình thường** |

### 6.3 Bổ sung email cho user Zalo-only

```
[1] User đã đăng nhập (session hợp lệ) → nhập email muốn thêm
        ↓
[2] Email đã thuộc User khác?
        ├── CÓ  → DỪNG. "Email này đã có tài khoản khác. Hãy đăng nhập vào
        │          tài khoản đó rồi liên kết Zalo vào."
        │          → KHÔNG merge hai User
        └── KHÔNG → gửi magic link tới email đó
        ↓
[3] Verify xong → cập nhật Email + EmailVerifiedAt trên User hiện tại
        (PrimaryIdentityKind giữ nguyên = Zalo — đây là lịch sử, không phải trạng thái)
```

**Vì sao không merge hai `User`:** merge hai record đã có `UserShop`, booking, review, shop ownership là nguồn bug và lỗ hổng quyền vô tận (ai giữ quyền Owner? review của ai? booking thuộc về ai?). Chi phí kỹ thuật cao, giá trị thấp, rủi ro lớn. Hướng đúng là **link, không merge**: giữ nguyên `User.Id` cũ, gắn thêm `ExternalLogin`.

### 6.4 Quên mật khẩu

Luồng **scoped theo context** — reset password ở `spa-abc.com` chỉ đổi `UserShop.PasswordHash` của shop đó, không đụng tới password `vsite.vn` hay shop khác.

```
[1] Nhập email → LUÔN trả về cùng một thông báo, dù email có tồn tại hay không
        ("Nếu email tồn tại, chúng tôi đã gửi hướng dẫn.")
        → tránh lộ thông tin email nào đã đăng ký
[2] Nếu tồn tại → gửi reset token (lưu hash, one-time, TTL ~30 phút)
        token PHẢI gắn scope: vsite.vn hay shopId nào
[3] Đặt lại mật khẩu ĐÚNG scope đó
        → revoke toàn bộ refresh token CÙNG scope
[4] Gửi mail thông báo "mật khẩu đã được đổi", có nêu rõ đổi ở đâu
        ("mật khẩu của bạn tại Spa ABC đã được thay đổi")
```

Bước [3] bắt buộc: nếu không revoke, kẻ tấn công đã chiếm session vẫn giữ quyền truy cập sau khi nạn nhân đổi mật khẩu.

Bước [4] nêu rõ nơi đổi để user phát hiện được bất thường — họ chỉ đổi ở một tiệm mà nhận mail báo đổi ở tiệm khác là dấu hiệu tấn công.

**Hệ quả cần chấp nhận:** user có N shop thì có tối đa N+1 mật khẩu và có thể quên mật khẩu nào ứng với tiệm nào. Đây là cái giá của việc tách biệt, đã cân nhắc và chấp nhận. Giảm nhẹ bằng cách để nút "Quên mật khẩu" nổi bật ở form đăng nhập của shop.

User **Zalo-only không có mật khẩu** ở bất kỳ scope nào → recovery của họ là đăng nhập lại bằng Zalo. Mất tài khoản Zalo = mất tài khoản vsite, nên §6.3 (bổ sung email) đáng được gợi ý chủ động trong UI.

---

## 7. Phân giải Role theo domain

`User` của shop và `User` của vsite **dùng chung một bảng**. Vai trò được phân giải theo domain đang truy cập tại thời điểm request.

| Domain | `ShopId` resolve từ | Token audience | Role dùng để authorize |
|---|---|---|---|
| `vsite.vn` | — (platform, không có tenant) | `vsite-main` | `User.RoleId` (Platform) |
| `spa-abc.vsite.vn`<br>`spa-abc.com`<br>`vsite.vn/spa-abc` | Host / path — Quyết định #7 | `shop:{shopId}` | `UserShop(userId, shopId).RoleId` |
| `admin.vsite.vn` | **Route param** `/shops/{shopId}/...` | `vsite-portal` | `UserShop(userId, shopId).RoleId` |

**Vì sao Portal phải resolve `ShopId` từ route:** Portal là platform domain phục vụ nhiều shop qua shop switcher — `TenantContext` **không** resolve được từ host. Đây là trường hợp duy nhất `ShopId` không đến từ host, nên phải ghi rõ để không ai suy diễn nhầm là "lấy từ body cũng được".

**Bốn ràng buộc bắt buộc:**

1. `ShopId` lấy từ **route hoặc host**, **không bao giờ** từ request body — giữ nguyên invariant Quyết định #21.4, kể cả trên Portal.
2. Authorization Handler query `UserShop(userId, shopId)` **tại mỗi request**. `ownerShopIds` trong JWT (Quyết định #27) **chỉ để render UI shop switcher**, tuyệt đối không dùng làm căn cứ cho phép — token cũ vẫn hiệu lực sau khi revoke quyền cho tới khi hết TTL (invariant #21.5).
3. Không có `UserShop` record → **không có role ở shop đó**. Không fallback về một role mặc định nào.
4. `User.RoleId = PlatformAdmin` **không được** bypass Global Query Filter ngầm (invariant #21.2). Muốn admin xem dữ liệu shop → **endpoint riêng, audit log riêng**. Tắt filter có điều kiện ngay trong endpoint dùng chung là lỗ hổng chờ sẵn — một lần quên điều kiện là rò dữ liệu toàn bộ tenant.

### 7.1 ⚠️ Ranh giới năng lực của token `shop:{shopId}`

Đây là **invariant quan trọng nhất** của thiết kế identity. Nó là lớp chặn thứ hai (bên cạnh Quyết định #30) khiến password riêng theo shop không dựng lại lỗ hổng của Quyết định #28.

**Vì sao cần:** token `shop:{shopId}` sinh ra từ password của **riêng shop đó** — thứ mà user được phép đặt khác nhau và có thể yếu hơn ở tiệm họ ít quan tâm. Nếu token đó chạm được vào credential global, **tài khoản chỉ an toàn bằng mật khẩu yếu nhất trong N shop**.

| Thao tác từ token `shop:{shopId}` | |
|---|---|
| Đọc `FullName`, `AvatarUrl`, `Phone`, `Email` của chính mình | ✅ |
| Sửa `FullName`, `AvatarUrl`, `Phone` | ✅ hồ sơ dùng chung, propagate mọi nơi |
| Đổi password **của chính shop đó** | ✅ |
| Đọc/ghi booking, review, dữ liệu **tại shop đó** | ✅ |
| **Đổi `User.Email`** | ❌ đổi email → "quên mật khẩu" → chiếm toàn bộ tài khoản |
| **Đặt/đổi password global (`vsite.vn`)** | ❌ leo thang từ scope shop lên scope platform |
| **Thêm/xoá `ExternalLogin`** | ❌ gắn Google của attacker = cửa hậu vĩnh viễn |
| **Đổi password của shop khác** | ❌ |
| **Liệt kê shop khác mà user thuộc về** | ❌ phá ảo giác tách biệt + lộ đời tư |
| **Đọc booking/review ở shop khác** | ❌ |

Quy tắc một câu: **hồ sơ thì được, credential và dữ liệu xuyên shop thì không.**

Muốn làm các thao tác ❌ → phải đăng nhập tại `vsite.vn` bằng credential global (hoặc social login).

**Cách enforce:**

- Authorization policy đọc `aud` của token; mọi endpoint chạm tới credential/identity gắn policy `RequireGlobalScope`.
- **Test tự động bắt buộc:** với mỗi endpoint identity, có test khẳng định token `shop:*` bị từ chối. Chỉ cần **một** endpoint quên là toàn bộ thiết kế sụp — không dựa vào code review.
- ⚠️ **Ghi invariant này vào `CLAUDE.md`** của module Identity, cùng nhóm với `basePath`/`resolveUrl()` (Quyết định #11) và isomorphic renderer (Quyết định #23). Đây là loại lỗi AI agent sẽ vi phạm liên tục nếu không được nói trước.

---

## 8. Đối chiếu với thiết kế ban đầu

| Thiết kế ban đầu | Chốt lại | Lý do |
|---|---|---|
| Email KHÔNG phải identity key | Email **LÀ** identity key, UNIQUE, luôn verified | Bỏ được toàn bộ nhóm bài toán "hai User cùng email"; đổi lại phải verify nghiêm |
| `UserShop` có username + password | **Giữ password**, bỏ username (dùng email làm định danh đăng nhập) | Password riêng giữ ảo giác tách biệt + autofill trình duyệt hoạt động đúng (§3.3). Username riêng là gánh nặng trí nhớ không đổi lại lợi ích gì |
| Hồ sơ (tên/avatar/SĐT) không nói rõ scope | **Dùng chung** ở `User`, không scoped theo shop | Bắt nhập lại ở từng shop phiền hơn giá trị tách biệt; với booking thì SĐT mới thường là số đúng |
| Không bắt buộc identity nào khi đăng ký | **Bắt buộc** email đã verify **hoặc** Zalo | Phải có ít nhất một kênh sở hữu được để authenticate và recover |
| Email từ provider "có thể dùng khi phù hợp" | Chỉ auto-link khi `email_verified = true` | "Phù hợp" quá mơ hồ để AI agent implement đúng — phải là điều kiện kiểm tra được |
| Merge theo email/SĐT chưa verify (Quyết định #28) | **Không merge**, chỉ link | Đóng lỗ hổng account-takeover xuyên shop |
| `IsOwner` bool | `RoleId → Role.Code = 'Owner'` | Giữ nguyên — thiết kế ban đầu đã đúng |
| Provider + ProviderUserId là external identity | Giữ nguyên | Giữ nguyên — thiết kế ban đầu đã đúng |
| Phone/CCCD là hồ sơ, không phải identity | Giữ nguyên | Giữ nguyên — thiết kế ban đầu đã đúng |
| `Role.Scope` phân biệt Platform/Shop | Giữ nguyên + thêm composite FK enforce ở DB | Ý tưởng đúng, cần thêm lớp chặn ở DB |

---

## 9. Trạng thái rủi ro

### ✅ Đã đóng

| Rủi ro | Đóng bằng |
|---|---|
| Account-takeover xuyên shop qua merge email/SĐT chưa verify (Quyết định #28) | **Hai lớp:** (a) luôn verify email khi đăng ký, kể cả lần thứ N ở shop khác (§6.1) — kẻ tấn công không nhận được mail nên không tạo được `UserShop`; (b) token `shop:*` không chạm được credential global (§7.1) — password shop yếu nhất không kéo theo mất tài khoản |
| Leo thang từ scope shop lên scope platform | Bảng năng lực token ở §7.1 + policy `RequireGlobalScope` + test tự động |
| Email squatting → chiếm tài khoản khi nạn nhân login social | `User` chỉ tạo sau verify, dùng `PendingRegistration` (§5) |
| Auto-link nhầm qua email chưa verify từ provider | Bắt buộc đọc `email_verified` (§6.2) |
| Gán nhầm role sai scope (Owner làm platform role) | Composite FK `(RoleId, RoleScope)` (§4) |
| Token cũ giữ quyền sau khi bị revoke ở shop | Query `UserShop` mỗi request, JWT chỉ để render UI (§7) |

### ⚠️ Cần xử lý khi implement

| # | Việc | Ghi chú |
|---|---|---|
| 1 | Rate-limit endpoint đăng ký & quên mật khẩu | Không rate-limit → spam mail + dò email nào đã đăng ký |
| 2 | Password hashing | Dùng ASP.NET Core Identity `PasswordHasher` (Argon2id/PBKDF2 có sẵn), **không tự cài** salt + hash |
| 3 | Xoá tài khoản / GDPR-like | `Status = Deleted` giữ `Id` để không vỡ FK; nhưng phải giải phóng `EmailNormalized` (ví dụ đổi thành `deleted+{id}@...`) nếu không user không đăng ký lại được bằng email cũ |
| 4 | Đổi email của user đã có email | Phải verify email **mới** trước khi ghi đè, và gửi cảnh báo tới email **cũ** |
| 5 | Owner cuối cùng rời shop | Chặn xoá `UserShop` cuối cùng có role Owner — shop không được phép mồ côi |
| 6 | Zalo app-scoped user ID | Chốt dùng **một** Zalo app duy nhất cho toàn nền tảng, hoặc coi mỗi app là một `Provider` riêng |
| 7 | Case-folding email | `EmailNormalized` chuẩn hoá bằng `ToUpperInvariant()` (theo chuẩn ASP.NET Core Identity) — chốt một cách, dùng nhất quán FE lẫn BE |
| 8 | `ShopCustomerDto` cho góc nhìn shop | Không serialize thẳng `User`. Xem bảng ranh giới ở §3.3 |
| 9 | Throttle ghi `LastActiveAt` | Ngưỡng > 1 giờ hoặc gom qua Hangfire — không ghi mỗi request |
| 10 | Test tự động cho §7.1 | Mỗi endpoint identity phải có test khẳng định token `shop:*` bị từ chối |
| 11 | Rate-limit / lockout **theo từng scope** | Dò password ở Shop C không được làm khóa luôn tài khoản ở Shop A — nếu không, đây thành vector DoS nhắm vào một user cụ thể |
| 12 | Reset token phải mang scope | Token reset của Shop A không được dùng để đổi password `vsite.vn` hay Shop B (§6.4) |
| 13 | Chính sách độ mạnh mật khẩu áp cho **cả** `UserShop` | Password shop yếu là điểm vào; §7.1 giới hạn thiệt hại nhưng không nên để nó quá dễ dò |

### 📌 Cố tình để mở

- **Verify Phone/CCCD:** cột `PhoneVerifiedAt`/`CCCDVerifiedAt` đã có sẵn nhưng chưa dùng. Verify SĐT cần dịch vụ SMS bên ngoài, chưa cần cho MVP. Kể cả sau khi verify, chúng **vẫn là hồ sơ**, không tự động thành identity key — muốn đổi phải ra quyết định mới ghi tại tài liệu này.
- **Phân quyền chi tiết trong shop** (Manager được làm gì, Accountant được làm gì): `Role` đã có chỗ, nhưng permission matrix thuộc Phase 2 cùng tính năng "Phân quyền nhân viên".

---

## 10. Checklist khi thêm provider đăng nhập mới

Thêm provider (Apple, TikTok...) **không** được sửa bảng `User`. Chỉ cần:

1. Thêm giá trị vào enum `Provider`
2. Cấu hình OAuth client ở BE (`.AddApple()` ...)
3. Đăng ký redirect URI — **chỉ một lần**, tại `api.vsite.vn/auth/external/callback` (Quyết định #6)
4. Xác định provider có trả `email_verified` không → quyết định nhánh nào ở §6.2
5. Test nhánh "không có email" nếu provider không trả email

Nếu bước nào đòi hỏi sửa `User` → thiết kế đã sai ở đâu đó, quay lại đọc §3.2.
