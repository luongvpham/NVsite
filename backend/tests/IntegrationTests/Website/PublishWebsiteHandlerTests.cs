using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Website.Commands.CreatePage;
using Vsite.Application.Website.Publishing;
using Vsite.Application.Website.Validation;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Website.Enums;
using static Vsite.IntegrationTests.Website.WebsiteHandlerKit;

namespace Vsite.IntegrationTests.Website;

/// <summary>
/// T4, WEBSITE-004 (Flow 1, R7–R10, R14, G6) — <c>PublishWebsiteHandler</c> trên Postgres thật, validator cây/Media THẬT,
/// <c>RequiredPropsChecker</c> thật. Không qua HTTP (phần HTTP ở <see cref="PublicationEndpointTests"/>).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PublishWebsiteHandlerTests(PostgresFixture postgres) : IAsyncLifetime
{
    private string Cs => postgres.ConnectionString;

    public Task InitializeAsync() => MigrateAsync(Cs);

    public Task DisposeAsync() => Task.CompletedTask;

    private static string HeroWithoutImage(string id) =>
        "{\"id\":\"" + id + "\",\"type\":\"Hero\",\"variant\":\"Hero01\",\"props\":{\"title\":\"Chào\"}}";

    private async Task SaveTreeAsync(Guid shopId, Guid pageId, string tree)
    {
        await using var request = new Request(Cs, shopId);
        var draft = await request.GetDraft(pageId);
        await request.SaveDraft(pageId, tree, draft.Version);
    }

    private async Task<List<Vsite.Domain.Website.Entities.SitePublication>> PublicationsAsync(Guid shopId)
    {
        await using var db = NewDb(Cs, shopId);
        return await db.SitePublications.AsNoTracking().OrderBy(p => p.Version).ToListAsync();
    }

    private async Task<Dictionary<Guid, (bool IsDirty, uint Version)>> DraftsAsync(Guid shopId)
    {
        await using var db = NewDb(Cs, shopId);
        return await db.PageDrafts.AsNoTracking().ToDictionaryAsync(d => d.Id, d => (d.IsDirty, d.Version));
    }

    private static PublishBlockedParams Blocked(PublishBlockedException ex) => Assert.IsType<PublishBlockedParams>(ex.ErrorParams);

    // ---- đường vui ----

    [Fact]
    public async Task Blank_website_publishes_immediately_as_version_1_and_becomes_Published_R8()
    {
        var seeded = await AddWebsiteAsync(Cs);
        // Người publish thật (FK app_user) được kiểm ở PublicationEndpointTests; ở đây không có user → null.
        await using var request = new Request(Cs, seeded.ShopId);

        var dto = await request.Publish("Lần đầu");

        Assert.Equal(1, dto.Version);
        Assert.True(dto.IsCurrent);
        Assert.Equal("Lần đầu", dto.Note);
        Assert.Null(dto.PublishedByUserId);
        Assert.True(dto.PublishedAt > DateTimeOffset.UtcNow.AddMinutes(-5));

        await using var db = NewDb(Cs, seeded.ShopId);
        var website = await db.Websites.AsNoTracking().SingleAsync();
        Assert.Equal(WebsiteStatus.Published, website.Status);
        Assert.Equal(dto.Id, website.CurrentPublicationId);

        var publication = Assert.Single(await PublicationsAsync(seeded.ShopId));
        Assert.Equal(dto.Id, publication.Id);
        Assert.Equal(seeded.Website.Id, publication.WebsiteId);
        Assert.Null(publication.PublishedByUserId);

        // Snapshot hợp lệ theo vỏ sinh từ Zod (#97) và có đủ 3 trang seed đang bật.
        Assert.Empty(S.Schemas.ValidateSiteSnapshot(publication.Snapshot));
        var snapshot = JsonNode.Parse(publication.Snapshot)!;
        Assert.Equal(1, snapshot["version"]!.GetValue<int>());
        Assert.Equal(3, snapshot["pages"]!.AsArray().Count);

        Assert.Equal([seeded.ShopId], request.PointerCache.Invalidated);
    }

    [Fact]
    public async Task Second_publish_is_version_2_and_version_1_stays_byte_identical()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using (var first = new Request(Cs, seeded.ShopId))
        {
            await first.Publish();
        }

        var v1Before = (await PublicationsAsync(seeded.ShopId)).Single().Snapshot;
        await SaveTreeAsync(seeded.ShopId, seeded.Home.Id, TreeWith(RichText("c_abc12", "<p>Bản hai</p>")));

        await using var second = new Request(Cs, seeded.ShopId);
        var dto = await second.Publish();

        Assert.Equal(2, dto.Version);
        var all = await PublicationsAsync(seeded.ShopId);
        Assert.Equal([1, 2], all.Select(p => p.Version));
        Assert.Equal(v1Before, all[0].Snapshot);
        Assert.DoesNotContain("Bản hai", all[0].Snapshot);
        Assert.Contains("Bản hai", all[1].Snapshot);

        await using var db = NewDb(Cs, seeded.ShopId);
        Assert.Equal(dto.Id, (await db.Websites.AsNoTracking().SingleAsync()).CurrentPublicationId);
    }

    [Fact]
    public async Task Publish_clears_IsDirty_and_a_draft_that_was_already_clean_keeps_its_Version_R9()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using (var first = new Request(Cs, seeded.ShopId))
        {
            await first.Publish();
        }

        var afterFirst = await DraftsAsync(seeded.ShopId);
        Assert.All(afterFirst.Values, d => Assert.False(d.IsDirty));

        // Chỉ trang chủ bẩn lại; Contact/NotFound sạch → không bị ghi → xmin giữ nguyên.
        await SaveTreeAsync(seeded.ShopId, seeded.Home.Id, TreeWith(RichText("c_abc12", "<p>Mới</p>")));
        await using (var second = new Request(Cs, seeded.ShopId))
        {
            await second.Publish();
        }

        var afterSecond = await DraftsAsync(seeded.ShopId);
        Assert.All(afterSecond.Values, d => Assert.False(d.IsDirty));
        Assert.Equal(afterFirst[seeded.Contact.Id].Version, afterSecond[seeded.Contact.Id].Version);
        Assert.Equal(afterFirst[seeded.NotFound.Id].Version, afterSecond[seeded.NotFound.Id].Version);
        Assert.NotEqual(afterFirst[seeded.Home.Id].Version, afterSecond[seeded.Home.Id].Version);
    }

    // ---- chặn publish ----

    [Fact]
    public async Task Enabled_page_with_Hero01_missing_image_blocks_publish_with_page_info_and_writes_nothing()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await SaveTreeAsync(seeded.ShopId, seeded.Home.Id, TreeWith(HeroWithoutImage("c_hero1")));
        var draftsBefore = await DraftsAsync(seeded.ShopId);
        await using var request = new Request(Cs, seeded.ShopId);

        var ex = await Assert.ThrowsAsync<PublishBlockedException>(() => request.Publish());

        Assert.Equal("PUBLISH_BLOCKED", ex.ErrorCode);
        Assert.Equal(422, ex.StatusCode);
        var info = Blocked(ex);
        Assert.False(info.Truncated);
        var error = Assert.Single(info.Errors);
        Assert.Equal(seeded.Home.Id, error.PageId);
        Assert.Equal(seeded.Home.Title, error.PageTitle);
        Assert.Equal("/children/0/props/image", error.Path);
        Assert.Equal(TreeErrorCodes.RequiredPropMissing, error.Code);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));

        Assert.Empty(await PublicationsAsync(seeded.ShopId));
        await using var db = NewDb(Cs, seeded.ShopId);
        var website = await db.Websites.AsNoTracking().SingleAsync();
        Assert.Equal(WebsiteStatus.Draft, website.Status);
        Assert.Null(website.CurrentPublicationId);
        Assert.Equal(draftsBefore, await DraftsAsync(seeded.ShopId));
        Assert.Empty(request.PointerCache.Invalidated);
    }

    [Fact]
    public async Task Errors_of_two_pages_come_back_in_one_response()
    {
        var seeded = await AddWebsiteAsync(Cs);
        Vsite.Application.Website.Dtos.PageDto about;
        await using (var setup = new Request(Cs, seeded.ShopId))
        {
            about = await setup.CreatePage(new CreatePageCommand("gioi-thieu", "Giới thiệu", null, null, null, false, true));
        }

        await SaveTreeAsync(seeded.ShopId, seeded.Home.Id, TreeWith(HeroWithoutImage("c_hero1")));
        await SaveTreeAsync(seeded.ShopId, about.Id, TreeWith(HeroWithoutImage("c_hero2")));
        await using var request = new Request(Cs, seeded.ShopId);

        var info = Blocked(await Assert.ThrowsAsync<PublishBlockedException>(() => request.Publish()));

        Assert.Equal(
            new[] { seeded.Home.Id, about.Id }.OrderBy(x => x),
            info.Errors.Select(e => e.PageId!.Value).Distinct().OrderBy(x => x));
        Assert.Equal("Giới thiệu", info.Errors.Single(e => e.PageId == about.Id).PageTitle);
    }

    [Fact]
    public async Task Soft_deleted_image_in_tree_blocks_publish_with_MEDIA_INVALID_IMAGE_REFERENCE()
    {
        var seeded = await AddWebsiteAsync(Cs);
        var clone = await AddAssetAsync(Cs, seeded.ShopId, "Clone");
        await SaveTreeAsync(seeded.ShopId, seeded.Home.Id, TreeWith(Hero("c_hero1", clone.ToString())));
        await using (var db = NewDb(Cs, null))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE media_asset SET is_deleted = true WHERE id = {clone}");
        }

        await using var request = new Request(Cs, seeded.ShopId);
        var info = Blocked(await Assert.ThrowsAsync<PublishBlockedException>(() => request.Publish()));

        var error = Assert.Single(info.Errors);
        Assert.Equal("MEDIA_INVALID_IMAGE_REFERENCE", error.Code);
        Assert.Equal(seeded.Home.Id, error.PageId);
        Assert.Equal("", error.Path);
        Assert.Empty(await PublicationsAsync(seeded.ShopId));
    }

    [Fact]
    public async Task Broken_page_that_is_disabled_does_not_block_and_is_left_out_of_the_snapshot_R7()
    {
        var seeded = await AddWebsiteAsync(Cs);
        Guid hiddenId;
        await using (var setup = new Request(Cs, seeded.ShopId))
        {
            hiddenId = (await setup.CreatePage(new CreatePageCommand("an", "Ẩn", null, null, null, false, false))).Id;
        }

        await SaveTreeAsync(seeded.ShopId, hiddenId, TreeWith(HeroWithoutImage("c_hero1")));
        await using var request = new Request(Cs, seeded.ShopId);

        await request.Publish();

        var snapshot = JsonNode.Parse((await PublicationsAsync(seeded.ShopId)).Single().Snapshot)!;
        var ids = snapshot["pages"]!.AsArray().Select(p => Guid.Parse(p!["id"]!.GetValue<string>())).ToList();
        Assert.DoesNotContain(hiddenId, ids);
        Assert.Equal(3, ids.Count);
        Assert.True((await DraftsAsync(seeded.ShopId))[hiddenId].IsDirty); // trang tắt không bị đụng cờ
    }

    // ---- trạng thái / race / tenant ----

    [Fact]
    public async Task Suspended_website_gets_a_new_publication_but_stays_Suspended_G6()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using (var db = NewDb(Cs, seeded.ShopId))
        {
            var website = await db.Websites.SingleAsync();
            website.Suspend();
            await db.SaveChangesAsync();
        }

        await using var request = new Request(Cs, seeded.ShopId);
        var dto = await request.Publish();

        await using var check = NewDb(Cs, seeded.ShopId);
        var after = await check.Websites.AsNoTracking().SingleAsync();
        Assert.Equal(WebsiteStatus.Suspended, after.Status);
        Assert.Equal(dto.Id, after.CurrentPublicationId);
    }

    [Fact]
    public async Task Draft_changed_between_read_and_write_is_409_PUBLISH_CONFLICT_and_nothing_is_written_R10()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var racing = new Request(Cs, seeded.ShopId, null, new BumpXminInterceptor(Cs, seeded.Home.Id));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => racing.Publish());

        Assert.Equal("PUBLISH_CONFLICT", ex.ErrorCode);
        Assert.Equal(409, ex.StatusCode);
        Assert.Empty(await PublicationsAsync(seeded.ShopId));
        Assert.True((await DraftsAsync(seeded.ShopId))[seeded.Home.Id].IsDirty);
        await using var db = NewDb(Cs, seeded.ShopId);
        Assert.Null((await db.Websites.AsNoTracking().SingleAsync()).CurrentPublicationId);
    }

    [Fact]
    public async Task Concurrent_publish_that_takes_the_same_version_is_409_PUBLISH_CONFLICT()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var racing = new Request(Cs, seeded.ShopId, null, new InsertPublicationInterceptor(Cs, seeded.ShopId, seeded.Website.Id, 1));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => racing.Publish());

        Assert.Equal("PUBLISH_CONFLICT", ex.ErrorCode);
        Assert.Single(await PublicationsAsync(seeded.ShopId)); // chỉ dòng "tab kia" chèn
    }

    [Fact]
    public async Task Unresolved_tenant_is_TENANT_REQUIRED()
    {
        await using var request = new Request(Cs, shopId: null);

        var ex = await Assert.ThrowsAsync<ForbiddenAccessException>(() => request.Publish());

        Assert.Equal("TENANT_REQUIRED", ex.ErrorCode);
    }

    [Fact]
    public async Task Tenant_of_shop_B_cannot_publish_website_of_shop_A()
    {
        var a = await AddWebsiteAsync(Cs);
        var shopB = await AddShopAsync(Cs);
        await using var requestB = new Request(Cs, shopB);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => requestB.Publish());

        Assert.Equal("WEBSITE_NOT_FOUND", ex.ErrorCode);
        Assert.Empty(await PublicationsAsync(a.ShopId));
    }

    // ---- bảng media ----

    [Fact]
    public async Task Snapshot_media_contains_every_tree_image_and_og_image()
    {
        var seeded = await AddWebsiteAsync(Cs);
        var heroClone = await AddAssetAsync(Cs, seeded.ShopId, "Clone");
        var galleryClone = await AddAssetAsync(Cs, seeded.ShopId, "Clone");
        var pageOg = await AddAssetAsync(Cs, seeded.ShopId, "Library");
        var siteOg = await AddAssetAsync(Cs, seeded.ShopId, "Library");
        var favicon = await AddAssetAsync(Cs, seeded.ShopId, "Library");
        await SaveTreeAsync(seeded.ShopId, seeded.Home.Id,
            TreeWith(Hero("c_hero1", heroClone.ToString()), Gallery("c_gal01", galleryClone.ToString())));
        await using (var setup = new Request(Cs, seeded.ShopId))
        {
            await setup.CreatePage(new CreatePageCommand("gioi-thieu", "Giới thiệu", null, null, pageOg, false, true));
        }

        await using (var db = NewDb(Cs, seeded.ShopId))
        {
            var website = await db.Websites.SingleAsync();
            website.SetDefaultOgImage(siteOg);
            website.SetFavicon(favicon);
            await db.SaveChangesAsync();
        }

        await using var request = new Request(Cs, seeded.ShopId);
        await request.Publish();

        var snapshotJson = (await PublicationsAsync(seeded.ShopId)).Single().Snapshot;
        Assert.Empty(S.Schemas.ValidateSiteSnapshot(snapshotJson));
        var media = JsonNode.Parse(snapshotJson)!["media"]!.AsObject();
        Assert.Equal(
            new[] { heroClone, galleryClone, pageOg, siteOg, favicon }.Select(g => g.ToString()).OrderBy(x => x, StringComparer.Ordinal),
            media.Select(kv => kv.Key).OrderBy(x => x, StringComparer.Ordinal));

        await using var check = NewDb(Cs, seeded.ShopId);
        var hero = await check.MediaAssets.AsNoTracking().SingleAsync(a => a.Id == heroClone);
        Assert.Equal(hero.StorageKey, media[heroClone.ToString()]!["storageKey"]!.GetValue<string>());
        Assert.Equal(hero.Preset, media[heroClone.ToString()]!["preset"]!.GetValue<string>());
        Assert.Null(media[pageOg.ToString()]!["preset"]);
    }

    [Fact]
    public async Task Uppercase_imageId_in_draft_is_lowercased_in_snapshot_and_matches_the_media_key_R14()
    {
        var seeded = await AddWebsiteAsync(Cs);
        var clone = await AddAssetAsync(Cs, seeded.ShopId, "Clone");
        var upper = clone.ToString().ToUpperInvariant();
        await SaveTreeAsync(seeded.ShopId, seeded.Home.Id, TreeWith(Hero("c_hero1", upper)));
        await using var request = new Request(Cs, seeded.ShopId);

        await request.Publish();

        var snapshotJson = (await PublicationsAsync(seeded.ShopId)).Single().Snapshot;
        Assert.DoesNotContain(upper, snapshotJson);
        var snapshot = JsonNode.Parse(snapshotJson)!;
        var home = snapshot["pages"]!.AsArray().Single(p => p!["id"]!.GetValue<string>() == seeded.Home.Id.ToString())!;
        var treeImageId = home["tree"]!["children"]![0]!["props"]!["image"]!["imageId"]!.GetValue<string>();
        Assert.Equal(clone.ToString(), treeImageId);
        Assert.True(snapshot["media"]!.AsObject().ContainsKey(treeImageId));
    }
}
