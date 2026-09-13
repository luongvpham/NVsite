using Identity.Domain;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.IntegrationTests;

/// <summary>
/// Global Query Filter (Quyết định #21.2) trên `UserShop` — entity tenant-scoped duy nhất ở Bước 3.
/// Test bắt buộc theo backend/CLAUDE.md: "query từ shop A không thấy dữ liệu shop B".
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class TenantIsolationTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private readonly Guid _shopAId = Guid.NewGuid();
    private readonly Guid _shopBId = Guid.NewGuid();
    private readonly Guid _userAId = Guid.NewGuid();
    private readonly Guid _userBId = Guid.NewGuid();

    public TenantIsolationTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        await using var db = CreateContext(new TestTenantContext());
        await db.Database.MigrateAsync();

        db.Shops.AddRange(
            new Shop(_shopAId) { Name = "Shop A", Slug = $"shop-a-{_shopAId:N}", Kind = ShopKind.Hosted },
            new Shop(_shopBId) { Name = "Shop B", Slug = $"shop-b-{_shopBId:N}", Kind = ShopKind.Hosted }
        );
        db.Users.AddRange(
            NewUser(_userAId, "user-a"),
            NewUser(_userBId, "user-b")
        );
        await db.SaveChangesAsync();

        db.UserShops.AddRange(
            new UserShop { UserId = _userAId, ShopId = _shopAId, RoleId = WellKnownRoles.CustomerId, Source = UserShopSource.RegisteredOnShop },
            new UserShop { UserId = _userBId, ShopId = _shopBId, RoleId = WellKnownRoles.CustomerId, Source = UserShopSource.RegisteredOnShop }
        );
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = CreateContext(new TestTenantContext());
        // Dọn dữ liệu test — không xoá schema, container dùng chung cho cả collection. Lưu ý:
        // AppDbContextBase chặn Delete thành soft-delete (IsDeleted=true) cho entity Auditable, nên
        // đây không xoá hẳn khỏi DB — chỉ ẩn qua Global Query Filter. Đủ dùng vì container bị huỷ
        // hoàn toàn khi hết vòng đời collection fixture (PostgresFixture.DisposeAsync).
        db.UserShops.RemoveRange(db.UserShops.IgnoreQueryFilters().Where(us => us.ShopId == _shopAId || us.ShopId == _shopBId));
        db.Users.RemoveRange(db.Users.Where(u => u.Id == _userAId || u.Id == _userBId));
        db.Shops.RemoveRange(db.Shops.Where(s => s.Id == _shopAId || s.Id == _shopBId));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Query_scoped_to_shop_A_does_not_see_shop_B_data()
    {
        await using var db = CreateContext(new TestTenantContext(_shopAId));

        var visible = await db.UserShops.Where(us => us.UserId == _userAId || us.UserId == _userBId).ToListAsync();

        Assert.Single(visible);
        Assert.Equal(_shopAId, visible[0].ShopId);
    }

    [Fact]
    public async Task Query_with_unresolved_tenant_sees_nothing_fail_closed()
    {
        await using var db = CreateContext(new TestTenantContext(shopId: null));

        var visible = await db.UserShops.Where(us => us.UserId == _userAId || us.UserId == _userBId).ToListAsync();

        Assert.Empty(visible);
    }

    private IdentityDbContext CreateContext(TestTenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(_postgres.ConnectionString).Options;
        return new IdentityDbContext(options, tenantContext);
    }

    private static User NewUser(Guid id, string emailLocalPart) => new(id)
    {
        Email = $"{emailLocalPart}-{id:N}@example.test",
        EmailNormalized = $"{emailLocalPart}-{id:N}@EXAMPLE.TEST".ToUpperInvariant(),
        EmailVerifiedAt = Now,
        PrimaryIdentityKind = PrimaryIdentityKind.Email,
        RoleId = WellKnownRoles.PlatformUserId,
    };

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
}
