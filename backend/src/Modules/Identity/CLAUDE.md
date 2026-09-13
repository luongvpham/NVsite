# Identity — vsite

## Trạng thái (Bước 3)

Phase 0 (hạ tầng Postgres/Redis) + Phase 1 (entity + migration + Global Query Filter) + Phase 2
(đăng ký/verify/đăng nhập/refresh/quên-đổi mật khẩu, 6 endpoint, `IDENTITY-001` đã Gate 1) đã xong,
kèm `Api.Tenancy.TenantResolutionMiddleware` (resolve tenant từ Host — xem mục riêng bên dưới).
Phase 3 (auth policy `RequireGlobalScope` + test token scope) chưa làm.

**6 endpoint hiện có** (`Identity.Api/IdentityEndpoints.cs`): `/auth/{register,verify-email,login,
refresh-token,forgot-password,reset-password}` — MỘT bộ route duy nhất cho mọi context (không có
`{shopId}` trong route nào — xem "Tenant Resolution Middleware" bên dưới). **Chưa có policy
authorize nào** — mọi endpoint hiện AllowAnonymous, kể cả refresh-token/reset-password (đúng — đó
chính là mục đích của chúng, không cần JWT hợp lệ trước). Phase 3 sẽ thêm `RequireGlobalScope`.

## ⚠️ Tenant Resolution Middleware — đọc trước khi thêm endpoint mới

