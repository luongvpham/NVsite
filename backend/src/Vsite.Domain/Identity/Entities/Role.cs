using Vsite.Domain.Common;
using Vsite.Domain.Identity.Enums;

namespace Vsite.Domain.Identity.Entities;

/// <summary>
/// 03 §3.5. `Owner` không bao giờ được lưu ở `User.RoleId` — quyền sở hữu luôn gắn với một shop cụ
/// thể (Ownership = UserShop.RoleId → Role.Code = 'Owner', không dùng cột IsOwner rời).
/// </summary>
public sealed class Role : BaseAuditableEntity
{
    public Role()
    {
    }

    /// <summary>Cho seed data — GUID cố định phải giữ ổn định giữa các môi trường (xem RoleSeed).</summary>
    public Role(Guid id) : base(id)
    {
    }

    public required string Code { get; init; }
    public required string Name { get; init; }
    public required RoleScope Scope { get; init; }
    public bool IsSystem { get; init; }
}
