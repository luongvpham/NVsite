using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Shop.Commands.CreateShop;
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
using ShopEntity = Vsite.Domain.Shop.Entities.Shop;

namespace Vsite.IntegrationTests;

/// <summary>MEDIA-001 final fix (C2, #21) — mọi truy vấn `UserShops.IgnoreQueryFilters()` phải tự lọc
/// `!IsDeleted` (IgnoreQueryFilters tắt cả bộ lọc soft-delete). Một membership Owner đã bị xoá mềm KHÔNG
/// được tính là Owner/member. EF InMemory, không cần Docker.</summary>
public sealed class SoftDeletedMembershipTests
{
    private readonly FakeTenant _tenant = new();

    [Fact]
    public async Task UserShopMembershipService_role_lookup_ignores_soft_deleted_owner()
    {
        // REFACTOR-BE-001: kiểm Owner chuyển từ handler/ShopOwnershipService về
        // ShopMembershipEndpointFilter, đọc role qua FindActiveRoleIdAsync.
        await using var db = CreateDb();
        var (shop, live, dead) = await SeedAsync(db);

        var sut = new UserShopMembershipService(db);

        Assert.Equal(WellKnownRoles.OwnerId, await sut.FindActiveRoleIdAsync(live, shop.Id, default));
        Assert.Null(await sut.FindActiveRoleIdAsync(dead, shop.Id, default));
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
    public async Task ListShops_does_not_list_soft_deleted_shop_or_soft_deleted_role_but_lists_control_shop()
    {
        await using var db = CreateDb();
        var (controlShop, user, _) = await SeedAsync(db);

        // Membership còn sống (Active, !IsDeleted) nhưng bản thân Shop đã bị xoá mềm.
        var deletedShop = new ShopEntity(Guid.NewGuid(), "Gone", $"gone-{Guid.NewGuid():N}", ShopKind.Hosted) { IsDeleted = true };
        db.Shops.Add(deletedShop);
        db.UserShops.Add(new UserShop { UserId = user, ShopId = deletedShop.Id, RoleId = WellKnownRoles.OwnerId, Source = UserShopSource.ShopCreator });

        // Membership còn sống trỏ tới một Role đã bị xoá mềm.
        var deletedRoleId = Guid.NewGuid();
        db.Roles.Add(new Role(deletedRoleId) { Code = "Ghost", Name = "Ghost", Scope = RoleScope.Shop, IsDeleted = true });
        var ghostShop = new ShopEntity(Guid.NewGuid(), "Ghost", $"ghost-{Guid.NewGuid():N}", ShopKind.Hosted);
        db.Shops.Add(ghostShop);
        db.UserShops.Add(new UserShop { UserId = user, ShopId = ghostShop.Id, RoleId = deletedRoleId, Source = UserShopSource.ShopCreator });
        await db.SaveChangesAsync();

        var list = await new ListShopsHandler(db, new FakeUser(user), new EmptyReader()).Handle(new ListShopsQuery(), default);

        Assert.Equal(controlShop.Id, Assert.Single(list).Id);
    }

    /// <summary>REFACTOR-DB-001 — unique index `shop.slug` không lọc soft delete, nên slug của shop đã
    /// xoá mềm vẫn bị giữ. Kiểm trước phải thấy nó để trả 409 thay vì để DB ném thành 500.</summary>
    [Fact]
    public async Task CreateShop_with_slug_of_soft_deleted_shop_is_conflict()
    {
        await using var db = CreateDb();
        var slug = $"gone-{Guid.NewGuid():N}";
        db.Shops.Add(new ShopEntity(Guid.NewGuid(), "Gone", slug, ShopKind.Hosted) { IsDeleted = true });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            new CreateShopHandler(db, new FakeUser(Guid.NewGuid()), new EmptyReader())
                .Handle(new CreateShopCommand("New", slug, ShopKind.Hosted, null), default));

        Assert.Equal("SHOP_SLUG_ALREADY_TAKEN", ex.ErrorCode);
    }

    [Fact]
    public async Task UpdateShop_to_slug_of_soft_deleted_shop_is_conflict()
    {
        await using var db = CreateDb();
        var (shop, _, _) = await SeedAsync(db);
        var slug = $"gone-{Guid.NewGuid():N}";
        db.Shops.Add(new ShopEntity(Guid.NewGuid(), "Gone", slug, ShopKind.Hosted) { IsDeleted = true });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateShopHandler(db, new NoopLookup(), new EmptyReader())
                .Handle(new UpdateShopCommand(shop.Id, "Shop", slug, ShopKind.Hosted, null, ShopStatus.Draft), default));

        Assert.Equal("SHOP_SLUG_ALREADY_TAKEN", ex.ErrorCode);
    }

    private sealed class NoopLookup : IShopLookupService
    {
        public Task<Guid?> FindShopIdBySlugAsync(string slug, CancellationToken cancellationToken) => Task.FromResult<Guid?>(null);
        public Task InvalidateAsync(string slug, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>Một shop, một Owner sống, một Owner có membership đã xoá mềm (Active + Owner + IsDeleted).</summary>
    private static async Task<(ShopEntity Shop, Guid Live, Guid Dead)> SeedAsync(AppDbContext db)
    {
        var shop = new ShopEntity(Guid.NewGuid(), "Shop", $"shop-{Guid.NewGuid():N}", ShopKind.Hosted);
        db.Shops.Add(shop);
        if (!await db.Roles.AnyAsync(r => r.Id == WellKnownRoles.OwnerId))
        {
            db.Roles.Add(new Role(WellKnownRoles.OwnerId) { Code = "Owner", Name = "Owner", Scope = RoleScope.Shop });
        }

        var live = Guid.NewGuid();
        var dead = Guid.NewGuid();

        // UserShopMembershipService kiểm cả User còn Active (#90) — InMemory không có FK nên phải seed User.
        foreach (var userId in new[] { live, dead })
        {
            db.Users.Add(new User(userId) { PrimaryIdentityKind = PrimaryIdentityKind.Email, RoleId = WellKnownRoles.PlatformUserId });
        }
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


    private sealed class FakeTenant : ITenantContext
    {
        public TenantAudienceKind AudienceKind => TenantAudienceKind.Portal;
        public Guid? ShopId { get; set; }
    }
}
