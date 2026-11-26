using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vsite.Application.Website.Jobs;
using Vsite.Application.Website.Options;
using Vsite.Domain.Website.Entities;
using static Vsite.IntegrationTests.Website.WebsiteHandlerKit;

namespace Vsite.IntegrationTests.Website;

/// <summary>WEBSITE-008 T3 (#118, #116) — <c>DeletedPagePurger</c>. Postgres thật (Docker).</summary>
[Collection(PostgresCollection.Name)]
public sealed class DeletedPagePurgerTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 3, 30, 0, TimeSpan.Zero);

    private static TimeSpan Days(double d) => TimeSpan.FromDays(d);

    private string Cs => postgres.ConnectionString;

    public Task InitializeAsync() => MigrateAsync(Cs);

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Shop + website + một trang Composable có draft và ảnh OG; <paramref name="deletedDaysAgo"/> null = còn sống.</summary>
    private async Task<(Guid ShopId, Guid PageId)> SeedAsync(TimeSpan? deletedAgo, bool withDraft = true)
    {
        var seeded = await AddWebsiteAsync(Cs);
        var ogId = await AddAssetAsync(Cs, seeded.ShopId, "Library");
        await using var db = NewDb(Cs, seeded.ShopId);
        var page = Page.NewComposable(seeded.ShopId, seeded.Website.Id, $"p-{Guid.NewGuid():N}"[..12], "Trang", 99);
        page.SetOgImage(ogId);
        db.Pages.Add(page);
        if (withDraft)
        {
            db.PageDrafts.Add(PageDraft.Create(seeded.ShopId, page.Id, "{}"));
        }

        await db.SaveChangesAsync();

        if (deletedAgo is { } ago)
        {
            page.SoftDelete();
            await db.SaveChangesAsync();
            var at = Now - ago;
            await db.Pages.IgnoreQueryFilters().Where(p => p.Id == page.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.DeletedAt, at));
        }

        return (seeded.ShopId, page.Id);
    }

    private async Task<int> PurgeAsync(Guid shopId)
    {
        await using var db = NewDb(Cs, shopId);
        var purger = new DeletedPagePurger(db, new TestTenantContext(shopId), Options.Create(new BuilderRetentionOptions()), new FixedTime(Now));
        return await purger.PurgeAsync(default);
    }

    private async Task<(bool DraftExists, Guid? OgImageId)> StateAsync(Guid shopId, Guid pageId)
    {
        await using var db = NewDb(Cs, shopId);
        var draft = await db.PageDrafts.IgnoreQueryFilters().AnyAsync(d => d.Id == pageId);
        var og = await db.Pages.IgnoreQueryFilters().Where(p => p.Id == pageId).Select(p => p.OgImageId).SingleAsync();
        return (draft, og);
    }

    [Fact]
    public async Task Deleted_8_days_ago_drops_draft_and_og_image()
    {
        var (shopId, pageId) = await SeedAsync(Days(8));

        Assert.Equal(1, await PurgeAsync(shopId));

        var (draft, og) = await StateAsync(shopId, pageId);
        Assert.False(draft);
        Assert.Null(og);
    }

    [Fact]
    public async Task Deleted_6_days_ago_is_untouched()
    {
        var (shopId, pageId) = await SeedAsync(Days(6));

        Assert.Equal(0, await PurgeAsync(shopId));

        var (draft, og) = await StateAsync(shopId, pageId);
        Assert.True(draft);
        Assert.NotNull(og);
    }

    [Fact]
    public async Task Live_page_is_untouched()
    {
        var (shopId, pageId) = await SeedAsync(null);

        Assert.Equal(0, await PurgeAsync(shopId));

        var (draft, og) = await StateAsync(shopId, pageId);
        Assert.True(draft);
        Assert.NotNull(og);
    }

    [Fact]
    public async Task Other_shop_deleted_page_is_untouched_116()
    {
        var (shopA, pageA) = await SeedAsync(Days(8));
        var (shopB, pageB) = await SeedAsync(Days(8));

        Assert.Equal(1, await PurgeAsync(shopA));

        var (draftA, ogA) = await StateAsync(shopA, pageA);
        Assert.False(draftA);
        Assert.Null(ogA);
        var (draftB, ogB) = await StateAsync(shopB, pageB);
        Assert.True(draftB);
        Assert.NotNull(ogB);
    }

    [Fact]
    public async Task Deleted_page_without_draft_still_gets_og_image_cleared()
    {
        var (shopId, pageId) = await SeedAsync(Days(8), withDraft: false);

        Assert.Equal(0, await PurgeAsync(shopId));

        var (draft, og) = await StateAsync(shopId, pageId);
        Assert.False(draft);
        Assert.Null(og);
    }

    [Fact]
    public async Task Boundary_one_minute_past_grace_is_purged()
    {
        var (shopId, pageId) = await SeedAsync(Days(7) + TimeSpan.FromMinutes(1));

        Assert.Equal(1, await PurgeAsync(shopId));

        var (draft, og) = await StateAsync(shopId, pageId);
        Assert.False(draft);
        Assert.Null(og);
    }

    [Fact]
    public async Task Boundary_one_minute_before_grace_is_kept()
    {
        var (shopId, pageId) = await SeedAsync(Days(7) - TimeSpan.FromMinutes(1));

        Assert.Equal(0, await PurgeAsync(shopId));

        var (draft, og) = await StateAsync(shopId, pageId);
        Assert.True(draft);
        Assert.NotNull(og);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
