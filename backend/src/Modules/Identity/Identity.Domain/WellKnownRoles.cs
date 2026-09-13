namespace Identity.Domain;

/// <summary>
/// GUID cố định cho 7 role hệ thống (03 §3.5). Domain-level vì cả Application (gán RoleId lúc tạo
/// User/UserShop) lẫn Infrastructure (seed data qua migration, `RoleSeed`) đều cần — đặt ở
/// Infrastructure sẽ buộc Application phải reference ngược Infrastructure, sai chiều phụ thuộc.
/// </summary>
public static class WellKnownRoles
{
    public static readonly Guid PlatformUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid PlatformAdminId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static readonly Guid OwnerId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    public static readonly Guid ManagerId = Guid.Parse("00000000-0000-0000-0000-000000000004");
    public static readonly Guid StaffId = Guid.Parse("00000000-0000-0000-0000-000000000005");
    public static readonly Guid AccountantId = Guid.Parse("00000000-0000-0000-0000-000000000006");
    public static readonly Guid CustomerId = Guid.Parse("00000000-0000-0000-0000-000000000007");
}
