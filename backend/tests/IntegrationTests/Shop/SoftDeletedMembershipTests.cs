using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Shop.Commands.UpdateShop;
using Vsite.Application.Shop.Interfaces;
using Vsite.Application.Shop.Queries.ListShops;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Identity;
using Vsite.Domain.Identity.Entities;
using Vsite.Domain.Identity.Enums;
using Vsite.Domain.Shop.Enums;
using Vsite.Infrastructure.Identity;
using Vsite.Infrastructure.Persistence;
using Vsite.Infrastructure.Shop;
using ShopEntity = Vsite.Domain.Shop.Entities.Shop;

namespace Vsite.IntegrationTests;

/// <summary>MEDIA-001 final fix (C2, #21) — mọi truy vấn `UserShops.IgnoreQueryFilters()` phải tự lọc
/// `!IsDeleted` (IgnoreQueryFilters tắt cả bộ lọc soft-delete). Một membership Owner đã bị xoá mềm KHÔNG
/// được tính là Owner/member. EF InMemory, không cần Docker.</summary>
public sealed class SoftDeletedMembershipTests
{
    private readonly FakeTenant _tenant = new();

    [Fact]
    public async Task ShopOwnershipService_live_owner_is_owner_but_soft_deleted_owner_is_not()
    {
        await using var db = CreateDb();
        var (shop, live, dead) = await SeedAsync(db);

        var sut = new ShopOwnershipService(db);

        Assert.True(await sut.IsOwnerAsync(live, shop.Id, default));
        Assert.False(await sut.IsOwnerAsync(dead, shop.Id, default));
    }

    [Fact]
    public async Task UserShopMembershipService_soft_deleted_membership_is_not_active_member()
    {
        await using var db = CreateDb();
        var (shop, live, dead) = await SeedAsync(db);

        var sut = new UserShopMembershipService(db);

        Assert.True(await sut.IsActiveMemberAsync(live, shop.Id, default));
        Assert.False(await sut.IsActiveMemberAsync(dead, shop.Id, default));
    }

    [Fact]
    public async Task ListShops_does_not_list_shop_of_soft_deleted_membership()
    {
        await using var db = CreateDb();
        var (shop, live, dead) = await SeedAsync(db);

        var liveList = await new ListShopsHandler(db, new FakeUser(live), new EmptyReader()).Handle(new ListShopsQuery(), default);
        var deadList = await new ListShopsHandler(db, new FakeUser(dead), new EmptyReader()).Handle(new ListShopsQuery(), default);

        Assert.Equal(shop.Id, Assert.Single(liveList).Id);
        Assert.Empty(deadList);
    }

    [Fact]
    public async Task UpdateShop_by_soft_deleted_owner_is_forbidden()
    {
        await using var db = CreateDb();
        var (shop, live, dead) = await SeedAsync(db);
        var command = new UpdateShopCommand(shop.Id, "Renamed", shop.Slug, ShopKind.Hosted, null, ShopStatus.Active);

        var ex = await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new UpdateShopHandler(db, new FakeUser(dead), new NoopLookup(), new EmptyReader()).Handle(command, default));
        Assert.Equal("SHOP_ACCESS_DENIED", ex.ErrorCode);

        var ok = await new UpdateShopHandler(db, new FakeUser(live), new NoopLookup(), new EmptyReader()).Handle(command, default);
        Assert.Equal("Renamed", ok.Name);
    }

    /// <summary>Một shop, một Owner sống, một Owner có membership đã xoá mềm (Active + Owner + IsDeleted).</summary>
    private static async Task<(ShopEntity Shop, Guid Live, Guid Dead)> SeedAsync(AppDbContext db)
    {
        var shop = new ShopEntity(Guid.NewGuid()) { Name = "Shop", Slug = $"shop-{Guid.NewGuid():N}", Kind = ShopKind.Hosted };
        db.Shops.Add(shop);
        if (!await db.Roles.AnyAsync(r => r.Id == WellKnownRoles.OwnerId))
        {
            db.Roles.Add(new Role(WellKnownRoles.OwnerId) { Code = "Owner", Name = "Owner", Scope = RoleScope.Shop });
        }

        var live = Guid.NewGuid();
        var dead = Guid.NewGuid();
        db.UserShops.Add(new UserShop { UserId = live, ShopId = shop.Id, RoleId = WellKnownRoles.OwnerId, Source = UserShopSource.ShopCreator });
        db.UserShops.Add(new UserShop { UserId = dead, ShopId = shop.Id, RoleId = WellKnownRoles.OwnerId, Source = UserShopSource.ShopCreator, IsDeleted = true });
        await db.SaveChangesAsync();
        return (shop, live, dead);
    }

    private AppDbContext CreateDb() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options,
        _tenant);

    private sealed class EmptyReader : IShopLogoReader
    {
        public Task<string?> GetLogoUrlAsync(Guid shopId, Guid? logoId, CancellationToken ct) => Task.FromResult<string?>(null);

        public Task<IReadOnlyDictionary<Guid, string>> GetLogoUrlsAsync(IReadOnlyCollection<Guid> shopIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
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

    private sealed class FakeTenant : ITenantContext
    {
        public TenantAudienceKind AudienceKind => TenantAudienceKind.Portal;
        public Guid? ShopId { get; set; }
    }
}
