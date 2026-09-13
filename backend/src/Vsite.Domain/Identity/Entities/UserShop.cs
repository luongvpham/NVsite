using Vsite.Domain.Common;
using Vsite.Domain.Identity.Enums;

namespace Vsite.Domain.Identity.Entities;

/// <summary>
/// 03 §3.3 — membership thuần: user này thuộc shop nào, vai trò gì. Kế thừa
/// <see cref="ShopAuditableEntity"/> → `ShopId` + Global Query Filter tự động
/// (`Vsite.Infrastructure.Persistence.TenantQueryFilterExtensions`), không viết tay `HasQueryFilter` nữa.
///
/// `PasswordHash` — cùng lý do với <see cref="User.PasswordHash"/>, không có cột `PasswordSalt`
/// riêng vì dùng `PasswordHasher&lt;User&gt;` (salt nằm trong chuỗi hash).
/// `Source` bất biến sau khi tạo — chỉ set ở nhánh INSERT (03 §3.3, Quyết định #29).
/// </summary>
public sealed class UserShop : ShopAuditableEntity
{
    public UserShop()
    {
    }

    public UserShop(Guid id) : base(id)
    {
    }

    public required Guid UserId { get; init; }

    public required Guid RoleId { get; set; }

    /// <summary>Generated column ('Shop' cố định) — xem 03 §4 ràng buộc [4].</summary>
    public RoleScope RoleScope { get; private set; } = RoleScope.Shop;

    public string? PasswordHash { get; set; }

    public required UserShopSource Source { get; init; }
    public UserShopStatus Status { get; set; } = UserShopStatus.Active;

    public DateTimeOffset? LastActiveAt { get; set; }

    public User? User { get; init; }
    public Shop? Shop { get; init; }
}
