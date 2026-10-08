# REFACTOR-BE-001 — changelog

Refactor nền backend rút từ đợt review kiến trúc 2026-10-08 (P0 #1, #3, #8 + P1 đổi tên base class,
entity có hành vi). **Lane A** — contract không đổi (`contract-diff.md`: 22 operation UNCHANGED).

Không chạm thiết kế trong `DesignIdeal/` (không đổi schema, không đổi API) nên **không cập nhật
banner STATUS** nào. Quy ước mới ghi ở `backend/CLAUDE.md` và `backend/docs/modules/{shop,media}.md`.

---

## Đã làm

### 1. Validator của command trả về void không bao giờ chạy (bug thật)

- **Trước:** `ValidationBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>`. Với
  MediatR 12, `ChangePasswordCommand`/`ResetPasswordCommand`/`ForgotPasswordCommand` (`: IRequest`)
  đi pipeline `<TRequest, Unit>` nhưng không phải `IRequest<Unit>` → DI bỏ qua behavior.
  `ResetPasswordHandler` hash mật khẩu mới mà không qua `MinimumLength(8)`.
- **Sau:** ràng buộc `where TRequest : notnull`.
- **Khoá lại:** `ValidationPipelineCoverageTests` duyệt mọi request có validator, khẳng định
  `ValidationBehavior` có trong pipeline thật (không cần Docker). Với code cũ, test không biên
  dịch được: CS0311, `ResetPasswordCommand` không phải `IRequest<Unit>`.

### 2. Kiểm quyền Owner gom về endpoint filter (#21.5)

- **Trước:** 3 bản kiểm Owner khác nhau: `UpdateShopHandler` tự query `UserShops`,
  `DeleteFromLibraryHandler` và `UploadShopLogoHandler` gọi `IShopOwnershipService`.
- **Sau:** `.RequireShopOwner(errorCode, detail)` (`Vsite.Api.Tenancy`) = `RequireShopMembership()`
  + metadata `ShopOwnerRequirement`. `ShopMembershipEndpointFilter` lấy `RoleId` trong **cùng** câu
  query membership (`IUserShopMembershipService.FindActiveRoleIdAsync`), nên handler không tự tra
  quyền nữa.
- **Đã xoá:** `IShopOwnershipService`, `ShopOwnershipService`. Handler bỏ dependency
  `ICurrentUserContext`.
- **Error code giữ nguyên:** `SHOP_OWNER_REQUIRED`, `MEDIA_OWNER_REQUIRED`.
- **Đổi hành vi (có chủ đích):** quyền giờ được kiểm *trước* validation và *trước* parse multipart.
  - Non-Owner gửi `PATCH /shops/{id}` với body sai: 403 (trước là 422).
  - Non-Owner gửi `PUT /logo` không phải multipart: 403 (trước là 415).
  - Không lộ thông tin cho người không có quyền.
- **Khoá lại:**
  - `ShopScopedRouteFilterTests.Owner_only_endpoints_carry_ShopOwnerRequirement` khoá danh sách 3
    endpoint và error code của từng endpoint.
  - `ShopMembershipEndpointFilterTests` unit test logic của filter (không cần Docker): không phải
    member, non-Owner trên endpoint Owner-only, member trên endpoint thường, Owner.
- **Test bị gỡ:** test non-Owner ở tầng handler (`LibraryHandlerTests`, `ShopLogoHandlerTests`,
  `SoftDeletedMembershipTests.UpdateShop_by_soft_deleted_owner_is_forbidden`). Hành vi này đã có
  test endpoint (Docker) giữ nguyên: `LibraryEndpointTests`, `ShopLogoTests`, `ShopEndpointTests`.
  Phần "Owner bị xoá mềm không còn là Owner" chuyển sang
  `SoftDeletedMembershipTests.UserShopMembershipService_role_lookup_ignores_soft_deleted_owner`.

### 3. Audit `CreatedByUserId` / `UpdatedByUserId` được ghi thật

- **Trước:** có cột nhưng `AppDbContext.StampAuditFields` chỉ ghi timestamp, nên mọi row đều NULL.
- **Sau:** `IAuditActor` (Application) + `HttpAuditActor` (Api, đọc `sub`, chưa đăng nhập thì
  `null`, không throw). `AppDbContext` nhận `IAuditActor?` (optional, test cũ không phải sửa) và
  ghi giá trị khi Added, Modified và lúc soft delete.
