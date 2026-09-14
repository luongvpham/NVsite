using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vsite.Application.Identity.Interfaces;
using Vsite.Domain.Identity.Entities;
using Vsite.Domain.Identity.Enums;
using Vsite.Infrastructure.Persistence;

namespace Vsite.IntegrationTests;

/// <summary>
/// Phần verify cache Redis của DOCKER-TEST-DEBT mục 1 (bước 9) — tách khỏi
/// <see cref="TenantResolutionTests"/> vào collection + <see cref="IdentityApiFactory"/> RIÊNG vì
/// test này tắt hẳn container Postgres giữa chừng; dùng chung factory với test khác sẽ làm connection
/// pool của các test đó dính request ngay sau lúc restart.
/// </summary>
[Collection(ShopLookupCacheCollection.Name)]
public sealed class ShopLookupCacheTests
{
    private readonly IdentityApiFactory _factory;

    public ShopLookupCacheTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Slug_resolution_is_served_from_redis_cache_when_postgres_is_unreachable()
    {
        // ShopLookupService cache found-slug 30 phút (FoundOptions) — verify THẬT SỰ phục vụ từ
        // Redis (không chỉ tình cờ đúng) bằng cách tắt hẳn Postgres sau khi cache đã ấm.
        var (shopId, slug) = await CreateShopAsync();

        Assert.Equal(shopId, await ResolveViaShopLookupServiceAsync(slug));

        await _factory.StopPostgresAsync();
        try
        {
            Assert.Equal(shopId, await ResolveViaShopLookupServiceAsync(slug));
        }
        finally
        {
            await _factory.StartPostgresAsync();
        }
    }

    private async Task<Guid?> ResolveViaShopLookupServiceAsync(string slug)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var shopLookup = scope.ServiceProvider.GetRequiredService<IShopLookupService>();
        return await shopLookup.FindShopIdBySlugAsync(slug, CancellationToken.None);
    }

    private async Task<(Guid ShopId, string Slug)> CreateShopAsync()
    {
        var shopId = Guid.NewGuid();
        var slug = $"shoplookup-{shopId:N}";

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Shops.Add(new Shop(shopId) { Name = "Shop Lookup Cache Test Shop", Slug = slug, Kind = ShopKind.Hosted, Status = ShopStatus.Active });
        await db.SaveChangesAsync();

        return (shopId, slug);
    }
}

[CollectionDefinition(Name)]
public sealed class ShopLookupCacheCollection : ICollectionFixture<IdentityApiFactory>
{
    public const string Name = "ShopLookupCache";
}
