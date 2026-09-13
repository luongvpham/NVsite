using Identity.Domain;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Role cố định theo 03 §3.5 bảng Scope=Platform/Shop. Seed qua migration (HasData), không phải
/// endpoint — GUID cố định (`WellKnownRoles`, Identity.Domain) để migration idempotent giữa các
/// môi trường và Application layer gán RoleId lúc tạo User/UserShop mà không cần reference Infra.
/// </summary>
public static class RoleSeed
{
    // Phải khai TRƯỚC `All` — field initializer tĩnh chạy theo đúng thứ tự khai báo trong file;
    // khai sau `All` sẽ khiến `Epoch` là default(DateTimeOffset) (năm 1) lúc `All` được build.
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static Role[] All { get; } =
    [
        new(WellKnownRoles.PlatformUserId) { Code = "PlatformUser", Name = "Người dùng nền tảng", Scope = RoleScope.Platform, IsSystem = true, CreatedAt = Epoch, UpdatedAt = Epoch },
        new(WellKnownRoles.PlatformAdminId) { Code = "PlatformAdmin", Name = "Quản trị nền tảng", Scope = RoleScope.Platform, IsSystem = true, CreatedAt = Epoch, UpdatedAt = Epoch },
        new(WellKnownRoles.OwnerId) { Code = "Owner", Name = "Chủ shop", Scope = RoleScope.Shop, IsSystem = true, CreatedAt = Epoch, UpdatedAt = Epoch },
        new(WellKnownRoles.ManagerId) { Code = "Manager", Name = "Quản lý", Scope = RoleScope.Shop, IsSystem = true, CreatedAt = Epoch, UpdatedAt = Epoch },
        new(WellKnownRoles.StaffId) { Code = "Staff", Name = "Nhân viên", Scope = RoleScope.Shop, IsSystem = true, CreatedAt = Epoch, UpdatedAt = Epoch },
        new(WellKnownRoles.AccountantId) { Code = "Accountant", Name = "Kế toán", Scope = RoleScope.Shop, IsSystem = true, CreatedAt = Epoch, UpdatedAt = Epoch },
        new(WellKnownRoles.CustomerId) { Code = "Customer", Name = "Khách hàng", Scope = RoleScope.Shop, IsSystem = true, CreatedAt = Epoch, UpdatedAt = Epoch },
    ];
}
