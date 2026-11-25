using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Vsite.Application.Website.Jobs;
using Vsite.Application.Website.Options;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Website.Entities;
using static Vsite.IntegrationTests.Website.WebsiteHandlerKit;

namespace Vsite.IntegrationTests.Website;

/// <summary>WEBSITE-008 T2 (#118, R1, R2, #116) — <c>PublicationPruner</c> + rollback về bản vừa bị xoá. Postgres thật (Docker).</summary>
[Collection(PostgresCollection.Name)]
public sealed class PublicationPrunerTests(PostgresFixture postgres) : IAsyncLifetime
{
    private string Cs => postgres.ConnectionString;

    public Task InitializeAsync() => MigrateAsync(Cs);

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Shop + website trắng + <paramref name="count"/> bản version 1..count chèn thẳng; current = bản <paramref name="currentVersion"/>.</summary>
    private async Task<(Guid ShopId, Guid WebsiteId, Dictionary<int, Guid> Ids)> SeedAsync(int count, int? currentVersion = null)
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var db = NewDb(Cs, seeded.ShopId);
        var ids = new Dictionary<int, Guid>();
        for (var v = 1; v <= count; v++)
        {
            var p = SitePublication.Create(seeded.ShopId, seeded.Website.Id, v, "{}", null, null, DateTimeOffset.UtcNow);
            db.SitePublications.Add(p);
            ids[v] = p.Id;
        }

        await db.SaveChangesAsync();
        if (count > 0)
        {
            var website = await db.Websites.SingleAsync();
            website.PointTo(await db.SitePublications.SingleAsync(p => p.Id == ids[currentVersion ?? count]));
            await db.SaveChangesAsync();
        }

        return (seeded.ShopId, seeded.Website.Id, ids);
    }

    private async Task<int> PruneAsync(Guid shopId, int keep = 20)
    {
        await using var db = NewDb(Cs, shopId);
        var pruner = new PublicationPruner(db, new TestTenantContext(shopId), Options.Create(new BuilderRetentionOptions { PublicationsToKeep = keep }));
        return await pruner.PruneAsync(default);
    }

    private async Task<int[]> VersionsAsync(Guid shopId)
    {
        await using var db = NewDb(Cs, shopId);
        return await db.SitePublications.AsNoTracking().OrderBy(p => p.Version).Select(p => p.Version).ToArrayAsync();
    }

    [Fact]
    public async Task Newest_is_current_keeps_versions_6_to_25()
    {
        var (shopId, _, _) = await SeedAsync(25);

        var deleted = await PruneAsync(shopId);

        Assert.Equal(5, deleted);
        Assert.Equal(Enumerable.Range(6, 20), await VersionsAsync(shopId));
    }

    [Fact]
    public async Task Rolled_back_to_version_1_keeps_exactly_20_including_current_R1()
    {
        var (shopId, _, _) = await SeedAsync(25, currentVersion: 1);

        var deleted = await PruneAsync(shopId);

        Assert.Equal(5, deleted);
        Assert.Equal(new[] { 1 }.Concat(Enumerable.Range(7, 19)), await VersionsAsync(shopId));
        await using var db = NewDb(Cs, shopId);
        var website = await db.Websites.AsNoTracking().SingleAsync();
        Assert.Equal(1, (await db.SitePublications.AsNoTracking().SingleAsync(p => p.Id == website.CurrentPublicationId)).Version);
    }

    [Fact]
    public async Task Twenty_or_fewer_publications_deletes_nothing()
    {
        var (shopId, _, _) = await SeedAsync(20);

        Assert.Equal(0, await PruneAsync(shopId));
        Assert.Equal(Enumerable.Range(1, 20), await VersionsAsync(shopId));
    }

    [Fact]
    public async Task No_current_publication_keeps_the_newest_20()
    {
        var (shopId, websiteId, _) = await SeedAsync(0);
        await using (var db = NewDb(Cs, shopId))
        {
            for (var v = 1; v <= 22; v++)
            {
                db.SitePublications.Add(SitePublication.Create(shopId, websiteId, v, "{}", null, null, DateTimeOffset.UtcNow));
            }

            await db.SaveChangesAsync();
        }

        Assert.Equal(2, await PruneAsync(shopId));
        Assert.Equal(Enumerable.Range(3, 20), await VersionsAsync(shopId));
    }

    [Fact]
    public async Task Shop_without_website_is_a_no_op()
    {
        var shopId = await AddShopAsync(Cs);

        Assert.Equal(0, await PruneAsync(shopId));
    }

    [Fact]
    public async Task Pruning_shop_A_leaves_shop_B_untouched_116()
    {
        var (shopA, _, _) = await SeedAsync(25);
        var (shopB, _, _) = await SeedAsync(25);

        await PruneAsync(shopA);

        Assert.Equal(20, (await VersionsAsync(shopA)).Length);
        Assert.Equal(Enumerable.Range(1, 25), await VersionsAsync(shopB));
    }

    [Fact]
    public async Task Publish_after_prune_continues_from_max_plus_one()
    {
        var (shopId, _, _) = await SeedAsync(25);
        await PruneAsync(shopId);

        await using var request = new Request(Cs, shopId);
        var dto = await request.Publish();

        Assert.Equal(26, dto.Version);
    }

    [Fact]
    public async Task Current_is_v3_of_25_keeps_3_and_7_to_25()
    {
        var (shopId, _, _) = await SeedAsync(25, currentVersion: 3);

        Assert.Equal(5, await PruneAsync(shopId));
        Assert.Equal(new[] { 3 }.Concat(Enumerable.Range(7, 19)), await VersionsAsync(shopId));
    }

    // ---- R2: rollback tới bản bị xoá cứng sau khi handler đã nạp ----

    /// <summary>Xoá cứng một bản từ kết nối khác ngay TRƯỚC SaveChanges của handler (mô phỏng job prune chen giữa).</summary>
    private sealed class HardDeletePublicationInterceptor(string connectionString, Guid publicationId) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            await using var other = NewDb(connectionString, null);
            await other.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM site_publication WHERE id = {publicationId}", cancellationToken);
            return result;
        }
    }

    [Fact]
    public async Task Rollback_to_publication_hard_deleted_after_load_is_404_not_500_R2()
    {
        var (shopId, _, ids) = await SeedAsync(3);

        await using var racing = new Request(Cs, shopId, null, new HardDeletePublicationInterceptor(Cs, ids[1]));
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => racing.Rollback(ids[1]));

        Assert.Equal("PUBLICATION_NOT_FOUND", ex.ErrorCode);
        Assert.Empty(racing.PointerCache.Invalidated);
    }
}
