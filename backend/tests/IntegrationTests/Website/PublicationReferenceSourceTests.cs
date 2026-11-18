using Microsoft.EntityFrameworkCore;
using Vsite.Application.Media.Dtos;
using Vsite.Application.Website.Commands.CreatePage;
using Vsite.Infrastructure.Website;
using static Vsite.IntegrationTests.Website.WebsiteHandlerKit;

namespace Vsite.IntegrationTests.Website;

/// <summary>
/// T8 (WEBSITE-004, 08 §4, R11) — hai nguồn tham chiếu mới của Media trên Postgres thật: <see cref="SitePublicationReferenceSource"/>
/// (ảnh còn nằm trong <c>snapshot.media</c> của bản publish nào) và <see cref="SeoImageReferenceSource"/> (ảnh OG trang / OG website / favicon).
/// Dữ liệu dựng bằng handler thật (save draft, publish) nên snapshot là snapshot do đúng đường sản phẩm ghi ra.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PublicationReferenceSourceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private string Cs => postgres.ConnectionString;

    public Task InitializeAsync() => MigrateAsync(Cs);

    public Task DisposeAsync() => Task.CompletedTask;

    private static Task<IReadOnlyList<MediaReferenceDto>> PublicationRefs(Request request, Guid assetId) =>
        new SitePublicationReferenceSource(request.Db, request.Tenant, S.Versioning).FindAsync(assetId, default);

    private static Task<IReadOnlyList<MediaReferenceDto>> SeoRefs(Request request, Guid assetId) =>
        new SeoImageReferenceSource(request.Db, request.Tenant).FindAsync(assetId, default);

    private async Task SetWebsiteImagesAsync(Guid shopId, Guid? og, Guid? favicon)
    {
        await using var db = NewDb(Cs, shopId);
        var website = await db.Websites.SingleAsync();
        website.SetDefaultOgImage(og);
        website.SetFavicon(favicon);
        await db.SaveChangesAsync();
    }

    private async Task SetPageOgAsync(Guid shopId, Guid pageId, Guid? og)
    {
        await using var db = NewDb(Cs, shopId);
        (await db.Pages.SingleAsync(p => p.Id == pageId)).SetOgImage(og);
        await db.SaveChangesAsync();
    }

    private async Task<string> ReadSnapshotAsync(Guid shopId)
    {
        await using var db = NewDb(Cs, null);
        return await db.Database
            .SqlQuery<string>($"SELECT snapshot::text AS \"Value\" FROM site_publication WHERE shop_id = {shopId} LIMIT 1")
            .FirstAsync();
    }

    // ---------- SitePublication ----------

    [Fact]
    public async Task Image_in_the_running_publication_is_one_current_reference()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var (library, clones) = await AddLibraryWithClonesAsync(Cs, seeded.ShopId, 1);
        await SaveTreeAsync(request, seeded.Home.Id, TreeWith(Hero("c_hero1", clones[0].ToString("D"))));
        await request.Publish();

        var references = await PublicationRefs(request, library);

        var reference = Assert.Single(references);
        Assert.Equal(MediaReferenceKind.SitePublication, reference.Kind);
        Assert.True(reference.IsCurrent);
        Assert.Null(reference.PageId);
    }

    [Fact]
    public async Task Image_only_in_an_older_publication_is_reported_with_isCurrent_false()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var (library, clones) = await AddLibraryWithClonesAsync(Cs, seeded.ShopId, 1);
        await SaveTreeAsync(request, seeded.Home.Id, TreeWith(Hero("c_hero1", clones[0].ToString("D"))));
        await request.Publish();
        await SaveTreeAsync(request, seeded.Home.Id, TreeWith(RichText("c_text1", "<p>không ảnh</p>")));
        await request.Publish();

        var reference = Assert.Single(await PublicationRefs(request, library));

        Assert.Equal(MediaReferenceKind.SitePublication, reference.Kind);
        Assert.False(reference.IsCurrent);
    }

    [Fact]
    public async Task Image_in_three_publications_is_still_one_reference_and_current_when_the_running_one_has_it()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var (library, clones) = await AddLibraryWithClonesAsync(Cs, seeded.ShopId, 1);
        await SaveTreeAsync(request, seeded.Home.Id, TreeWith(Hero("c_hero1", clones[0].ToString("D"))));
        await request.Publish();
        await request.Publish();
        await request.Publish();

        var reference = Assert.Single(await PublicationRefs(request, library));

        Assert.True(reference.IsCurrent);
    }

    [Fact]
    public async Task Rollback_to_a_version_without_the_image_flips_isCurrent_to_false()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var (library, clones) = await AddLibraryWithClonesAsync(Cs, seeded.ShopId, 1);
        var v1 = await request.Publish();
        await SaveTreeAsync(request, seeded.Home.Id, TreeWith(Hero("c_hero1", clones[0].ToString("D"))));
        await request.Publish();
        Assert.True(Assert.Single(await PublicationRefs(request, library)).IsCurrent);

        await request.Rollback(v1.Id);

        Assert.False(Assert.Single(await PublicationRefs(request, library)).IsCurrent);
    }

    [Fact]
    public async Task Og_image_of_a_page_and_of_the_website_are_found_in_the_snapshot_via_the_library_id_itself()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var pageOg = await AddAssetAsync(Cs, seeded.ShopId, "Library");
        var siteOg = await AddAssetAsync(Cs, seeded.ShopId, "Library");
        await SetPageOgAsync(seeded.ShopId, seeded.Home.Id, pageOg);
        await SetWebsiteImagesAsync(seeded.ShopId, siteOg, null);
        await request.Publish();

        Assert.True(Assert.Single(await PublicationRefs(request, pageOg)).IsCurrent);
        Assert.True(Assert.Single(await PublicationRefs(request, siteOg)).IsCurrent);
    }

    [Fact]
    public async Task Media_key_matches_case_insensitively_R14()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var (library, clones) = await AddLibraryWithClonesAsync(Cs, seeded.ShopId, 1);
        await SaveTreeAsync(request, seeded.Home.Id, TreeWith(Hero("c_hero1", clones[0].ToString("D"))));
        await request.Publish();
        var lower = clones[0].ToString("D");
        var upper = lower.ToUpperInvariant();
        await using (var db = NewDb(Cs, null))
        {
            // Chỉ đổi KEY trong bảng media (giữ nguyên giá trị) — không đụng imageId trong tree.
            var changed = await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE site_publication SET snapshot = jsonb_set(snapshot, '{{media}}', ((snapshot->'media') - {lower}) || jsonb_build_object({upper}::text, snapshot->'media'->{lower})) WHERE shop_id = {seeded.ShopId}");
            Assert.Equal(1, changed);
            var keys = await db.Database
                .SqlQuery<string>($"SELECT k AS \"Value\" FROM site_publication, jsonb_object_keys(snapshot->'media') k WHERE shop_id = {seeded.ShopId}")
                .ToListAsync();
            Assert.Equal([upper], keys); // chứng minh key đã đổi thật (test không rỗng nghĩa)
        }

        var reference = Assert.Single(await PublicationRefs(request, library));

        Assert.True(reference.IsCurrent);
    }

    [Fact]
    public async Task Publication_source_hides_other_shops_and_empty_cases()
    {
        var a = await AddWebsiteAsync(Cs);
        var b = await AddWebsiteAsync(Cs);
        await using var requestA = new Request(Cs, a.ShopId);
        await using var requestB = new Request(Cs, b.ShopId);
        var (libraryA, _) = await AddLibraryWithClonesAsync(Cs, a.ShopId, 1);
        var (libraryB, clonesB) = await AddLibraryWithClonesAsync(Cs, b.ShopId, 1);
        await SaveTreeAsync(requestB, b.Home.Id, TreeWith(Hero("c_hero1", clonesB[0].ToString("D"))));
        await requestB.Publish();
        await requestA.Publish(); // shop A cũng có bản publish, nhưng không có ảnh nào

        Assert.Empty(await PublicationRefs(requestA, libraryB)); // shop A hỏi id Library của shop B
        // Hỏi bằng id clone — key này CÓ THẬT trong snapshot của B nên chỉ tenant isolation mới giữ kết quả rỗng.
        Assert.Empty(await PublicationRefs(requestA, clonesB[0]));
        Assert.Contains(clonesB[0].ToString("D"), (await ReadSnapshotAsync(b.ShopId)));
        Assert.Empty(await PublicationRefs(requestA, libraryA)); // ảnh của A, không dùng ở đâu
        Assert.Empty(await PublicationRefs(requestA, Guid.NewGuid()));
        Assert.Single(await PublicationRefs(requestB, libraryB)); // đối chứng
    }

    [Fact]
    public async Task Publication_source_with_unresolved_tenant_returns_empty()
    {
        var seeded = await AddWebsiteAsync(Cs);
        var (library, clones) = await AddLibraryWithClonesAsync(Cs, seeded.ShopId, 1);
        await using (var request = new Request(Cs, seeded.ShopId))
        {
            await SaveTreeAsync(request, seeded.Home.Id, TreeWith(Hero("c_hero1", clones[0].ToString("D"))));
            await request.Publish();
        }

        await using var noTenant = new Request(Cs, shopId: null);

        Assert.Empty(await PublicationRefs(noTenant, library));
    }

    // ---------- Ảnh OG / favicon ----------

    [Fact]
    public async Task Page_og_image_is_one_reference_per_page_with_page_id_and_title()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var og = await AddAssetAsync(Cs, seeded.ShopId, "Library");
        var other = await AddAssetAsync(Cs, seeded.ShopId, "Library");
        var pageB = await request.CreatePage(new CreatePageCommand("trang-b", "Trang B"));
        var pageC = await request.CreatePage(new CreatePageCommand("trang-c", "Trang C"));
        await SetPageOgAsync(seeded.ShopId, seeded.Home.Id, og);
        await SetPageOgAsync(seeded.ShopId, pageB.Id, og);
        await SetPageOgAsync(seeded.ShopId, pageC.Id, other);

        var references = await SeoRefs(request, og);

        Assert.All(references, r => Assert.Equal(MediaReferenceKind.PageOgImage, r.Kind));
        Assert.Equal(2, references.Count);
        Assert.Contains(references, r => r.PageId == seeded.Home.Id && r.PageTitle == seeded.Home.Title);
        Assert.Contains(references, r => r.PageId == pageB.Id && r.PageTitle == "Trang B");
        Assert.All(references, r => Assert.Null(r.IsCurrent));
    }

    [Fact]
    public async Task Website_og_image_and_favicon_report_their_own_kind_without_page()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var og = await AddAssetAsync(Cs, seeded.ShopId, "Library");
        var favicon = await AddAssetAsync(Cs, seeded.ShopId, "Library");
        await SetWebsiteImagesAsync(seeded.ShopId, og, favicon);

        var ogRef = Assert.Single(await SeoRefs(request, og));
        var faviconRef = Assert.Single(await SeoRefs(request, favicon));

        Assert.Equal(MediaReferenceKind.WebsiteOgImage, ogRef.Kind);
        Assert.Equal(MediaReferenceKind.WebsiteFavicon, faviconRef.Kind);
        Assert.Null(ogRef.PageId);
        Assert.Null(faviconRef.PageTitle);
    }

    [Fact]
    public async Task Same_image_used_as_site_og_and_favicon_reports_both()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var image = await AddAssetAsync(Cs, seeded.ShopId, "Library");
        await SetWebsiteImagesAsync(seeded.ShopId, image, image);

        var kinds = (await SeoRefs(request, image)).Select(r => r.Kind).ToHashSet();

        Assert.Equal(
            new HashSet<MediaReferenceKind> { MediaReferenceKind.WebsiteOgImage, MediaReferenceKind.WebsiteFavicon }, kinds);
    }

    [Fact]
    public async Task Seo_source_hides_other_shops_unused_assets_and_unresolved_tenant()
    {
        var a = await AddWebsiteAsync(Cs);
        var b = await AddWebsiteAsync(Cs);
        await using var requestA = new Request(Cs, a.ShopId);
        await using var requestB = new Request(Cs, b.ShopId);
        var unusedA = await AddAssetAsync(Cs, a.ShopId, "Library");
        var ogB = await AddAssetAsync(Cs, b.ShopId, "Library");
        var faviconB = await AddAssetAsync(Cs, b.ShopId, "Library");
        await SetPageOgAsync(b.ShopId, b.Home.Id, ogB);
        await SetWebsiteImagesAsync(b.ShopId, ogB, faviconB);

        Assert.Empty(await SeoRefs(requestA, ogB));
        Assert.Empty(await SeoRefs(requestA, faviconB));
        Assert.Empty(await SeoRefs(requestA, unusedA));
        Assert.Empty(await SeoRefs(requestA, Guid.NewGuid()));
        Assert.Equal(2, (await SeoRefs(requestB, ogB)).Count); // đối chứng: trang + website OG
        await using var noTenant = new Request(Cs, shopId: null);
        Assert.Empty(await SeoRefs(noTenant, ogB));
    }
}
