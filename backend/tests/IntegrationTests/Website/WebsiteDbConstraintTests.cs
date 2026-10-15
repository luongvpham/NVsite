using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vsite.Domain.Media.Entities;
using Vsite.Domain.Shop.Entities;
using Vsite.Domain.Shop.Enums;
using Vsite.Domain.Website.Entities;
using Vsite.Domain.Website.Enums;
using Vsite.Infrastructure.Persistence;
using WebsiteEntity = Vsite.Domain.Website.Entities.Website;

namespace Vsite.IntegrationTests.Website;

/// <summary>
/// T3, WEBSITE-001 (#92) — ràng buộc phải nằm ở DB (Quyết định #17). Mỗi test phải FAIL THẬT khi bị vi phạm
/// (Postgres thật, Testcontainers). Cần Docker.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class WebsiteDbConstraintTests(PostgresFixture postgres)
{
    private const string FkViolation = "23503";
    private const string UniqueViolation = "23505";
    private const string CheckViolation = "23514";

    // ---------------------------------------------------------------- website

    [Fact]
    public async Task Only_one_website_per_shop()
    {
        await using var db = await NewDbAsync();
        var shop = await AddShopAsync(db);
        db.Websites.Add(WebsiteEntity.Create(shop.Id));
        await db.SaveChangesAsync();

        db.Websites.Add(WebsiteEntity.Create(shop.Id));
        var ex = await AssertPgAsync(db, UniqueViolation);

        Assert.Equal("ux_website_shop", ex.ConstraintName);
    }

    [Fact]
    public async Task Check_constraint_rejects_default_locale_outside_locales()
    {
        await using var db = await NewDbAsync();
        var shop = await AddShopAsync(db);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO website (id, shop_id, status, default_locale, locales, tracking_snippets, created_at, is_deleted)
            VALUES ({Guid.NewGuid()}, {shop.Id}, 'Draft', 'en', ARRAY['vi'], CAST({"{}"} AS jsonb), now(), false)
            """));

        Assert.Equal("ck_website_default_locale", ex.ConstraintName);
    }

    [Fact]
    public async Task Locales_roundtrip_as_text_array_and_tracking_as_jsonb()
    {
        await using var db = await NewDbAsync();
        var shop = await AddShopAsync(db);
        var website = WebsiteEntity.Create(shop.Id, "en", ["vi", "en"]);
        website.SetTrackingSnippets("""{"ga4":"G-ABCDEF1"}""");
        db.Websites.Add(website);
        await db.SaveChangesAsync();

        await using var verify = NewContext(shop.Id);
        var loaded = await verify.Websites.SingleAsync(w => w.Id == website.Id);

        Assert.Equal(["vi", "en"], loaded.Locales);
        Assert.Equal("en", loaded.DefaultLocale);
        var jsonType = await verify.Database.SqlQuery<string>(
            $"SELECT data_type AS \"Value\" FROM information_schema.columns WHERE table_name = 'website' AND column_name = 'tracking_snippets'")
            .SingleAsync();
        Assert.Equal("jsonb", jsonType);
    }

    [Theory]
    [InlineData("default_og_image_id", "fk_website_media_asset_default_og_image")]
    [InlineData("favicon_asset_id", "fk_website_media_asset_favicon")]
    public async Task Composite_fk_blocks_website_image_from_another_shop(string column, string constraint)
    {
        await using var db = await NewDbAsync();
        var shopA = await AddShopAsync(db);
        var shopB = await AddShopAsync(db);
        var assetOfB = await AddLibraryAssetAsync(db, shopB.Id);

        var website = WebsiteEntity.Create(shopA.Id);
        if (column == "default_og_image_id")
        {
            website.SetDefaultOgImage(assetOfB.Id);
        }
        else
        {
            website.SetFavicon(assetOfB.Id);
        }

        db.Websites.Add(website);
        var ex = await AssertPgAsync(db, FkViolation);

        Assert.Equal(constraint, ex.ConstraintName);
    }

    [Fact]
    public async Task Website_images_of_the_same_shop_are_accepted_and_set_null_on_asset_hard_delete_keeps_shop_id()
    {
        await using var db = await NewDbAsync();
        var shop = await AddShopAsync(db);
        var asset = await AddLibraryAssetAsync(db, shop.Id);
        var favicon = await AddLibraryAssetAsync(db, shop.Id);
        var website = WebsiteEntity.Create(shop.Id);
        website.SetDefaultOgImage(asset.Id);
        website.SetFavicon(favicon.Id);
        db.Websites.Add(website);
        await db.SaveChangesAsync();

        // ON DELETE SET NULL (cột) — Postgres 15+: chỉ nulling cột ảnh, KHÔNG đụng shop_id (NOT NULL).
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM media_asset WHERE id = {asset.Id}");

        await using var verify = NewContext(shop.Id);
        var loaded = await verify.Websites.SingleAsync(w => w.Id == website.Id);
        Assert.Null(loaded.DefaultOgImageId);
        Assert.Equal(favicon.Id, loaded.FaviconAssetId); // cột còn lại không bị đụng
        Assert.Equal(shop.Id, loaded.ShopId);
    }

    // ---------------------------------------------------------------- theme

    [Fact]
    public async Task Theme_composite_fk_blocks_pointing_at_website_of_another_shop()
    {
        await using var db = await NewDbAsync();
        var shopA = await AddShopAsync(db);
        var shopB = await AddShopAsync(db);
        var website = WebsiteEntity.Create(shopA.Id);
        db.Websites.Add(website);
        await db.SaveChangesAsync();

        db.Themes.Add(Theme.Create(website.Id, shopB.Id, """{"schemaVersion":1}"""));
        var ex = await AssertPgAsync(db, FkViolation);

        Assert.Equal("fk_theme_website", ex.ConstraintName);
    }

    [Fact]
    public async Task Theme_is_one_to_one_with_website_and_tokens_are_jsonb()
    {
        await using var db = await NewDbAsync();
        var shop = await AddShopAsync(db);
        var website = WebsiteEntity.Create(shop.Id);
        db.Websites.Add(website);
        db.Themes.Add(Theme.Create(website.Id, shop.Id, """{"schemaVersion":1,"colors":{"primary":"#7c3aed"}}""", "spa-lavender"));
        await db.SaveChangesAsync();

        await using var db2 = NewContext(null);
        db2.Themes.Add(Theme.Create(website.Id, shop.Id, "{}"));
        var ex = await AssertPgAsync(db2, UniqueViolation); // trùng PK website_id
        Assert.Equal("pk_theme", ex.ConstraintName);

        await using var verify = NewContext(shop.Id);
        var theme = await verify.Themes.SingleAsync(t => t.Id == website.Id);
        Assert.Contains("#7c3aed", theme.Tokens);
    }

    // ---------------------------------------------------------------- page

    [Fact]
    public async Task Page_composite_fk_blocks_pointing_at_website_of_another_shop()
    {
        await using var db = await NewDbAsync();
        var shopA = await AddShopAsync(db);
        var shopB = await AddShopAsync(db);
        var website = WebsiteEntity.Create(shopA.Id);
        db.Websites.Add(website);
        await db.SaveChangesAsync();

        db.Pages.Add(Page.NewComposable(shopB.Id, website.Id, "gioi-thieu", "Giới thiệu", 1));
        var ex = await AssertPgAsync(db, FkViolation);

        Assert.Equal("fk_page_website", ex.ConstraintName);
    }

    [Fact]
    public async Task Page_og_image_composite_fk_blocks_asset_of_another_shop()
    {
        await using var db = await NewDbAsync();
        var (shopA, website) = await AddWebsiteAsync(db);
        var shopB = await AddShopAsync(db);
        var assetOfB = await AddLibraryAssetAsync(db, shopB.Id);

        var page = Page.NewComposable(shopA.Id, website.Id, "gioi-thieu", "Giới thiệu", 1);
        page.SetOgImage(assetOfB.Id);
        db.Pages.Add(page);
        var ex = await AssertPgAsync(db, FkViolation);

        Assert.Equal("fk_page_media_asset_og_image", ex.ConstraintName);
    }

    [Fact]
    public async Task Page_og_image_set_null_on_asset_hard_delete_keeps_shop_id()
    {
        await using var db = await NewDbAsync();
        var (shop, website) = await AddWebsiteAsync(db);
        var asset = await AddLibraryAssetAsync(db, shop.Id);
        var page = Page.NewComposable(shop.Id, website.Id, "gioi-thieu", "Giới thiệu", 1);
        page.SetOgImage(asset.Id);
        db.Pages.Add(page);
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM media_asset WHERE id = {asset.Id}");

        await using var verify = NewContext(shop.Id);
        var loaded = await verify.Pages.SingleAsync(p => p.Id == page.Id);
        Assert.Null(loaded.OgImageId);
        Assert.Equal(shop.Id, loaded.ShopId);
    }

    [Fact]
    public async Task Slug_is_unique_per_website_among_live_pages_and_reusable_after_soft_delete()
    {
        await using var db = await NewDbAsync();
        var (shop, website) = await AddWebsiteAsync(db);

        var first = Page.NewComposable(shop.Id, website.Id, "gioi-thieu", "Giới thiệu", 1);
        db.Pages.Add(first);
        await db.SaveChangesAsync();

        db.Pages.Add(Page.NewComposable(shop.Id, website.Id, "gioi-thieu", "Trùng", 2));
        var ex = await AssertPgAsync(db, UniqueViolation);
        Assert.Equal("ux_page_slug", ex.ConstraintName);

        // Xoá mềm rồi tạo lại cùng slug → OK (G10, R13).
        await using var db2 = NewContext(shop.Id);
        var tracked = await db2.Pages.SingleAsync(p => p.Id == first.Id);
        tracked.SoftDelete();
        await db2.SaveChangesAsync();
        db2.Pages.Add(Page.NewComposable(shop.Id, website.Id, "gioi-thieu", "Tạo lại", 3));
        await db2.SaveChangesAsync();
    }

    [Fact]
    public async Task Same_slug_is_allowed_in_different_websites()
    {
        await using var db = await NewDbAsync();
        var (shopA, websiteA) = await AddWebsiteAsync(db);
        var (shopB, websiteB) = await AddWebsiteAsync(db);

        db.Pages.Add(Page.NewComposable(shopA.Id, websiteA.Id, "gioi-thieu", "A", 1));
        db.Pages.Add(Page.NewComposable(shopB.Id, websiteB.Id, "gioi-thieu", "B", 1));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Home_page_slug_empty_is_unique_per_website()
    {
        await using var db = await NewDbAsync();
        var (shop, website) = await AddWebsiteAsync(db);
        db.Pages.Add(Page.NewComposable(shop.Id, website.Id, "", "Trang chủ", 0));
        await db.SaveChangesAsync();

        db.Pages.Add(Page.NewComposable(shop.Id, website.Id, "", "Trang chủ 2", 1));
        var ex = await AssertPgAsync(db, UniqueViolation);

        Assert.Equal("ux_page_slug", ex.ConstraintName);
    }

    [Fact]
    public async Task Null_slug_of_NotFound_does_not_collide_in_slug_unique_but_system_type_is_unique()
    {
        await using var db = await NewDbAsync();
        var (shop, website) = await AddWebsiteAsync(db);
        db.Pages.Add(Page.NewSystem(shop.Id, website.Id, SystemType.NotFound, null, "404", 1));
        db.Pages.Add(Page.NewSystem(shop.Id, website.Id, SystemType.Contact, "lien-he", "Liên hệ", 2));
        await db.SaveChangesAsync();

        // Hai Contact (slug khác nhau) → vi phạm ux_page_system_type.
        db.Pages.Add(Page.NewSystem(shop.Id, website.Id, SystemType.Contact, "lien-he-2", "Liên hệ 2", 3));
        var ex = await AssertPgAsync(db, UniqueViolation);

        Assert.Equal("ux_page_system_type", ex.ConstraintName);
    }

    [Fact]
    public async Task Check_constraint_rejects_system_kind_without_system_type_and_composable_with_one()
    {
        await using var db = await NewDbAsync();
        var (shop, website) = await AddWebsiteAsync(db);

        var ex1 = await Assert.ThrowsAsync<PostgresException>(() => InsertPageSqlAsync(db, shop.Id, website.Id, "System", null, "x1"));
        Assert.Equal("ck_page_kind_system_type", ex1.ConstraintName);

        var ex2 = await Assert.ThrowsAsync<PostgresException>(() => InsertPageSqlAsync(db, shop.Id, website.Id, "Composable", "Contact", "x2"));
        Assert.Equal("ck_page_kind_system_type", ex2.ConstraintName);
    }

    [Fact]
    public async Task Check_constraint_rejects_null_slug_unless_NotFound_and_NotFound_with_slug()
    {
        await using var db = await NewDbAsync();
        var (shop, website) = await AddWebsiteAsync(db);

        var ex1 = await Assert.ThrowsAsync<PostgresException>(() => InsertPageSqlAsync(db, shop.Id, website.Id, "System", "Contact", null));
        Assert.Equal("ck_page_slug_not_found", ex1.ConstraintName);

        var ex2 = await Assert.ThrowsAsync<PostgresException>(() => InsertPageSqlAsync(db, shop.Id, website.Id, "System", "NotFound", "404"));
        Assert.Equal("ck_page_slug_not_found", ex2.ConstraintName);

        var ex3 = await Assert.ThrowsAsync<PostgresException>(() => InsertPageSqlAsync(db, shop.Id, website.Id, "Composable", null, null));
        Assert.Equal("ck_page_slug_not_found", ex3.ConstraintName);
    }

    // ---------------------------------------------------------------- page_draft

    [Fact]
    public async Task Draft_composite_fk_blocks_pointing_at_page_of_another_shop()
    {
        await using var db = await NewDbAsync();
        var (shopA, website) = await AddWebsiteAsync(db);
        var shopB = await AddShopAsync(db);
        var page = Page.NewComposable(shopA.Id, website.Id, "", "Trang chủ", 0);
        db.Pages.Add(page);
        await db.SaveChangesAsync();

        db.PageDrafts.Add(PageDraft.Create(shopB.Id, page.Id, EmptyTree));
        var ex = await AssertPgAsync(db, FkViolation);

        Assert.Equal("fk_page_draft_page", ex.ConstraintName);
    }

    [Fact]
    public async Task Draft_tree_is_jsonb_and_is_one_to_one_with_page()
    {
        await using var db = await NewDbAsync();
        var (shop, website) = await AddWebsiteAsync(db);
        var page = Page.NewComposable(shop.Id, website.Id, "", "Trang chủ", 0);
        db.Pages.Add(page);
        db.PageDrafts.Add(PageDraft.Create(shop.Id, page.Id, EmptyTree));
        await db.SaveChangesAsync();

        var type = await db.Database.SqlQuery<string>(
            $"SELECT data_type AS \"Value\" FROM information_schema.columns WHERE table_name = 'page_draft' AND column_name = 'tree'")
            .SingleAsync();
        Assert.Equal("jsonb", type);

        await using var db2 = NewContext(null);
        db2.PageDrafts.Add(PageDraft.Create(shop.Id, page.Id, EmptyTree));
        var ex = await AssertPgAsync(db2, UniqueViolation); // trùng PK page_id
        Assert.Equal("pk_page_draft", ex.ConstraintName);
    }

    [Fact]
    public async Task Draft_xmin_version_changes_on_update_and_stale_write_throws_concurrency()
    {
        await using var seed = await NewDbAsync();
        var (shop, website) = await AddWebsiteAsync(seed);
        var page = Page.NewComposable(shop.Id, website.Id, "", "Trang chủ", 0);
        seed.Pages.Add(page);
        seed.PageDrafts.Add(PageDraft.Create(shop.Id, page.Id, EmptyTree));
        await seed.SaveChangesAsync();

        await using var tabA = NewContext(shop.Id);
        await using var tabB = NewContext(shop.Id);
        var draftA = await tabA.PageDrafts.SingleAsync(d => d.Id == page.Id);
        var draftB = await tabB.PageDrafts.SingleAsync(d => d.Id == page.Id);
        var v1 = draftA.Version;
        Assert.NotEqual(0u, v1);
        Assert.Equal(v1, draftB.Version);

        draftB.Replace("""{"schemaVersion":1,"id":"root","type":"Page","children":[{"id":"c_abcde","type":"Hero"}]}""");
        await tabB.SaveChangesAsync();
        Assert.NotEqual(v1, draftB.Version); // xmin được nạp lại sau UPDATE

        // Tab cũ giữ token cũ → ghi đè bị chặn.
        draftA.Replace("""{"schemaVersion":1,"id":"root","type":"Page","children":[]}""");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => tabA.SaveChangesAsync());

        await using var verify = NewContext(shop.Id);
        var loaded = await verify.PageDrafts.SingleAsync(d => d.Id == page.Id);
        Assert.Contains("c_abcde", loaded.Tree);
        Assert.Equal(draftB.Version, loaded.Version);
    }

    // ---------------------------------------------------------------- helpers

    private const string EmptyTree = """{"schemaVersion":1,"id":"root","type":"Page","children":[]}""";

    private static async Task<PostgresException> AssertPgAsync(AppDbContext db, string sqlState)
    {
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(sqlState, pg.SqlState);
        return pg;
    }

    private static Task<int> InsertPageSqlAsync(AppDbContext db, Guid shopId, Guid websiteId, string kind, string? systemType, string? slug) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO page (id, shop_id, website_id, kind, system_type, slug, title, no_index, is_enabled, sort_order, created_at, is_deleted)
            VALUES ({Guid.NewGuid()}, {shopId}, {websiteId}, {kind}, {systemType}, {slug}, 't', false, true, 0, now(), false)
            """);

    private async Task<AppDbContext> NewDbAsync()
    {
        var db = NewContext(null);
        await db.Database.MigrateAsync();
        return db;
    }

    private AppDbContext NewContext(Guid? shopId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(postgres.ConnectionString).Options;
        return new AppDbContext(options, new TestTenantContext(shopId));
    }

    private static async Task<Shop> AddShopAsync(AppDbContext db)
    {
        var id = Guid.NewGuid();
        var shop = new Shop(id, "Test Shop", $"test-shop-{id:N}", ShopKind.Hosted);
        db.Shops.Add(shop);
        await db.SaveChangesAsync();
        return shop;
    }

    private static async Task<(Shop Shop, WebsiteEntity Website)> AddWebsiteAsync(AppDbContext db)
    {
        var shop = await AddShopAsync(db);
        var website = WebsiteEntity.Create(shop.Id);
        db.Websites.Add(website);
        await db.SaveChangesAsync();
        return (shop, website);
    }

    private static async Task<MediaAsset> AddLibraryAssetAsync(AppDbContext db, Guid shopId)
    {
        var asset = MediaAsset.NewLibrary(
            shopId, $"shops/{shopId}/library/{Guid.NewGuid():N}.webp", 100, 100, 10, 0.5f, 0.5f, null, null);
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync();
        return asset;
    }
}
