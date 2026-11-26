using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vsite.Application.Website.Jobs;
using Vsite.Application.Website.Navigation;
using Vsite.Application.Website.Options;
using Vsite.Application.Website.Versioning;
using Vsite.Domain.Website.Entities;
using static Vsite.IntegrationTests.Website.WebsiteHandlerKit;

namespace Vsite.IntegrationTests.Website;

/// <summary>WEBSITE-008 T4 (#118, #116, R3) — <c>NavigationOrphanCleaner</c>. Postgres thật (Docker).</summary>
[Collection(PostgresCollection.Name)]
public sealed class NavigationOrphanCleanerTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 3, 30, 0, TimeSpan.Zero);
    private static readonly DocumentVersioning Versioning = new([]);

    private string Cs => postgres.ConnectionString;

    public Task InitializeAsync() => MigrateAsync(Cs);

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Menu: [trang Home sống, trang đã xoá (kèm con custom), custom]. <c>IsDirty</c> = false. Mốc sửa cuối = Now − <paramref name="quiet"/>.</summary>
    private async Task<(Guid ShopId, Guid HomeId)> SeedAsync(TimeSpan quiet)
    {
        var seeded = await AddWebsiteAsync(Cs);
        var homeId = seeded.Home.Id;
        var gone = Guid.NewGuid();
        NavItem[] items =
        [
            new(NavItem.PageKey(homeId), null, null, false, []),
            new(NavItem.PageKey(gone), null, null, false, [new NavItem("custom", "c", "https://c.vn", false, [])]),
            new("custom", "y", "https://y.vn", true, []),
        ];

        await using var db = NewDb(Cs, seeded.ShopId);
        var nav = NavigationConfig.Create(seeded.Website.Id, seeded.ShopId, NavigationItems.Serialize(items, Versioning));
        nav.MarkPublished();
        db.NavigationConfigs.Add(nav);
        await db.SaveChangesAsync();

        // Interceptor audit đã đóng dấu UpdatedAt = giờ thật; đặt lại mốc tường minh (ExecuteUpdate bỏ qua interceptor).
        var at = Now - quiet;
        await db.NavigationConfigs.IgnoreQueryFilters().Where(n => n.Id == nav.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.CreatedAt, at).SetProperty(n => n.UpdatedAt, at));
        return (seeded.ShopId, homeId);
    }

    private async Task<int> CleanAsync(Guid shopId)
    {
        await using var db = NewDb(Cs, shopId);
        var job = new NavigationOrphanCleaner(
            db, new TestTenantContext(shopId), Options.Create(new BuilderRetentionOptions()), new FixedTime(Now), Versioning,
            NullLogger<NavigationOrphanCleaner>.Instance);
        return await job.CleanAsync(default);
    }

    private async Task<(string[] Keys, int Total, bool IsDirty, uint Version)> StateAsync(Guid shopId)
    {
        await using var db = NewDb(Cs, shopId);
        var nav = await db.NavigationConfigs.AsNoTracking().SingleAsync();
        var items = NavigationItems.Parse(nav.Items, Versioning);
        return (items.Select(i => i.Key).ToArray(), NavigationItems.CountAll(items), nav.IsDirty, nav.Version);
    }

    [Fact]
    public async Task Quiet_25h_drops_orphan_with_children_and_keeps_IsDirty()
    {
        var (shopId, homeId) = await SeedAsync(TimeSpan.FromHours(25));

        Assert.Equal(1, await CleanAsync(shopId));

        var s = await StateAsync(shopId);
        Assert.Equal([NavItem.PageKey(homeId), "custom"], s.Keys);
        Assert.Equal(2, s.Total);
        Assert.False(s.IsDirty);
    }

    [Fact]
    public async Task Edited_1h_ago_is_untouched_version_unchanged_R3()
    {
        var (shopId, _) = await SeedAsync(TimeSpan.FromHours(1));
        var before = await StateAsync(shopId);

        Assert.Equal(0, await CleanAsync(shopId));

        var after = await StateAsync(shopId);
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(before.Total, after.Total);
    }

    [Fact]
    public async Task No_orphans_means_no_write_version_unchanged()
    {
        var (shopId, _) = await SeedAsync(TimeSpan.FromHours(25));
        Assert.Equal(1, await CleanAsync(shopId));
        var before = await StateAsync(shopId);

        Assert.Equal(0, await CleanAsync(shopId));

        Assert.Equal(before.Version, (await StateAsync(shopId)).Version);
    }

    [Fact]
    public async Task Other_shop_menu_is_untouched_116()
    {
        var (shopA, _) = await SeedAsync(TimeSpan.FromHours(25));
        var (shopB, _) = await SeedAsync(TimeSpan.FromHours(25));
        var beforeB = await StateAsync(shopB);

        Assert.Equal(1, await CleanAsync(shopA));

        var afterB = await StateAsync(shopB);
        Assert.Equal(beforeB.Version, afterB.Version);
        Assert.Equal(4, afterB.Total);
    }

    [Fact]
    public async Task Concurrent_edit_during_run_skips_without_throwing()
    {
        var (shopId, _) = await SeedAsync(TimeSpan.FromHours(25));
        await using var db = NewDb(Cs, shopId);
        var job = new NavigationOrphanCleaner(
            db, new TestTenantContext(shopId), Options.Create(new BuilderRetentionOptions()), new FixedTime(Now), Versioning,
            NullLogger<NavigationOrphanCleaner>.Instance);
        // Chủ shop sửa menu (đổi xmin) sau khi job đã nạp: mô phỏng bằng một UPDATE ngay trước SaveChanges của job.
        var nav = await db.NavigationConfigs.SingleAsync();
        await using (var other = NewDb(Cs, shopId))
        {
            var theirs = await other.NavigationConfigs.SingleAsync();
            theirs.Replace(theirs.Items);
            await other.SaveChangesAsync();
        }

        // Job nạp lại thực thể đã track (xmin cũ) ⇒ UPDATE lệch ⇒ bỏ lượt.
        Assert.Equal(0, await job.CleanAsync(default));
        Assert.NotNull(nav);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
