namespace Identity.Domain.Enums;

/// <summary>
/// DesignIdeal/03-identity-entity-design.md §3.5. Dùng chung cho <see cref="Role.Scope"/> và cột
/// generated <see cref="User.RoleScope"/> / <see cref="UserShop.RoleScope"/> — composite FK
/// (RoleId, RoleScope) chặn gán role sai scope ngay ở tầng DB (§4, ràng buộc [4]).
/// </summary>
public enum RoleScope
{
    Platform,
    Shop,
}
