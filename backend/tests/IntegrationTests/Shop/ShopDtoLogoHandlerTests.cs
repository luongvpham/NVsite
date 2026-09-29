using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Shop.Commands.CreateShop;
using Vsite.Application.Shop.Commands.UpdateShop;
using Vsite.Application.Shop.Interfaces;
using Vsite.Application.Shop.Queries.GetShop;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Identity;
using Vsite.Domain.Identity.Entities;
using Vsite.Domain.Identity.Enums;
using Vsite.Domain.Shop.Enums;
using Vsite.Infrastructure.Persistence;
using ShopEntity = Vsite.Domain.Shop.Entities.Shop;

namespace Vsite.IntegrationTests;

/// <summary>MEDIA-001 D4 (#88) — mọi handler trả `ShopDto` gọi port `IShopLogoReader` và đưa kết quả vào
/// `LogoStorageKey`. Handler-level (EF InMemory + port giả), KHÔNG cần Docker.</summary>
public sealed class ShopDtoLogoHandlerTests
{
    private const string Key = "shops/x/logo-320.webp";

    private readonly FakeTenantContext _tenant = new();

    [Fact]
    public async Task GetShop_puts_port_value_in_ShopDto_and_passes_shop_and_logo_ids()
    {
        await using var db = CreateDb();
        var logoId = Guid.NewGuid();
        var shop = await SeedShopAsync(db, logoId);
        var reader = new FakeReader(Key);

        var dto = await new GetShopHandler(db, reader).Handle(new GetShopQuery(shop.Id), CancellationToken.None);

        Assert.Equal(Key, dto.LogoStorageKey);
        Assert.Equal(logoId, dto.LogoId);
        Assert.Equal((shop.Id, (Guid?)logoId), reader.Calls.Single());
    }

    [Fact]
    public async Task GetShop_without_logo_returns_null_key()
    {
        await using var db = CreateDb();
        var shop = await SeedShopAsync(db, null);

        var dto = await new GetShopHandler(db, new FakeReader(null)).Handle(new GetShopQuery(shop.Id), CancellationToken.None);

        Assert.Null(dto.LogoStorageKey);
        Assert.Null(dto.LogoId);
    }

    [Fact]
    public async Task UpdateShop_puts_port_value_in_ShopDto()
    {
        await using var db = CreateDb();
        var logoId = Guid.NewGuid();
        var shop = await SeedShopAsync(db, logoId);
        var userId = Guid.NewGuid();
        db.UserShops.Add(new UserShop
        {
            UserId = userId,
            ShopId = shop.Id,
            RoleId = WellKnownRoles.OwnerId,
            Source = UserShopSource.ShopCreator,
        });
        await db.SaveChangesAsync();
        var reader = new FakeReader(Key);

        var dto = await new UpdateShopHandler(db, new FakeUser(userId), new NoopLookup(), reader).Handle(
            new UpdateShopCommand(shop.Id, "Renamed", shop.Slug, ShopKind.Hosted, null, ShopStatus.Active),
            CancellationToken.None);

        Assert.Equal(Key, dto.LogoStorageKey);
        Assert.Equal("Renamed", dto.Name);
        Assert.Equal((shop.Id, (Guid?)logoId), reader.Calls.Single());
    }

    [Fact]
    public async Task CreateShop_calls_port_and_returns_its_value()
    {
        await using var db = CreateDb();
        var reader = new FakeReader(null);

        var dto = await new CreateShopHandler(db, new FakeUser(Guid.NewGuid()), reader).Handle(
            new CreateShopCommand("New", "new-shop", ShopKind.Hosted, null), CancellationToken.None);

        Assert.Null(dto.LogoStorageKey);
        Assert.Equal((dto.Id, (Guid?)null), reader.Calls.Single());
    }

    private static async Task<ShopEntity> SeedShopAsync(AppDbContext db, Guid? logoId)
    {
        var shop = new ShopEntity(Guid.NewGuid()) { Name = "Shop", Slug = $"shop-{Guid.NewGuid():N}", Kind = ShopKind.Hosted };
        if (logoId is { } id)
        {
            shop.LogoId = id;
        }

        db.Shops.Add(shop);
        await db.SaveChangesAsync();
        return shop;
    }

    private AppDbContext CreateDb() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options,
        _tenant);

    private sealed class FakeReader(string? value) : IShopLogoReader
    {
        public List<(Guid ShopId, Guid? LogoId)> Calls { get; } = [];

        public Task<string?> GetLogoStorageKeyAsync(Guid shopId, Guid? logoId, CancellationToken ct)
        {
            Calls.Add((shopId, logoId));
            return Task.FromResult(value);
        }
    }

    private sealed class FakeUser(Guid userId) : ICurrentUserContext
    {
        public Guid UserId => userId;
        public string Audience => "vsite-portal";
    }

    private sealed class NoopLookup : IShopLookupService
    {
        public Task<Guid?> FindShopIdBySlugAsync(string slug, CancellationToken cancellationToken) => Task.FromResult<Guid?>(null);
        public Task InvalidateAsync(string slug, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeTenantContext : ITenantContext
    {
        public TenantAudienceKind AudienceKind => TenantAudienceKind.Portal;
        public Guid? ShopId { get; set; }
    }
}
