using Microsoft.EntityFrameworkCore;
using Vsite.Domain.Media.Entities;
using Vsite.Domain.Shop.Entities;
using Vsite.Domain.Shop.Enums;
using Vsite.Infrastructure.Persistence;

namespace Vsite.IntegrationTests;

/// <summary>
/// Global Query Filter (Quyết định #21.2) trên `MediaAsset` — entity tenant-scoped của module Media
/// (task T4, MEDIA-001). Test bắt buộc theo backend/CLAUDE.md: "query từ shop A không thấy dữ liệu
/// shop B".
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class MediaTenantIsolationTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private readonly Guid _shopAId = Guid.NewGuid();
    private readonly Guid _shopBId = Guid.NewGuid();
    private Guid _assetAId;
    private Guid _assetBId;

    public MediaTenantIsolationTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        await using var db = CreateContext(new TestTenantContext());
        await db.Database.MigrateAsync();

        var shopA = new Shop(_shopAId, "Shop A", $"shop-a-{_shopAId:N}", ShopKind.Hosted);
        var shopB = new Shop(_shopBId, "Shop B", $"shop-b-{_shopBId:N}", ShopKind.Hosted);
        db.Shops.AddRange(shopA, shopB);
        await db.SaveChangesAsync();

        var assetA = MediaAsset.NewLibrary(_shopAId, $"shops/{_shopAId}/library/{Guid.NewGuid():N}.webp", 100, 100, 10, 0.5f, 0.5f, null, null);
        var assetB = MediaAsset.NewLibrary(_shopBId, $"shops/{_shopBId}/library/{Guid.NewGuid():N}.webp", 100, 100, 10, 0.5f, 0.5f, null, null);
        db.MediaAssets.AddRange(assetA, assetB);
        await db.SaveChangesAsync();

        _assetAId = assetA.Id;
        _assetBId = assetB.Id;
    }

    public async Task DisposeAsync()
    {
        await using var db = CreateContext(new TestTenantContext());
        db.MediaAssets.RemoveRange(db.MediaAssets.IgnoreQueryFilters().Where(m => m.Id == _assetAId || m.Id == _assetBId));
        db.Shops.RemoveRange(db.Shops.Where(s => s.Id == _shopAId || s.Id == _shopBId));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Query_scoped_to_shop_A_does_not_see_shop_B_asset()
    {
        await using var db = CreateContext(new TestTenantContext(_shopAId));

        var visible = await db.MediaAssets.Where(m => m.Id == _assetAId || m.Id == _assetBId).ToListAsync();

        Assert.Single(visible);
        Assert.Equal(_shopAId, visible[0].ShopId);
    }

    [Fact]
    public async Task Query_with_unresolved_tenant_sees_nothing_fail_closed()
    {
        await using var db = CreateContext(new TestTenantContext(shopId: null));

        var visible = await db.MediaAssets.Where(m => m.Id == _assetAId || m.Id == _assetBId).ToListAsync();

        Assert.Empty(visible);
    }

    private AppDbContext CreateContext(TestTenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_postgres.ConnectionString).Options;
        return new AppDbContext(options, tenantContext);
    }
}