`Api.Tenancy.TenantResolutionMiddleware` (root `Api`, không phải module) resolve
`ITenantContext.AudienceKind`/`ShopId` từ **Host header** của request, chạy TRƯỚC MediatR/mọi
handler. Cơ chế (Quyết định #7, THU HẸP LẠI — không phải bản đầy đủ):

1. Nhãn đầu tiên của hostname (`spa-abc` trong `spa-abc.vsite.local:5270`) = `"admin"` → audience
   `vsite-portal` (Quyết định #25, đã reserved).
2. Nhãn khớp `config/reserved-routes.json` → audience `vsite-main`, khỏi query DB.
3. Nhãn khớp một `Shop.Slug` đang tồn tại (qua `IShopLookupService`) → audience `shop:{shopId}`.
4. Còn lại → audience `vsite-main`.

**⚠️ `IShopLookupService` cache qua Redis** (Quyết định #7 — "Cache Redis: host+path → shop_id,
invalidate khi đổi domain"), vì middleware gọi hàm này ở MỌI request: 30 phút cho slug tìm thấy, 1
phút cho slug không tồn tại (negative cache, tránh dội DB khi domain lạ/gõ sai). **BẤT KỲ command
nào sau này sửa `Shop.Slug` hoặc xoá/disable Shop PHẢI gọi `IShopLookupService.InvalidateAsync`**
(cả slug cũ lẫn mới nếu đổi) NGAY sau khi `SaveChangesAsync` thành công — quên bước này nghĩa là
middleware tiếp tục resolve sai tenant tới khi cache tự hết hạn (tối đa 30 phút).

**Hệ quả cho MỌI endpoint mới ở module này (và sau này module khác dùng chung middleware):**
- KHÔNG BAO GIỜ nhận `ShopId` từ route param hay body nữa — luôn đọc `ITenantContext` (inject vào
  handler). Route `/shops/{shopId}/...` mà tài liệu `03`/`05` mô tả **không** áp dụng ở Bước 3 —
  FE không có cách nào biết GUID của shop, chỉ biết domain/slug đang phục vụ trang.
- FE gọi các endpoint này bằng **relative URL** (same-origin với domain đang phục vụ trang) —
  KHÔNG cross-origin tới một host `api.*` cố định, vì Host header chỉ đúng khi request thực sự đi
  tới domain của chính shop đó (Production: Caddy reverse-proxy giữ nguyên Host, Quyết định #9).
- Ngoại lệ: `verify-email`/`reset-password` LUÔN dùng host cố định `api.vsite.vn` (03 §5) — context
  nằm trong chính token, không phụ thuộc Host header lúc bấm link email.
- **CHƯA resolve được custom domain thật** (`spa-abc.com` bất kỳ) — chỉ nhãn-đầu-của-host khớp
  `Shop.Slug` (đúng cho Path `vsite.vn/{slug}` lẫn Subdomain `{slug}.vsite.vn`, và dev qua hosts
  file `{slug}.vsite.local`). Custom domain cần bảng `ShopDomain` (module Shop đầy đủ) để match
  CHÍNH XÁC domain — deferred, xem mục "Lệch có chủ đích" bên dưới.
- Test dev: thêm entry hosts file Windows (`C:\Windows\System32\drivers\etc\hosts`), vd.
  `127.0.0.1 spa-abc.vsite.local`, gọi thẳng `http://spa-abc.vsite.local:5270/auth/login` — Kestrel
  không quan tâm hostname, chỉ quan tâm port, nên Host header đúng thật không cần Caddy.

## Invariant — vi phạm là bug, không phải lựa chọn phong cách

1. `ShopId` **không bao giờ** từ request body hay route param — CHỈ từ `ITenantContext`, resolve
   bởi `TenantResolutionMiddleware` theo Host (Quyết định #21.4, #7 thu hẹp). Endpoint tương lai cần
   `ShopId` cho path/route riêng (vd. Portal xem NHIỀU shop cùng lúc, `/shops/{shopId}/products`)
   vẫn dùng route param như bình thường — điểm khác biệt duy nhất ở Identity là auth tự thân
   (login/register/...) không có "route riêng theo shop" nữa, tất cả qua Host.
2. `UserShop` là entity tenant-scoped DUY NHẤT ở Bước 3. `User`/`Role`/`ExternalLogin`/`Shop` là
   platform-scoped hoặc không cần Global Query Filter (Shop chưa multi-tenant theo nghĩa này).
3. Global Query Filter fail-closed: `ITenantContext.ShopId == null` → filter không khớp hàng nào
   (query rỗng), KHÔNG bao giờ "bỏ qua filter khi null". Xem `IdentityDbContext.OnModelCreating`.
4. **⚠️ Token `shop:{shopId}` không bao giờ đọc/sửa credential global hay dữ liệu xuyên shop**
   (Quyết định #32, `03` §7.1 — tài liệu tự nhận "quan trọng nhất"). Mỗi endpoint identity PHẢI có
   test khẳng định token `shop:*` bị từ chối — làm ở Phase 3, đừng quên khi thêm endpoint mới.
5. `Owner` không bao giờ lưu ở `User.RoleId` — ownership luôn qua `UserShop.RoleId → Role.Code =
   'Owner'` (03 §3.5). Không dùng cột `IsOwner`.
6. `UserShop.Source` bất biến sau khi tạo — chỉ set ở nhánh INSERT (03 §3.3).
7. KHÔNG merge hai `User` theo email/SĐT — chỉ **link** thêm `ExternalLogin` (Quyết định #29, đóng
   lỗ hổng account-takeover của Quyết định #28 cũ).
8. KHÔNG ghi password context-shop vào `User.PasswordHash` (và ngược lại) — 03 §6.1 cảnh báo hai lần
   đây là lỗi nguy hiểm nhất của luồng đăng ký.

## Lệch có chủ đích so với `DesignIdeal/03-identity-entity-design.md`

- **Không có cột `PasswordSalt` riêng trên `User`/`UserShop`.** 03 §3.1/§3.3 vẽ hai cột
  `PasswordSalt`+`PasswordHash`. Thực thi dùng
  `Microsoft.AspNetCore.Identity.PasswordHasher<User>` — output của hasher này tự chứa salt +
  iteration count trong MỘT chuỗi (`PasswordHash`), nên cột `PasswordSalt` sẽ luôn rỗng/chết nếu giữ
  lại. Đúng khuyến nghị "không tự cài salt+hash" ở 03 §9 rủi ro #2. Quyết định kỹ thuật nhỏ, không
  phải nghiệp vụ — nếu đổi sang thư viện hash khác cần lưu salt riêng, thêm lại cột này.
- **`Shop` chỉ là bản trích tối thiểu** (03 §3.4: Id/Name/Slug/Kind/ExternalUrl/Status). Nguồn sự
  thật đầy đủ là `04-listing-and-review-design.md` §2.1 (Quyết định #39.5), module `Shop` riêng
  chưa làm. KHÔNG thêm field hồ sơ (địa chỉ, toạ độ, giờ mở cửa...) vào entity này.
- **Chưa có Social Login thật** (Google/Facebook/Zalo, Quyết định #6) — bảng `ExternalLogin` đã tạo
  đủ schema (rẻ, không cần chờ), nhưng luồng OAuth/callback/handoff-code là task riêng SAU khi
  module `Shop` đầy đủ tồn tại (cần `ShopDomain` để tra callback URL theo slug).
- **Tenant Resolution Middleware ĐÃ có, nhưng thu hẹp** (Quyết định #7) — chỉ Path/Subdomain qua
  `Shop.Slug` (xem mục riêng phía trên). Custom domain (`ShopDomain`, Caddy On-Demand TLS #9) vẫn
  deferred tới khi module Shop đầy đủ tồn tại.
- **`UserShop` không có navigation `Shop` qua base class** — `ShopAuditableEntity` (Shared) chỉ có
  `ShopId` (Guid), không navigation, vì `Shared` không được reference entity của module nào
  (Quyết định #1). Navigation `UserShop.Shop` tự khai thêm ở entity cụ thể trong module này.
- **`RefreshToken`/`PasswordResetToken` là entity MỚI, không có trong `03`** — cần thiết để hiện
  thực Quyết định #3 (refresh token rotation) và #6.4 (reset scoped theo audience/shop). Coi là chi
  tiết triển khai của các quyết định đã chốt, không phải entity nghiệp vụ mới cần duyệt riêng — xem
  `docs/tasks/IDENTITY-001/contract-diff.md` mục "Giả định tôi đã tự đặt" #5.
- **Login bằng email/password tại shop chưa có `UserShop` → 401**, không tự tạo membership. 03 §3.3
  "mọi lần authenticate → upsert UserShop" chỉ áp dụng cho Social Login (chưa làm) — với email/
  password, không có `UserShop.PasswordHash` để so khớp nên không thể "login" vào một membership
  chưa tồn tại. Phải đăng ký (`/auth/register`, cùng domain shop đó) trước.

## Base class + tổ chức thư mục (xem `backend/CLAUDE.md` cho quy ước chung mọi module)

- `User`, `Role`, `ExternalLogin`, `Shop` → `BaseAuditableEntity` (platform-scoped, có audit trail).
- `UserShop` → `ShopAuditableEntity` (tenant-scoped — entity DUY NHẤT ở Bước 3 có Global Query
  Filter tự động theo `ShopId`, không viết tay `HasQueryFilter`).
- `PendingRegistration` → `BaseEntity` thẳng (staging ngắn hạn, không cần audit/soft-delete).
- `Entities/`, `Enums/` dưới `Identity.Domain`; `Persistence/`, `Persistence/Configurations/` dưới
  `Identity.Infrastructure` — đúng layout `DesignIdeal/architecture-guide.md` §2/§5.
- Entity cần GUID cố định (seed data, vd. `Role`) tự expose `public {Entity}(Guid id) : base(id)`
  — `Id` là `protected set`, không gán tự do qua object initializer từ ngoài (xem `RoleSeed.cs`).
- Xoá (`Remove`) một entity `BaseAuditableEntity` = soft-delete tự động (`IsDeleted=true`, ẩn qua
  Global Query Filter) — `AppDbContextBase.SaveChangesAsync` chặn `EntityState.Deleted` lại, không
  cần tự viết logic soft-delete trong Application layer khi làm Phase 2.

## Ranh giới kiến trúc

- `Identity.Domain` không reference EF Core/ASP.NET Core, và không trực tiếp dùng type của MediatR
  (`ArchitectureTests.LayeringTests` enforce bằng NetArchTest) — chỉ gián tiếp qua `Shared.BaseEvent`
  (implements `MediatR.INotification`), không tính là vi phạm vì Identity.Domain's IL không chạm
  trực tiếp namespace MediatR.
- `Identity.Infrastructure` reference `Identity.Domain` + `Shared.Persistence` — không cross-module.
- Composite FK `(RoleId, RoleScope)` → `Role(Id, Scope)` nằm ở tầng DB (migration), không chỉ code —
  gán role sai scope (vd. gán `Owner` làm `User.RoleId`) không compile được ở DB.
