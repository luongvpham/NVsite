# Identity — vsite

## Trạng thái (Bước 3)

Phase 0 (hạ tầng Postgres/Redis) + Phase 1 (entity + migration + Global Query Filter) đã xong.
Phase 2 (đăng ký/verify/đăng nhập, Gate 1 bắt đầu từ đây) + Phase 3 (auth policy + token scope test)
chưa làm — xem plan hiện hành (`ok-ch-a-c-n-x-a-cuddly-thunder.md` nếu còn, hoặc hỏi lại kế hoạch).
**Không có endpoint nào ở module này tính tới hiện tại** — `Identity.Api` chưa tồn tại.

## Invariant — vi phạm là bug, không phải lựa chọn phong cách

1. `ShopId` **không bao giờ** từ request body — chỉ từ route hoặc `ITenantContext` (Quyết định
   #21.4). Portal lấy `ShopId` từ route param (Quyết định #31).
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
- **Chưa có Tenant Resolution Middleware theo Host/custom domain** (Quyết định #7, #9) — `ShopId`
  Phase 2/3 chỉ resolve từ JWT audience hoặc route param, không từ Host header. Middleware theo
  domain là việc của module Website/Shop đầy đủ (Phase 2 theo `dependency-map.json`).
- **`UserShop` không có navigation `Shop` qua base class** — `ShopAuditableEntity` (Shared) chỉ có
  `ShopId` (Guid), không navigation, vì `Shared` không được reference entity của module nào
  (Quyết định #1). Navigation `UserShop.Shop` tự khai thêm ở entity cụ thể trong module này.

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
