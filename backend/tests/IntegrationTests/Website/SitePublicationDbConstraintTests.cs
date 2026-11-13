using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vsite.Domain.Shop.Entities;
using Vsite.Domain.Shop.Enums;
using Vsite.Domain.Website.Entities;
using Vsite.Infrastructure.Persistence;
using WebsiteEntity = Vsite.Domain.Website.Entities.Website;

namespace Vsite.IntegrationTests.Website;

/// <summary>
/// T1, WEBSITE-004 (#92, R4) — ràng buộc DB của <c>site_publication</c> và con trỏ
/// <c>website.current_publication_id</c>. Mỗi test FAIL THẬT khi bị vi phạm (Postgres thật, Testcontainers).
/// Cần Docker.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SitePublicationDbConstraintTests(PostgresFixture postgres)
{
    private const string FkViolation = "23503";
    private const string UniqueViolation = "23505";

    private const string Snapshot = """{"schemaVersion":1,"version":1,"pages":[]}""";

    [Fact]
    public async Task Version_is_unique_per_website()
    {
        await using var db = await NewDbAsync();
        var (shop, website) = await AddWebsiteAsync(db);

        db.SitePublications.Add(NewPublication(shop.Id, website.Id, 1));
        await db.SaveChangesAsync();

        db.SitePublications.Add(NewPublication(shop.Id, website.Id, 1));
        var ex = await AssertPgAsync(db, UniqueViolation);

        Assert.Equal("ux_site_publication_website_version", ex.ConstraintName);
    }

    [Fact]
    public async Task Composite_fk_blocks_publication_shop_id_differing_from_website_shop_id()
    {
        await using var db = await NewDbAsync();
        var (_, website) = await AddWebsiteAsync(db);
        var shopB = await AddShopAsync(db);

        // Bản mang shop_id của shop B nhưng trỏ website của shop A → FK ghép (website_id, shop_id) từ chối.
        db.SitePublications.Add(NewPublication(shopB.Id, website.Id, 1));
        var ex = await AssertPgAsync(db, FkViolation);

        Assert.Equal("fk_site_publication_website", ex.ConstraintName);
    }

    [Fact]
    public async Task Check_constraint_rejects_non_positive_version()
    {
        await using var db = await NewDbAsync();
        var (shop, website) = await AddWebsiteAsync(db);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO site_publication (id, shop_id, website_id, version, snapshot, note, published_at)
            VALUES ({Guid.NewGuid()}, {shop.Id}, {website.Id}, 0, CAST({Snapshot} AS jsonb), NULL, now())
            """));

        Assert.Equal("ck_site_publication_version_positive", ex.ConstraintName);
    }

    [Fact]
    public async Task Current_publication_pointer_rejects_publication_of_another_website()
    {
        await using var db = await NewDbAsync();
        var (shopA, websiteA) = await AddWebsiteAsync(db);
        var (_, websiteB) = await AddWebsiteAsync(db);

        var pubB = NewPublication(websiteB.ShopId, websiteB.Id, 1);
        db.SitePublications.Add(pubB);
        await db.SaveChangesAsync();

        // Trỏ con trỏ của website A sang bản của website B qua SQL thô (bỏ qua domain guard) → FK ghép từ chối (R4).
        var ex = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE website SET current_publication_id = {pubB.Id} WHERE id = {websiteA.Id}"));

        Assert.Equal("fk_website_current_publication", ex.ConstraintName);
    }

    [Fact]
    public async Task Current_publication_of_same_website_is_accepted()
    {
        await using var db = await NewDbAsync();
        var (shop, website) = await AddWebsiteAsync(db);
        var pub = NewPublication(shop.Id, website.Id, 1);
        db.SitePublications.Add(pub);
        await db.SaveChangesAsync();

        website.Publish(pub);
        await db.SaveChangesAsync();

        await using var verify = NewContext(shop.Id);
        var loaded = await verify.Websites.SingleAsync(w => w.Id == website.Id);
        Assert.Equal(pub.Id, loaded.CurrentPublicationId);
    }

    [Fact]
    public async Task Publication_that_is_current_cannot_be_deleted_but_a_non_current_one_can()
    {
        await using var db = await NewDbAsync();
        var (shop, website) = await AddWebsiteAsync(db);
        var pub1 = NewPublication(shop.Id, website.Id, 1);
        var pub2 = NewPublication(shop.Id, website.Id, 2);
        db.SitePublications.AddRange(pub1, pub2);
        await db.SaveChangesAsync();

        website.Publish(pub2); // current = pub2
        await db.SaveChangesAsync();

        // Xoá bản đang là current → FK Restrict từ chối.
        var ex = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM site_publication WHERE id = {pub2.Id}"));
        Assert.Equal(FkViolation, ex.SqlState);
        Assert.Equal("fk_website_current_publication", ex.ConstraintName);

        // Bản không current → xoá được.
        var deleted = await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM site_publication WHERE id = {pub1.Id}");
        Assert.Equal(1, deleted);
    }

    // ---------------------------------------------------------------- helpers

    private static SitePublication NewPublication(Guid shopId, Guid websiteId, int version) =>
        SitePublication.Create(shopId, websiteId, version, Snapshot, null, null, DateTimeOffset.UtcNow);

    private static async Task<PostgresException> AssertPgAsync(AppDbContext db, string sqlState)
    {
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(sqlState, pg.SqlState);
        return pg;
    }

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
}
