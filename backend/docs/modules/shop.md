# Shop — vsite

## Trạng thái (SHOP-001)

Entity `Shop` đầy đủ (04 §2.1) + 4 endpoint (`tạo/sửa/xem/liệt kê shop của mình`) + cơ chế resolve
`ShopId` cho Portal từ route `{shopId}` đã xong. `SHOP-001` ở Gate 1 (`contract-diff.md`), chưa có
`brief.md` — xem `Docs/tasks/SHOP-001/`.

**4 endpoint hiện có** (`Vsite.Api/Shop/ShopEndpoints.cs`), tất cả `RequireAuthorization(AuthPolicies.RequireGlobalScope)`
(Quyết định #32 — token `shop:{shopId}` không được tạo/sửa shop, đó là thao tác quản trị Portal/Main):

- `POST /shops` — tạo shop mới. Nhánh **DUY NHẤT** sinh `UserShop(RoleId=Owner, Source=ShopCreator)`
  (03 §3.3) — tạo Shop + UserShop trong MỘT `SaveChangesAsync`.
- `GET /shops` — shop mà user hiện tại là thành viên (`Active`), kèm `RoleCode` — thay thế hoàn
  toàn `GET /auth/me/shops` cũ của Identity (xem `Docs/tasks/SHOP-001/contract-diff.md`).
- `GET /shops/{shopId}` — đọc một shop, qua `ShopMembershipEndpointFilter`.
- `PATCH /shops/{shopId}` — sửa `Name`/`Slug`/`Kind`/`ExternalUrl`/`Status` (full-replace, không
  phải partial patch), qua `ShopMembershipEndpointFilter` + chỉ role `Owner` (check ở
  `UpdateShopHandler`, error code `SHOP_OWNER_REQUIRED`).

## ⚠️ Resolve `ShopId` cho Portal — đọc trước khi thêm endpoint `{shopId}` mới

Quyết định #31: Portal lấy `ShopId` từ route `/shops/{shopId}/...`, **trường hợp DUY NHẤT** ShopId
không đến từ Host. Nhưng `TenantResolutionMiddleware` set `ShopId = null` cho audience Portal, và
Global Query Filter fail-closed (`null` không khớp `Guid` non-null nào) — nếu không có bước riêng,
mọi query shop-scoped ở Portal trả rỗng.

`Vsite.Api.Tenancy.ShopMembershipEndpointFilter` (namespace dùng chung, không thuộc module nào —
mọi module Portal sau này dùng lại) giải quyết việc này bằng `IEndpointFilter`:

1. Đọc `shopId` từ route.
2. Query `UserShop(userId, shopId)` còn `Active` **tại request này** — không tin claim JWT (tái
   dùng `IUserShopMembershipService.IsActiveMemberAsync`, cùng service Identity Phase 3 đã có,
   không viết lại query).
3. Không có record → 403 `SHOP_ACCESS_DENIED`, KHÔNG fallback role mặc định.
4. Có → set `TenantContext.ShopId = shopId`.

**⚠️ Gắn bằng `.RequireShopMembership()` (`Vsite.Api.Tenancy.ShopScopedEndpointExtensions`), KHÔNG
gọi thẳng `.AddEndpointFilter<ShopMembershipEndpointFilter>()`.** Method này gắn CẢ filter LẪN một
metadata marker rỗng (`ShopMembershipRequiredMarker`) — marker là thứ DUY NHẤT làm cho endpoint
filter "nhìn thấy được" từ bên ngoài (filter tự nó biên dịch thẳng vào request pipeline, không để
lại dấu vết ở `Endpoint.Metadata`). `ShopScopedRouteFilterTests`
(`backend/tests/IntegrationTests/Tenancy/`) duyệt `EndpointDataSource` **thật** sau khi host build,
tìm mọi route có tham số `shopId`, khẳng định marker có mặt — **quên gọi `RequireShopMembership()`
cho endpoint `{shopId}` mới là test RED ngay**, không cần ai nhớ (#17). Test này không cần Docker
(chỉ đọc route table, không gửi request chạm DB/Redis).

Chỉ cần cho endpoint có `{shopId}` trong route. `POST /shops` và `GET /shops` KHÔNG cần (không có
`{shopId}`).

**⚠️ Giả định đã tự đặt:** sau bước này, `TenantContext.ShopId` có giá trị dù `AudienceKind` vẫn là
`Portal` (không đổi thành `Shop`) — nới rộng invariant cũ "`ShopId` chỉ có giá trị khi `AudienceKind`
= Shop" (xem XML doc `ITenantContext.ShopId` và `Docs/tasks/SHOP-001/contract-diff.md`).

**Quyền "chỉ Owner"** (cho `PATCH`) không nằm trong filter dùng chung ở trên — filter chỉ xác nhận
membership generic. Business rule riêng của từng endpoint (như "chỉ Owner mới sửa được shop") kiểm
ở tầng handler (`UpdateShopHandler` tự query `RoleId`), theo đúng invariant #21.5 "Quyền theo shop
kiểm ở Authorization Handler/handler, không tin claim trong token".

## Base class + tổ chức thư mục (xem `backend/CLAUDE.md` cho quy ước chung mọi module)

- `Shop` → `BaseAuditableEntity` (platform-scoped — bản thân Shop LÀ tenant, không kế thừa
  `ShopEntity`/`ShopAuditableEntity`).
- File của module nằm ở 4 chỗ: `Vsite.Domain/Shop/{Entities,Enums}/` ·
  `Vsite.Application/Shop/{Interfaces,Dtos,Commands,Queries}/` · `Vsite.Infrastructure/Shop/` +
  `Vsite.Infrastructure/Persistence/Configurations/Shop/` · `Vsite.Api/Shop/`.
- `Shop.dependsOn = ["Identity"]` (`Docs/architecture/dependency-map.json`) — module Shop được phép
  reference `Vsite.*.Identity` (vd. `WellKnownRoles`, `UserShopStatus`), chiều ngược lại (Identity →
  Shop) bị `ModuleBoundaryTests` chặn. `UserShop` (thuộc Identity) vì vậy KHÔNG có navigation
  `UserShop.Shop` — chỉ giữ `ShopId` dạng `Guid` thuần; FK khai từ **phía Shop**
  (`ShopConfiguration.HasMany<UserShop>()...`).

## Slug — validation (Quyết định #8, #24)

- Không nằm trong `reservedPaths` hoặc `reservedSubdomains` của `config/reserved-routes.json` — đọc
  qua `IReservedRoutesProvider` (đã có sẵn, dùng chung FE + BE, KHÔNG viết tay danh sách thứ hai).
- Format: lowercase, `[a-z0-9-]`, không bắt đầu/kết thúc bằng `-`, 3–63 ký tự.
- Unique — CHECK ở validator (422) **và** unique index DB; trùng slug ở handler → 409
  `SHOP_SLUG_ALREADY_TAKEN`.
- **Đổi `Slug` (PATCH) PHẢI gọi `IShopLookupService.InvalidateAsync` cho CẢ slug cũ lẫn mới**, ngay
  sau `SaveChangesAsync` thành công (`UpdateShopHandler`) — quên bước này = middleware
  (`TenantResolutionMiddleware`) tiếp tục resolve sai tenant tới khi cache Redis tự hết hạn (tối đa
  30 phút, xem `backend/docs/modules/identity.md`).

## Ràng buộc `Kind`/`ExternalUrl` (04 §2.1)

`Kind = ExternalOnly ⇒ ExternalUrl NOT NULL` — chặn ở CẢ hai lớp:
1. `CreateShopValidator`/`UpdateShopValidator` (422, `FluentValidation`).
2. CHECK constraint DB `ck_shop_external_url` (`ShopConfiguration`) — lớp phòng thủ thứ hai, test ở
   `DbConstraintTests.Check_constraint_rejects_ExternalOnly_shop_without_ExternalUrl`.

## Lệch có chủ đích / chưa làm xong so với `DesignIdeal/04-listing-and-review-design.md`

> 📌 Danh sách đầy đủ: `Docs/tasks/SHOP-001/changelog.md`.

- **04 §2.2** (đổi `Hosted → ExternalOnly` phải chuyển/gỡ `Listing` trỏ nội bộ) — để **RỖNG có chủ
  đích**. `Listing` thuộc module `Marketplace`, chưa tồn tại. Test
  `[Fact(Skip = "Listing chưa tồn tại — mở lại ở module Marketplace")]` ở `ShopEndpointTests.cs`
  đánh dấu chỗ cần mở lại.
- Chỉ 4 field business (`Name`/`Slug`/`Kind`/`ExternalUrl`/`Status`) — không có field hồ sơ công
  khai (địa chỉ, toạ độ, giờ mở cửa...), đúng phạm vi 04 §2.1 (những field đó thuộc `04` §5 hồ sơ
  shop công khai, task riêng của `apps/web` + module `Marketplace`).
- Mời nhân viên / phân quyền Manager/Staff/Accountant — Phase 4 (Quyết định #39.1), ngoài phạm vi.
- Xoá shop cứng — chưa có nhu cầu nghiệp vụ; `Status = Closed` là đủ cho giờ.
- `ShopDomain` / custom domain / Caddy On-Demand TLS (Quyết định #7, #9) — gắn với module `Website`.
