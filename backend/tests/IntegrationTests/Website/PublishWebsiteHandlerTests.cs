using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Website.Commands.CreatePage;
using Vsite.Application.Website.Commands.UpdatePage;
using Vsite.Application.Website.Dtos;
using Vsite.Application.Website.Navigation;
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

    [Fact]
    public async Task Truncated_is_true_when_a_page_validator_stopped_early_I1()
    {
        var seeded = await AddWebsiteAsync(Cs);
        // PUT draft không cho lưu cây hỏng → ghi thẳng: 60 node type lạ (catalog đổi luật sau khi lưu). Validator dừng ở MaxErrors.
        var tree = TreeWith(Enumerable.Range(0, 60)
            .Select(i => "{\"id\":\"c_x" + i.ToString("D4") + "\",\"type\":\"NoSuchType\",\"variant\":\"X\",\"props\":{}}")
            .ToArray());
        await using (var db = NewDb(Cs, null))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE page_draft SET tree = {tree}::jsonb WHERE page_id = {seeded.Home.Id}");
        }

        await using var request = new Request(Cs, seeded.ShopId);
        var info = Blocked(await Assert.ThrowsAsync<PublishBlockedException>(() => request.Publish()));

        Assert.True(info.Errors.Count < 60);
        Assert.True(info.Truncated);
    }

    [Fact]
    public async Task Website_level_image_error_has_null_page_and_comes_first_I1()
    {
        var seeded = await AddWebsiteAsync(Cs);
        var favicon = await AddAssetAsync(Cs, seeded.ShopId, "Library");
        await using (var db = NewDb(Cs, seeded.ShopId))
        {
            var website = await db.Websites.SingleAsync();
            website.SetFavicon(favicon);
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE media_asset SET is_deleted = true WHERE id = {favicon}");
        }

        await SaveTreeAsync(seeded.ShopId, seeded.Home.Id, TreeWith(HeroWithoutImage("c_hero1")));
        await using var request = new Request(Cs, seeded.ShopId);

        var info = Blocked(await Assert.ThrowsAsync<PublishBlockedException>(() => request.Publish()));

        Assert.Equal(2, info.Errors.Count);
        Assert.Null(info.Errors[0].PageId);
        Assert.Null(info.Errors[0].PageTitle);
        Assert.Equal("MEDIA_INVALID_IMAGE_REFERENCE", info.Errors[0].Code);
        Assert.Equal(seeded.Home.Id, info.Errors[1].PageId);
    }

    [Fact]
    public async Task Image_soft_deleted_between_check_and_load_is_409_and_nothing_is_written_I2()
    {
        var seeded = await AddWebsiteAsync(Cs);
        var clone = await AddAssetAsync(Cs, seeded.ShopId, "Clone");
        await SaveTreeAsync(seeded.ShopId, seeded.Home.Id, TreeWith(Hero("c_hero1", clone.ToString())));
        await using var request = new Request(Cs, seeded.ShopId);
        var racingMedia = new SoftDeleteAfterCheckMediaValidator(request.Media, Cs, clone);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => request.Publish(media: racingMedia));

        Assert.Equal("PUBLISH_CONFLICT", ex.ErrorCode);
        Assert.Empty(await PublicationsAsync(seeded.ShopId));
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

    // ---- T5: danh sách bản + rollback ----

    private async Task<(Guid ShopId, Guid[] Ids)> ThreePublicationsAsync()
    {
        var seeded = await AddWebsiteAsync(Cs);
        var ids = new List<Guid>();
        for (var i = 1; i <= 3; i++)
        {
            await using var request = new Request(Cs, seeded.ShopId);
            ids.Add((await request.Publish($"bản {i}")).Id);
        }

        return (seeded.ShopId, [.. ids]);
    }

    [Fact]
    public async Task List_returns_publications_descending_with_exactly_one_current_T5()
    {
        var (shopId, ids) = await ThreePublicationsAsync();
        await using var request = new Request(Cs, shopId);

        var list = await request.ListPublications();

        Assert.Equal([3, 2, 1], list.Select(p => p.Version));
        Assert.Equal([ids[2], ids[1], ids[0]], list.Select(p => p.Id));
        Assert.Equal(ids[2], Assert.Single(list, p => p.IsCurrent).Id);
    }

    [Fact]
    public async Task List_is_capped_at_20_newest_T5()
    {
        var seeded = await AddWebsiteAsync(Cs);
        for (var i = 0; i < 22; i++)
        {
            await using var request = new Request(Cs, seeded.ShopId);
            await request.Publish();
        }

        await using var list = new Request(Cs, seeded.ShopId);
        var result = await list.ListPublications();

        Assert.Equal(20, result.Count);
        Assert.Equal(22, result[0].Version);
        Assert.Equal(3, result[^1].Version);
    }

    [Fact]
    public async Task List_without_website_is_404_and_without_tenant_is_TENANT_REQUIRED_T5()
    {
        var shopId = await AddShopAsync(Cs);
        await using var request = new Request(Cs, shopId);
        await Assert.ThrowsAsync<NotFoundException>(() => request.ListPublications());

        await using var noTenant = new Request(Cs, null);
        var ex = await Assert.ThrowsAsync<ForbiddenAccessException>(() => noTenant.ListPublications());
        Assert.Equal("TENANT_REQUIRED", ex.ErrorCode);
    }

    [Fact]
    public async Task Rollback_repoints_marks_all_drafts_dirty_keeps_trees_and_creates_no_publication_G7_T5()
    {
        var (shopId, ids) = await ThreePublicationsAsync();
        Dictionary<Guid, string> treesBefore;
        await using (var before = NewDb(Cs, shopId))
        {
            treesBefore = await before.PageDrafts.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Tree);
        }

        await using var request = new Request(Cs, shopId);
        var dto = await request.Rollback(ids[0]);

        Assert.Equal(ids[0], dto.Id);
        Assert.True(dto.IsCurrent);
        Assert.Equal(1, dto.Version);

        await using var db = NewDb(Cs, shopId);
        Assert.Equal(ids[0], (await db.Websites.AsNoTracking().SingleAsync()).CurrentPublicationId);
        var drafts = await db.PageDrafts.AsNoTracking().ToListAsync();
        Assert.NotEmpty(drafts);
        Assert.All(drafts, d => Assert.True(d.IsDirty));
        Assert.All(drafts, d => Assert.Equal(treesBefore[d.Id], d.Tree));
        Assert.Equal(3, (await PublicationsAsync(shopId)).Count);
        Assert.Equal([shopId], request.PointerCache.Invalidated);
    }

    [Fact]
    public async Task Rollback_to_current_publication_writes_nothing_T5()
    {
        var (shopId, ids) = await ThreePublicationsAsync();
        DateTimeOffset? updatedBefore;
        await using (var before = NewDb(Cs, shopId))
        {
            updatedBefore = (await before.Websites.AsNoTracking().SingleAsync()).UpdatedAt;
        }

        var draftsBefore = await DraftsAsync(shopId);
        await using var request = new Request(Cs, shopId);
        var dto = await request.Rollback(ids[2]);

        Assert.True(dto.IsCurrent);
        Assert.Equal(3, dto.Version);
        await using var db = NewDb(Cs, shopId);
        Assert.Equal(updatedBefore, (await db.Websites.AsNoTracking().SingleAsync()).UpdatedAt);
        Assert.Equal(draftsBefore, await DraftsAsync(shopId));
    }

    // ---- review I1: publish ∥ rollback — Website có concurrency token (xmin) ----

    [Fact]
    public async Task Rollback_racing_another_write_to_website_is_409_ROLLBACK_CONFLICT_and_pointer_unchanged_I1()
    {
        var (shopId, ids) = await ThreePublicationsAsync();
        Guid websiteId;
        await using (var setup = NewDb(Cs, shopId))
        {
            websiteId = (await setup.Websites.AsNoTracking().SingleAsync()).Id;
            // Mọi draft đang bẩn ⇒ rollback chỉ ghi dòng website (đúng kịch bản review: không draft nào bị xmin chặn).
            await setup.Database.ExecuteSqlInterpolatedAsync($"UPDATE page_draft SET is_dirty = true WHERE shop_id = {shopId}");
        }

        await using var racing = new Request(Cs, shopId, null, new BumpWebsiteXminInterceptor(Cs, websiteId));
        var ex = await Assert.ThrowsAsync<ConflictException>(() => racing.Rollback(ids[0]));

        Assert.Equal("ROLLBACK_CONFLICT", ex.ErrorCode);
        Assert.Equal(409, ex.StatusCode);
        Assert.Empty(racing.PointerCache.Invalidated);
        await using var db = NewDb(Cs, shopId);
        Assert.Equal(ids[2], (await db.Websites.AsNoTracking().SingleAsync()).CurrentPublicationId);
    }

    [Fact]
    public async Task Publish_racing_another_write_to_website_is_409_PUBLISH_CONFLICT_and_nothing_is_written_I1()
    {
        var (shopId, ids) = await ThreePublicationsAsync();
        Guid websiteId;
        await using (var setup = NewDb(Cs, shopId))
        {
            websiteId = (await setup.Websites.AsNoTracking().SingleAsync()).Id;
        }

        // Draft đều sạch sau ba lần publish ⇒ publish chỉ ghi dòng website + chèn bản mới.
        await using var racing = new Request(Cs, shopId, null, new BumpWebsiteXminInterceptor(Cs, websiteId));
        var ex = await Assert.ThrowsAsync<ConflictException>(() => racing.Publish());

        Assert.Equal("PUBLISH_CONFLICT", ex.ErrorCode);
        Assert.Equal(3, (await PublicationsAsync(shopId)).Count);
        await using var db = NewDb(Cs, shopId);
        Assert.Equal(ids[2], (await db.Websites.AsNoTracking().SingleAsync()).CurrentPublicationId);
    }

    [Fact]
    public async Task Rollback_with_publication_of_another_shop_is_404_and_pointer_unchanged_R4_T5()
    {
        var (shopA, idsA) = await ThreePublicationsAsync();
        var (_, idsB) = await ThreePublicationsAsync();
        await using var request = new Request(Cs, shopA);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => request.Rollback(idsB[0]));

        Assert.Equal("PUBLICATION_NOT_FOUND", ex.ErrorCode);
        Assert.Empty(request.PointerCache.Invalidated);
        await using var db = NewDb(Cs, shopA);
        Assert.Equal(idsA[2], (await db.Websites.AsNoTracking().SingleAsync()).CurrentPublicationId);
    }

    [Fact]
    public async Task Rollback_with_random_id_is_404_and_without_website_is_WEBSITE_NOT_FOUND_T5()
    {
        var (shopId, _) = await ThreePublicationsAsync();
        await using var request = new Request(Cs, shopId);
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => request.Rollback(Guid.NewGuid()));
        Assert.Equal("PUBLICATION_NOT_FOUND", ex.ErrorCode);

        var bare = await AddShopAsync(Cs);
        await using var noWebsite = new Request(Cs, bare);
        var ex2 = await Assert.ThrowsAsync<NotFoundException>(() => noWebsite.Rollback(Guid.NewGuid()));
        Assert.Equal("WEBSITE_NOT_FOUND", ex2.ErrorCode);
    }

    [Fact]
    public async Task Rollback_does_not_change_Suspended_status_T5()
    {
        var (shopId, ids) = await ThreePublicationsAsync();
        await using (var db = NewDb(Cs, shopId))
        {
            (await db.Websites.SingleAsync()).Suspend();
            await db.SaveChangesAsync();
        }

        await using var request = new Request(Cs, shopId);
        await request.Rollback(ids[0]);

        await using var after = NewDb(Cs, shopId);
        Assert.Equal(WebsiteStatus.Suspended, (await after.Websites.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task Publish_after_rollback_uses_max_plus_one_not_reused_number_T5()
    {
        var (shopId, ids) = await ThreePublicationsAsync();
        await using (var rollback = new Request(Cs, shopId))
        {
            await rollback.Rollback(ids[0]);
        }

        await using var request = new Request(Cs, shopId);
        var dto = await request.Publish();

        Assert.Equal(4, dto.Version);
        Assert.True(dto.IsCurrent);
    }

    [Fact]
    public async Task Rollback_cache_invalidation_failure_after_commit_is_swallowed_T5()
    {
        var (shopId, ids) = await ThreePublicationsAsync();
        await using var db = NewDb(Cs, shopId);
        var handler = new Vsite.Application.Website.Commands.RollbackPublication.RollbackPublicationHandler(
            db, new Vsite.IntegrationTests.TestTenantContext(shopId), new ThrowingPointerCache(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Vsite.Application.Website.Commands.RollbackPublication.RollbackPublicationHandler>.Instance);

        var dto = await handler.Handle(new Vsite.Application.Website.Commands.RollbackPublication.RollbackPublicationCommand(ids[0]), default);

        Assert.True(dto.IsCurrent);
        await using var check = NewDb(Cs, shopId);
        Assert.Equal(ids[0], (await check.Websites.AsNoTracking().SingleAsync()).CurrentPublicationId);
    }

    // ---- WEBSITE-007 T4 (#113): publish / rollback mang menu ----

    private async Task<(bool IsDirty, uint Version, string Items)?> MenuRowAsync(Guid shopId)
    {
        await using var db = NewDb(Cs, shopId);
        var row = await db.NavigationConfigs.AsNoTracking().SingleOrDefaultAsync();
        return row is null ? null : (row.IsDirty, row.Version, row.Items);
    }

    private static JsonNode SnapshotItems(string snapshot) => JsonNode.Parse(snapshot)!["navigation"]!["items"]!;

    private static string Key(Guid pageId) => NavItem.PageKey(pageId);

    [Fact]
    public async Task Publish_without_saved_menu_puts_autogenerated_menu_in_snapshot_and_creates_a_clean_row()
    {
        var seeded = await AddWebsiteAsync(Cs);
        Assert.Null(await MenuRowAsync(seeded.ShopId));

        await using (var request = new Request(Cs, seeded.ShopId))
        {
            await request.Publish();
        }

        var snapshot = (await PublicationsAsync(seeded.ShopId)).Single().Snapshot;
        Assert.Empty(S.Schemas.ValidateSiteSnapshot(snapshot));
        // Tự sinh: mọi trang trừ NotFound, theo SortOrder rồi Title, nhãn null, không con, không hidden.
        var expectedKeys = seeded.Pages
            .Where(p => p.SystemType != SystemType.NotFound)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Title, StringComparer.Ordinal)
            .Select(p => Key(p.Id))
            .ToList();
        Assert.NotEmpty(expectedKeys);
        var items = SnapshotItems(snapshot).AsArray();
        Assert.Equal(expectedKeys, items.Select(i => i!["key"]!.GetValue<string>()).ToList());
        Assert.All(items, i =>
        {
            Assert.Equal(["key", "label"], i!.AsObject().Select(kv => kv.Key).ToArray());
            Assert.Null(i["label"]);
        });

        var row = await MenuRowAsync(seeded.ShopId);
        Assert.NotNull(row);
        Assert.False(row.Value.IsDirty);
        var stored = NavigationItems.Parse(row.Value.Items, S.Versioning);
        Assert.Equal(expectedKeys, stored.Select(i => i.Key).ToList());
        Assert.All(stored, i => Assert.False(i.Hidden));
    }

    [Fact]
    public async Task Blocked_publish_creates_no_menu_row()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await SaveTreeAsync(seeded.ShopId, seeded.Home.Id, TreeWith(HeroWithoutImage("c_hero1")));
        await using var request = new Request(Cs, seeded.ShopId);

        await Assert.ThrowsAsync<PublishBlockedException>(() => request.Publish());

        Assert.Null(await MenuRowAsync(seeded.ShopId));
    }

    [Fact]
    public async Task Publish_drops_hidden_items_disabled_pages_and_the_whole_group_of_a_disabled_parent_R5()
    {
        var seeded = await AddWebsiteAsync(Cs);
        Guid services, spa, nails, offPage, offParent, underOffParent;
        await using (var setup = new Request(Cs, seeded.ShopId))
        {
            services = (await setup.CreatePage(new CreatePageCommand("dich-vu", "Dịch vụ"))).Id;
            spa = (await setup.CreatePage(new CreatePageCommand("spa", "Spa"))).Id;
            nails = (await setup.CreatePage(new CreatePageCommand("nail", "Nail"))).Id;
            offPage = (await setup.CreatePage(new CreatePageCommand("tat", "Tắt", IsEnabled: false))).Id;
            offParent = (await setup.CreatePage(new CreatePageCommand("cha-tat", "Cha tắt", IsEnabled: false))).Id;
            underOffParent = (await setup.CreatePage(new CreatePageCommand("con-cha-tat", "Con của cha tắt"))).Id;

            await setup.SaveNavigation(
                null,
                new NavigationItemDto(Key(seeded.Home.Id), null, null, false, []),
                new NavigationItemDto(Key(services), "Dịch vụ của chúng tôi", null, false,
                [
                    new NavigationChildItemDto(Key(spa), null, null, false,
                    [
                        new NavigationGrandchildItemDto(Key(nails), "Làm móng", null, false),
                        new NavigationGrandchildItemDto(Key(offPage), null, null, false), // trang tắt ở cấp 3 ⇒ bỏ
                        new NavigationGrandchildItemDto(NavItem.CustomKey, "Bảng giá", "https://example.vn/gia", false),
                    ]),
                    new NavigationChildItemDto(NavItem.CustomKey, "Ẩn", "https://example.vn/an", true, []), // ẩn ⇒ bỏ
                ]),
                new NavigationItemDto(Key(offParent), null, null, false, // cha tắt ⇒ bỏ cả nhóm
                [
                    new NavigationChildItemDto(Key(underOffParent), null, null, false, []),
                ]),
                new NavigationItemDto(Key(seeded.Contact.Id), null, null, true, []), // ẩn ⇒ bỏ
                new NavigationItemDto(NavItem.CustomKey, "Gọi ngay", "tel:0901234567", false, []));
        }

        var before = await MenuRowAsync(seeded.ShopId);
        Assert.True(before!.Value.IsDirty);

        await using (var request = new Request(Cs, seeded.ShopId))
        {
            await request.Publish();
        }

        var snapshot = (await PublicationsAsync(seeded.ShopId)).Single().Snapshot;
        Assert.Empty(S.Schemas.ValidateSiteSnapshot(snapshot));
        var expected = new JsonArray
        {
            new JsonObject { ["key"] = Key(seeded.Home.Id), ["label"] = null },
            new JsonObject
            {
                ["key"] = Key(services),
                ["label"] = "Dịch vụ của chúng tôi",
                ["children"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["key"] = Key(spa),
                        ["label"] = null,
                        ["children"] = new JsonArray
                        {
                            new JsonObject { ["key"] = Key(nails), ["label"] = "Làm móng" },
                            new JsonObject { ["key"] = "custom", ["label"] = "Bảng giá", ["url"] = "https://example.vn/gia" },
                        },
                    },
                },
            },
            new JsonObject { ["key"] = "custom", ["label"] = "Gọi ngay", ["url"] = "tel:0901234567" },
        };
        var items = SnapshotItems(snapshot);
        Assert.True(JsonNode.DeepEquals(expected, items), items.ToJsonString());
        // Snapshot công khai không mang id trang tắt (kể cả con của cha tắt — trang đó bật nên có trong pages, nhưng không trong menu).
        Assert.DoesNotContain(offPage.ToString(), snapshot);
        Assert.DoesNotContain(offParent.ToString(), snapshot);
        Assert.DoesNotContain(underOffParent.ToString(), items.ToJsonString());

        // Dòng menu hạ cờ nhưng nội dung đang sửa giữ nguyên (vẫn còn mục ẩn / trang tắt).
        var after = await MenuRowAsync(seeded.ShopId);
        Assert.False(after!.Value.IsDirty);
        Assert.Equal(before.Value.Items, after.Value.Items);
    }

    [Fact]
    public async Task Page_disabled_after_saving_the_menu_is_dropped_from_the_snapshot_R5()
    {
        var seeded = await AddWebsiteAsync(Cs);
        Guid about;
        await using (var setup = new Request(Cs, seeded.ShopId))
        {
            about = (await setup.CreatePage(new CreatePageCommand("gioi-thieu", "Giới thiệu"))).Id;
            await setup.SaveNavigation(
                null,
                new NavigationItemDto(Key(seeded.Home.Id), null, null, false, []),
                new NavigationItemDto(Key(about), null, null, false, []));
            await setup.UpdatePage(new UpdatePageCommand(about, "gioi-thieu", "Giới thiệu", null, null, null, false, IsEnabled: false));
        }

        await using (var request = new Request(Cs, seeded.ShopId))
        {
            await request.Publish();
        }

        var items = SnapshotItems((await PublicationsAsync(seeded.ShopId)).Single().Snapshot).AsArray();
        Assert.Equal([Key(seeded.Home.Id)], items.Select(i => i!["key"]!.GetValue<string>()).ToArray());
    }

    [Fact]
    public async Task Second_publish_with_a_clean_menu_does_not_rewrite_the_row()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using (var first = new Request(Cs, seeded.ShopId))
        {
            await first.Publish();
        }

        var afterFirst = await MenuRowAsync(seeded.ShopId);
        await using (var second = new Request(Cs, seeded.ShopId))
        {
            await second.Publish();
        }

        var afterSecond = await MenuRowAsync(seeded.ShopId);
        Assert.False(afterSecond!.Value.IsDirty);
        Assert.Equal(afterFirst!.Value.Version, afterSecond.Value.Version);
        var all = await PublicationsAsync(seeded.ShopId);
        Assert.True(JsonNode.DeepEquals(SnapshotItems(all[0].Snapshot), SnapshotItems(all[1].Snapshot)));
    }

    [Fact]
    public async Task First_menu_PUT_racing_publish_insert_is_409_PUBLISH_CONFLICT_and_nothing_of_publish_is_written()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var racing = new Request(Cs, seeded.ShopId, null, new InsertNavigationInterceptor(Cs, seeded.ShopId, seeded.Website.Id));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => racing.Publish());

        Assert.Equal("PUBLISH_CONFLICT", ex.ErrorCode);
        Assert.Equal(409, ex.StatusCode);
        Assert.Empty(await PublicationsAsync(seeded.ShopId));
        var row = await MenuRowAsync(seeded.ShopId);
        Assert.True(row!.Value.IsDirty); // chỉ dòng của "tab kia"
        Assert.Empty(NavigationItems.Parse(row.Value.Items, S.Versioning));
        Assert.All((await DraftsAsync(seeded.ShopId)).Values, d => Assert.True(d.IsDirty));
    }

    [Fact]
    public async Task Menu_saved_between_publish_read_and_write_is_409_PUBLISH_CONFLICT_and_menu_stays_dirty()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using (var setup = new Request(Cs, seeded.ShopId))
        {
            await setup.SaveNavigation(null, new NavigationItemDto(Key(seeded.Home.Id), null, null, false, []));
        }

        await using var racing = new Request(Cs, seeded.ShopId, null, new BumpNavigationXminInterceptor(Cs, seeded.Website.Id));
        var ex = await Assert.ThrowsAsync<ConflictException>(() => racing.Publish());

        Assert.Equal("PUBLISH_CONFLICT", ex.ErrorCode);
        Assert.Empty(await PublicationsAsync(seeded.ShopId));
        Assert.True((await MenuRowAsync(seeded.ShopId))!.Value.IsDirty);
    }

    [Fact]
    public async Task Rollback_marks_the_menu_dirty_and_keeps_its_items_Flow6()
    {
        var (shopId, ids) = await ThreePublicationsAsync();
        var before = await MenuRowAsync(shopId);
        Assert.False(before!.Value.IsDirty);

        await using (var request = new Request(Cs, shopId))
        {
            await request.Rollback(ids[0]);
        }

        var after = await MenuRowAsync(shopId);
        Assert.True(after!.Value.IsDirty);
        Assert.Equal(before.Value.Items, after.Value.Items);
    }

    [Fact]
    public async Task Rollback_to_a_snapshot_from_before_step_7_without_menu_row_works_and_creates_no_row_R6()
    {
        var (shopId, ids) = await ThreePublicationsAsync();
        await using (var setup = NewDb(Cs, shopId))
        {
            // Website publish trước Bước 7: bản cũ có `items: []` và chưa có dòng menu.
            await setup.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM navigation_config WHERE shop_id = {shopId}");
            await setup.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE site_publication SET snapshot = jsonb_set(snapshot, '{{navigation,items}}', '[]'::jsonb) WHERE id = {ids[0]}");
        }

        await using (var request = new Request(Cs, shopId))
        {
            var dto = await request.Rollback(ids[0]);
            Assert.True(dto.IsCurrent);
        }

        Assert.Null(await MenuRowAsync(shopId));
        var old = (await PublicationsAsync(shopId))[0].Snapshot;
        Assert.Empty(SnapshotItems(old).AsArray());
        Assert.Empty(S.Schemas.ValidateSiteSnapshot(old));
    }

    private sealed class ThrowingPointerCache : Vsite.Application.Website.Interfaces.ISitePointerCache
    {
        public Task<Vsite.Application.Website.Interfaces.SitePointer?> GetAsync(Guid shopId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("redis down");

        public Task SetAsync(Guid shopId, Vsite.Application.Website.Interfaces.SitePointer pointer, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("redis down");

        public Task InvalidateAsync(Guid shopId, CancellationToken cancellationToken) => throw new InvalidOperationException("redis down");
    }
}