- **Test:** `AuditStampingTests` (InMemory), và
  `ValidationPipelineCoverageTests.Real_host_injects_IAuditActor_into_AppDbContext` (DI thật có
  truyền actor vào context — quên đăng ký thì audit âm thầm về NULL).
- **Dữ liệu cũ:** row tạo trước task này vẫn NULL. Chưa có production nên không backfill.

### 4. Đổi tên base class tenant và khoá `ShopId`

- `ShopEntity` → `TenantEntity`, `ShopAuditableEntity` → `TenantAuditableEntity`. Tên cũ đụng alias
  `ShopEntity = Vsite.Domain.Shop.Entities.Shop` ở 12 file.
- `ShopId` từ `{ get; set; }` đổi thành `{ get; init; }`: entity không thể bị chuyển sang tenant
  khác sau khi tạo. Hai test cố ý giả lập dữ liệu xuyên shop
  (`GetDerivativesHandlerTests`, `ShopLogoReaderTests`) set bằng reflection.
- Schema không đổi (tên class không vào DB), không có migration.

### 5. `Shop` có hành vi thay cho setter public

- Setter đổi thành `private`. Tạo shop bằng constructor
  `Shop(name, slug, kind, externalUrl?, status?)` (thêm overload có `Guid id`). Sửa shop qua
  `Update(...)`, gán logo qua `SetLogo(...)`. EF dùng constructor private không tham số.
- Invariant `Kind = ExternalOnly ⇒ ExternalUrl` chuyển vào entity, ném
  `DomainException("SHOP_EXTERNAL_URL_REQUIRED")` (400). Validator vẫn chặn trước (422), nên qua API
  bình thường không bao giờ gặp 400 này.
- `DbConstraintTests.Check_constraint_rejects_ExternalOnly_shop_without_ExternalUrl` giờ ép giá trị
  qua `ChangeTracker` để vẫn chạm tới CHECK ở DB.
- **Test:** `ShopEntityTests`.

---

## Chưa làm xong / để task khác

1. **`User`, `UserShop` vẫn dùng setter public.** Làm cùng task audit `IgnoreQueryFilters` của
   Identity (mục 2), task đó viết lại các luồng Identity đọc `UserShop`. Đã ghi vào 00-INDEX §4.
2. **7 handler Identity vẫn `UserShops.IgnoreQueryFilters()` thiếu `!IsDeleted`/`Status == Active`**
   (00-INDEX §4, nợ MEDIA-001 #15). Task này không chạm luồng auth — cần task riêng.
3. **Handler vẫn nhận `ShopId` từ command (route)** trong khi `MediaReferenceValidator` đọc
   `ITenantContext.ShopId`. Hai nguồn hiện bằng nhau nhờ filter. Chưa chốt quy ước một nguồn
   (00-INDEX §4).
4. **Cảnh báo bảo mật dependency (ngoài phạm vi, phát hiện khi build):** `SixLabors.ImageSharp
   3.1.12` có NU1903 (high) GHSA-j3p4-wp97-rph4, GHSA-j9gm-c75j-xc9q, GHSA-jjfr-hcj7-qf5w. Cần task
   nâng version riêng (pipeline ảnh xử lý file người dùng upload). Đã ghi vào 00-INDEX §4.

## Giả định tôi đã tự đặt

> ⚠️ Hai giả định đầu chạm invariant tenant (#21.5) — **cần người duyệt**. Duyệt xong thì ghi chú
> vào #21 ở `DesignIdeal/DECISIONS.md` để agent sau không đưa việc kiểm quyền về lại handler.

- Kiểm quyền bằng endpoint filter (đã có sẵn, đã có test phủ) được coi là đạt #21.5 "Authorization
  Handler", thay vì viết `IAuthorizationHandler` của ASP.NET Core. Lý do: filter đã đọc `shopId` từ
  route và query DB ở đúng một chỗ; tách thêm một policy nữa sẽ phải query membership hai lần.
- Chấp nhận thứ tự mới 403 trước 422/415 (mục 2).
- `UpdatedByUserId` không bị ghi đè thành null khi thao tác ẩn danh (vd. Register sửa
  `UserShop`): giữ người sửa có danh tính gần nhất.
