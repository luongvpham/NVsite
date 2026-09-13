using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Role cố định theo 03 §3.5 bảng Scope=Platform/Shop. Seed qua migration (HasData), không phải
/// endpoint — GUID cố định để migration idempotent giữa các môi trường.
/// </summary>
public static class RoleSeed
{
    public static readonly Guid PlatformUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid PlatformAdminId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static readonly Guid OwnerId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    public static readonly Guid ManagerId = Guid.Parse("00000000-0000-0000-0000-000000000004");
    public static readonly Guid StaffId = Guid.Parse("00000000-0000-0000-0000-000000000005");
    public static readonly Guid AccountantId = Guid.Parse("00000000-0000-0000-0000-000000000006");
    public static readonly Guid CustomerId = Guid.Parse("00000000-0000-0000-0000-000000000007");

    // Phải khai TRƯỚC `All` — field initializer tĩnh chạy theo đúng thứ tự khai báo trong file;
    // khai sau `All` sẽ khiến `Epoch` là default(DateTimeOffset) (năm 1) lúc `All` được build.
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static Role[] All { get; } =
    [
        new(PlatformUserId) { Code = "PlatformUser", Name = "Người dùng nền tảng", Scope = RoleScope.Platform, IsSystem = true, CreatedAt = Epoch, UpdatedAt = Epoch },
        new(PlatformAdminId) { Code = "PlatformAdmin", Name = "Quản trị nền tảng", Scope = RoleScope.Platform, IsSystem = true, CreatedAt = Epoch, UpdatedAt = Epoch },
        new(OwnerId) { Code = "Owner", Name = "Chủ shop", Scope = RoleScope.Shop, IsSystem = true, CreatedAt = Epoch, UpdatedAt = Epoch },
        new(ManagerId) { Code = "Manager", Name = "Quản lý", Scope = RoleScope.Shop, IsSystem = true, CreatedAt = Epoch, UpdatedAt = Epoch },
        new(StaffId) { Code = "Staff", Name = "Nhân viên", Scope = RoleScope.Shop, IsSystem = true, CreatedAt = Epoch, UpdatedAt = Epoch },
        new(AccountantId) { Code = "Accountant", Name = "Kế toán", Scope = RoleScope.Shop, IsSystem = true, CreatedAt = Epoch, UpdatedAt = Epoch },
        new(CustomerId) { Code = "Customer", Name = "Khách hàng", Scope = RoleScope.Shop, IsSystem = true, CreatedAt = Epoch, UpdatedAt = Epoch },
    ];
}
