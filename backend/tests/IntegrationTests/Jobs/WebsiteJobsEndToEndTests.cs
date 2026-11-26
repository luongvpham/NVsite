using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vsite.Application.Common.Jobs;
using Vsite.Application.Media.Jobs;
using Vsite.Application.Website.Jobs;
using Vsite.Application.Website.Navigation;
using Vsite.Application.Website.Versioning;
using Vsite.Domain.Media.Entities;
using Vsite.Domain.Website.Entities;
using Vsite.Infrastructure.Jobs;
using static Vsite.IntegrationTests.Website.WebsiteHandlerKit;

namespace Vsite.IntegrationTests.Jobs;

/// <summary>
/// WEBSITE-008 T8 (#116, #117, #118) — hai job mới chạy qua <see cref="ShopJobRunner"/> lấy từ DI của host thật (không dựng tay):
/// chứng minh đăng ký DI đủ (job, bước, options, nguồn ảnh) và chạy shop A không đổi một dòng nào của shop B.
/// Host dùng <c>TimeProvider.System</c> ⇒ mốc thời gian seed tính từ giờ thật.
/// </summary>
[Collection(IdentityApiCollection.Name)]
public sealed class WebsiteJobsEndToEndTests(IdentityApiFactory factory)
{
    private static readonly DocumentVersioning Versioning = new([]);

    private string Cs => factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("Identity")!;

    private sealed record Seed(Guid ShopId, Guid PageId, Guid OrphanAssetId);

    private sealed record State(
        int Publications, bool DraftExists, Guid? OgImageId, int NavItems, DateTimeOffset? NavUpdatedAt, bool MediaExists, DateTimeOffset? OrphanedAt);

    private async Task<Seed> SeedShopAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var seeded = await AddWebsiteAsync(Cs);
        var shopId = seeded.ShopId;
        var ogId = await AddAssetAsync(Cs, shopId, "Library");
        Guid pageId;
        Guid orphanId;

        await using (var db = NewDb(Cs, shopId))
        {
            // 25 bản publish, current = bản mới nhất ⇒ prune xoá 5.
            var last = default(SitePublication)!;
            for (var v = 1; v <= 25; v++)
            {
                last = SitePublication.Create(shopId, seeded.Website.Id, v, "{\"schemaVersion\":1,\"media\":{}}", null, null, now);
                db.SitePublications.Add(last);
            }

            await db.SaveChangesAsync();
            (await db.Websites.SingleAsync()).PointTo(last);

            // Trang xoá mềm 8 ngày trước, còn draft + ảnh OG.
            var page = Page.NewComposable(shopId, seeded.Website.Id, $"p-{Guid.NewGuid():N}"[..12], "Trang", 99);
            page.SetOgImage(ogId);
            db.Pages.Add(page);
            db.PageDrafts.Add(PageDraft.Create(shopId, page.Id, "{}"));
            await db.SaveChangesAsync();
            page.SoftDelete();
            await db.SaveChangesAsync();
            var deletedAt = now - TimeSpan.FromDays(8);
            await db.Pages.IgnoreQueryFilters().Where(p => p.Id == page.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.DeletedAt, deletedAt));
            pageId = page.Id;

            // Menu yên 25h, có mục trỏ trang đã biến mất.
            NavItem[] items =
            [
                new(NavItem.PageKey(seeded.Home.Id), null, null, false, []),
                new(NavItem.PageKey(Guid.NewGuid()), null, null, false, []),
                new("custom", "y", "https://y.vn", true, []),
            ];
            var nav = NavigationConfig.Create(seeded.Website.Id, shopId, NavigationItems.Serialize(items, Versioning));
            nav.MarkPublished();
            db.NavigationConfigs.Add(nav);
            await db.SaveChangesAsync();
            var quiet = now - TimeSpan.FromHours(25);
            await db.NavigationConfigs.IgnoreQueryFilters().Where(n => n.Id == nav.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.CreatedAt, quiet).SetProperty(n => n.UpdatedAt, quiet));

            // Ảnh Direct không ai dùng, đã đánh dấu mồ côi 8 ngày trước ⇒ job ảnh xoá.
            var direct = MediaAsset.NewDirect(shopId, $"shops/{shopId}/direct/{Guid.NewGuid():N}.webp", 100, 100, 10, "p,cover", 0.5f, 0.5f, null, null);
            db.MediaAssets.Add(direct);
            await db.SaveChangesAsync();
            var created = now - TimeSpan.FromDays(30);
            var marked = now - TimeSpan.FromDays(8);
            await db.MediaAssets.IgnoreQueryFilters().Where(m => m.Id == direct.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.CreatedAt, created).SetProperty(m => m.OrphanedAt, marked));
            orphanId = direct.Id;
        }

        return new Seed(shopId, pageId, orphanId);
    }

    private async Task<State> StateAsync(Seed seed)
    {
        await using var db = NewDb(Cs, seed.ShopId);
        var nav = await db.NavigationConfigs.AsNoTracking().SingleAsync();
        var media = await db.MediaAssets.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.Id == seed.OrphanAssetId).Select(m => new { m.OrphanedAt }).SingleOrDefaultAsync();
        return new State(
            await db.SitePublications.CountAsync(),
            await db.PageDrafts.IgnoreQueryFilters().AnyAsync(d => d.Id == seed.PageId),
            await db.Pages.IgnoreQueryFilters().Where(p => p.Id == seed.PageId).Select(p => p.OgImageId).SingleAsync(),
            NavigationItems.CountAll(NavigationItems.Parse(nav.Items, Versioning)),
            nav.UpdatedAt,
            media is not null,
            media?.OrphanedAt);
    }

    private async Task RunAsync<TJob>(Guid shopId)
        where TJob : class, IShopJob
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ShopJobRunner>().RunAsync<TJob>(shopId, CancellationToken.None);
    }

    [Fact]
    public async Task Retention_and_media_jobs_through_real_DI_change_shop_A_and_leave_shop_B_untouched()
    {
        var a = await SeedShopAsync();
        var b = await SeedShopAsync();
        var beforeA = await StateAsync(a);
        var beforeB = await StateAsync(b);
        Assert.Equal(25, beforeA.Publications);
        Assert.True(beforeA.DraftExists);
        Assert.Equal(3, beforeA.NavItems);
        Assert.True(beforeA.MediaExists);

        await RunAsync<WebsiteRetentionJob>(a.ShopId);
        await RunAsync<MediaOrphanCleanupJob>(a.ShopId);

        var afterA = await StateAsync(a);
        Assert.Equal(20, afterA.Publications);
        Assert.False(afterA.DraftExists);
        Assert.Null(afterA.OgImageId);
        Assert.Equal(2, afterA.NavItems);
        Assert.False(afterA.MediaExists);

        Assert.Equal(beforeB, await StateAsync(b));
    }

    [Fact]
    public async Task Retention_job_rerun_is_a_no_op()
    {
        var a = await SeedShopAsync();
        await RunAsync<WebsiteRetentionJob>(a.ShopId);
        var once = await StateAsync(a);

        await RunAsync<WebsiteRetentionJob>(a.ShopId);

        Assert.Equal(once, await StateAsync(a));
    }
}
