using Vsite.Domain.Common;
using Vsite.Domain.Identity.Enums;

namespace Vsite.Domain.Identity.Entities;

/// <summary>
/// 03 §3.3 — membership thuần: user này thuộc shop nào, vai trò gì. Kế thừa
/// <see cref="TenantAuditableEntity"/> → `ShopId` + Global Query Filter tự động
/// (`Vsite.Infrastructure.Persistence.TenantQueryFilterExtensions`), không viết tay `HasQueryFilter` nữa.
/// Đọc XUYÊN shop (luồng auth) thì qua `Vsite.Application.Identity.UserShopQueries`, không gọi
/// `IgnoreQueryFilters()` trực tiếp.
///
/// `PasswordHash` — cùng lý do với <see cref="User.PasswordHash"/>, không có cột `PasswordSalt`
/// riêng vì dùng `PasswordHasher&lt;User&gt;` (salt nằm trong chuỗi hash).
/// `Source` bất biến sau khi tạo — chỉ set ở nhánh INSERT (03 §3.3, Quyết định #29).
///
/// Trạng thái (`RoleId`, `PasswordHash`, `Status`, `LastActiveAt`) chỉ gán được lúc khởi tạo
/// (object initializer), sau đó đổi qua method (REFACTOR-AUTHZ-001).
/// </summary>
public sealed class UserShop : TenantAuditableEntity
{
    private Guid _roleId;
    private string? _passwordHash;
    private UserShopStatus _status = UserShopStatus.Active;
    private DateTimeOffset? _lastActiveAt;

    public UserShop()
    {
    }

    public UserShop(Guid id) : base(id)
    {
    }

    public required Guid UserId { get; init; }

    public required Guid RoleId { get => _roleId; init => _roleId = value; }

    /// <summary>Generated column ('Shop' cố định) — xem 03 §4 ràng buộc [4].</summary>
    public RoleScope RoleScope { get; private set; } = RoleScope.Shop;

    public string? PasswordHash { get => _passwordHash; init => _passwordHash = value; }

    public required UserShopSource Source { get; init; }

    public UserShopStatus Status { get => _status; init => _status = value; }

    public DateTimeOffset? LastActiveAt { get => _lastActiveAt; init => _lastActiveAt = value; }

    public User? User { get; init; }

    public void SetPassword(string passwordHash) => _passwordHash = passwordHash;

    public void MarkActive(DateTimeOffset at) => _lastActiveAt = at;

    /// <summary>
    /// Khách đăng ký lại ở shop mà membership cũ đã bị xoá mềm (REFACTOR-AUTHZ-001, người duyệt chốt
    /// "khôi phục"): unique index <c>(user_id, shop_id)</c> không lọc soft delete nên phải dùng lại
    /// dòng cũ. Về role Customer, Active, mật khẩu mới. `Source` GIỮ NGUYÊN (bất biến, #29).
    /// `DeletedAt` do `AppDbContext` tự xoá khi `IsDeleted` về false.
    /// </summary>
    public void RestoreAsCustomer(string passwordHash)
    {
        if (!IsDeleted)
        {
            throw new InvalidOperationException("Chỉ khôi phục được membership đã xoá mềm.");
        }

        IsDeleted = false;
        _status = UserShopStatus.Active;
        _roleId = WellKnownRoles.CustomerId;
        _passwordHash = passwordHash;
    }
}
