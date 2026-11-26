using Microsoft.EntityFrameworkCore;
using Vsite.Application.Website.Commands.CreatePage;
using Vsite.Infrastructure.Website;
using static Vsite.IntegrationTests.Website.WebsiteHandlerKit;

namespace Vsite.IntegrationTests.Website;

/// <summary>
/// WEBSITE-008 T6 (#117, R5) — ba nguồn "ảnh đang dùng" của Website trên Postgres thật. Dữ liệu dựng bằng handler thật
/// (save draft, publish). Nguồn phải NÉM khi không đọc được, không trả rỗng (rỗng ⇒ T7 xoá nhầm ảnh đang dùng).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class MediaUsageSourceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private string Cs => postgres.ConnectionString;

    public Task InitializeAsync() => MigrateAsync(Cs);

    public Task DisposeAsync() => Task.CompletedTask;

    private static Task<IReadOnlySet<Guid>> Drafts(Request r) =>
        new PageDraftUsageSource(r.Db, r.Tenant, S.Validator).CollectUsedAssetIdsAsync(default);

    private static Task<IReadOnlySet<Guid>> Publications(Request r) =>
        new SitePublicationUsageSource(r.Db, r.Tenant, S.Versioning).CollectUsedAssetIdsAsync(default);

    private static Task<IReadOnlySet<Guid>> Seo(Request r) =>
        new SeoImageUsageSource(r.Db, r.Tenant).CollectUsedAssetIdsAsync(default);

    // ---------- PageDraftUsageSource ----------

    [Fact]
    public async Task Draft_source_returns_ids_of_live_pages_only_case_insensitive()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var (_, clones) = await AddLibraryWithClonesAsync(Cs, seeded.ShopId, 3);
        var pageB = await request.CreatePage(new CreatePageCommand("trang-b", "Trang B"));
        var gone = await request.CreatePage(new CreatePageCommand("se-xoa", "Sẽ xoá"));
        await SaveTreeAsync(request, seeded.Home.Id, TreeWith(
            Hero("c_hero1", clones[0].ToString("D")), Gallery("c_gall1", clones[1].ToString("D"))));
        await SaveTreeAsync(request, pageB.Id, TreeWith(Hero("c_hero1", clones[0].ToString("D").ToUpperInvariant())));
        await SaveTreeAsync(request, gone.Id, TreeWith(Hero("c_hero1", clones[2].ToString("D"))));
        await request.DeletePage(gone.Id);

        var used = await Drafts(request);

        Assert.Equal(new HashSet<Guid> { clones[0], clones[1] }, used.ToHashSet());
    }

    [Fact]
    public async Task Draft_source_hides_other_shops_and_empty_shop_is_empty()
    {
        var a = await AddWebsiteAsync(Cs);
        var b = await AddWebsiteAsync(Cs);
        await using var requestA = new Request(Cs, a.ShopId);
        await using var requestB = new Request(Cs, b.ShopId);
        var (_, clonesB) = await AddLibraryWithClonesAsync(Cs, b.ShopId, 1);
        await SaveTreeAsync(requestB, b.Home.Id, TreeWith(Hero("c_hero1", clonesB[0].ToString("D"))));

        Assert.Empty(await Drafts(requestA));
        Assert.Equal(clonesB[0], Assert.Single(await Drafts(requestB)));
    }

    [Fact]
    public async Task Draft_source_throws_when_a_tree_cannot_be_read()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        await using (var db = NewDb(Cs, null))
        {
            var changed = await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE page_draft SET tree = '{{\"schemaVersion\":999}}'::jsonb WHERE shop_id = {seeded.ShopId}");
            Assert.True(changed >= 1);
        }

        await Assert.ThrowsAnyAsync<Exception>(() => Drafts(request));
    }

    [Fact]
    public async Task Draft_source_throws_on_unknown_node_type_even_with_valid_envelope()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var (_, clones) = await AddLibraryWithClonesAsync(Cs, seeded.ShopId, 1);
        // Hero hợp lệ + một node type lạ: validator báo lỗi nên danh sách ảnh có thể thiếu → nguồn phải ném.
        var tree = TreeWith(Hero("c_hero1", clones[0].ToString("D")), "{\"id\":\"c_x1\",\"type\":\"NoSuchType\",\"props\":{}}");
        await using (var db = NewDb(Cs, null))
        {
            var changed = await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE page_draft SET tree = {tree}::jsonb WHERE shop_id = {seeded.ShopId}");
            Assert.True(changed >= 1);
        }

        await Assert.ThrowsAnyAsync<Exception>(() => Drafts(request));
    }

    // ---------- SitePublicationUsageSource ----------

    [Fact]
    public async Task Publication_source_unions_media_keys_of_all_remaining_publications_case_insensitive()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var (_, clones) = await AddLibraryWithClonesAsync(Cs, seeded.ShopId, 2);
        await SaveTreeAsync(request, seeded.Home.Id, TreeWith(Hero("c_hero1", clones[0].ToString("D"))));
        await request.Publish();
        await SaveTreeAsync(request, seeded.Home.Id, TreeWith(Hero("c_hero1", clones[1].ToString("D"))));
        await request.Publish();
        var lower = clones[0].ToString("D");
        var upper = lower.ToUpperInvariant();
        await using (var db = NewDb(Cs, null))
        {
            // Đổi KEY của bản có clones[0] sang chữ HOA (giữ giá trị).
            var changed = await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE site_publication SET snapshot = jsonb_set(snapshot, '{{media}}', ((snapshot->'media') - {lower}) || jsonb_build_object({upper}::text, snapshot->'media'->{lower})) WHERE shop_id = {seeded.ShopId} AND jsonb_exists(snapshot->'media', {lower})");
            Assert.Equal(1, changed);
        }

        var used = await Publications(request);

        Assert.Equal(new HashSet<Guid> { clones[0], clones[1] }, used.ToHashSet());
    }

    [Fact]
    public async Task Publication_source_hides_other_shops()
    {
        var a = await AddWebsiteAsync(Cs);
        var b = await AddWebsiteAsync(Cs);
        await using var requestA = new Request(Cs, a.ShopId);
        await using var requestB = new Request(Cs, b.ShopId);
        var (_, clonesB) = await AddLibraryWithClonesAsync(Cs, b.ShopId, 1);
        await SaveTreeAsync(requestB, b.Home.Id, TreeWith(Hero("c_hero1", clonesB[0].ToString("D"))));
        await requestB.Publish();
        await requestA.Publish();

        Assert.Empty(await Publications(requestA));
        Assert.Contains(clonesB[0], await Publications(requestB));
    }

    [Fact]
    public async Task Publication_source_throws_when_a_snapshot_cannot_be_read()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        await request.Publish();
        await request.Publish();
        await using (var db = NewDb(Cs, null))
        {
            // Hỏng đúng MỘT bản: nguồn vẫn phải ném, không trả tập từ các bản còn lại.
            var changed = await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE site_publication SET snapshot = '{{\"schemaVersion\":999}}'::jsonb WHERE id = (SELECT id FROM site_publication WHERE shop_id = {seeded.ShopId} LIMIT 1)");
            Assert.Equal(1, changed);
        }

        await Assert.ThrowsAnyAsync<Exception>(() => Publications(request));
    }

    [Fact]
    public async Task Publication_source_throws_when_snapshot_has_no_media()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        await request.Publish();
        await using (var db = NewDb(Cs, null))
        {
            var changed = await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE site_publication SET snapshot = snapshot - 'media' WHERE shop_id = {seeded.ShopId}");
            Assert.Equal(1, changed);
        }

        await Assert.ThrowsAnyAsync<Exception>(() => Publications(request));
    }

    // ---------- SeoImageUsageSource ----------

    [Fact]
    public async Task Seo_source_returns_page_og_of_live_pages_website_og_and_favicon()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var pageOg = await AddAssetAsync(Cs, seeded.ShopId, "Library");
        var goneOg = await AddAssetAsync(Cs, seeded.ShopId, "Library");
        var siteOg = await AddAssetAsync(Cs, seeded.ShopId, "Library");
        var favicon = await AddAssetAsync(Cs, seeded.ShopId, "Library");
        var gone = await request.CreatePage(new CreatePageCommand("se-xoa", "Sẽ xoá"));
        await using (var db = NewDb(Cs, seeded.ShopId))
        {
            (await db.Pages.SingleAsync(p => p.Id == seeded.Home.Id)).SetOgImage(pageOg);
            (await db.Pages.SingleAsync(p => p.Id == gone.Id)).SetOgImage(goneOg);
            var website = await db.Websites.SingleAsync();
            website.SetDefaultOgImage(siteOg);
            website.SetFavicon(favicon);
            await db.SaveChangesAsync();
        }

        await request.DeletePage(gone.Id);

        var used = await Seo(request);

        Assert.Equal(new HashSet<Guid> { pageOg, siteOg, favicon }, used.ToHashSet());
    }

    [Fact]
    public async Task Seo_source_hides_other_shops()
    {
        var a = await AddWebsiteAsync(Cs);
        var b = await AddWebsiteAsync(Cs);
        await using var requestA = new Request(Cs, a.ShopId);
        await using var requestB = new Request(Cs, b.ShopId);
        var ogB = await AddAssetAsync(Cs, b.ShopId, "Library");
        await using (var db = NewDb(Cs, b.ShopId))
        {
            (await db.Pages.SingleAsync(p => p.Id == b.Home.Id)).SetOgImage(ogB);
            (await db.Websites.SingleAsync()).SetFavicon(ogB);
            await db.SaveChangesAsync();
        }

        Assert.Empty(await Seo(requestA));
        Assert.Equal(ogB, Assert.Single(await Seo(requestB)));
    }

    // ---------- Không tenant ----------

    [Fact]
    public async Task Sources_throw_without_a_resolved_tenant()
    {
        await using var noTenant = new Request(Cs, shopId: null);

        await Assert.ThrowsAnyAsync<Exception>(() => Drafts(noTenant));
        await Assert.ThrowsAnyAsync<Exception>(() => Publications(noTenant));
        await Assert.ThrowsAnyAsync<Exception>(() => Seo(noTenant));
    }
}
