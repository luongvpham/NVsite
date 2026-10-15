using Microsoft.EntityFrameworkCore;
using Vsite.Domain.Shop.Entities;
using Vsite.Domain.Shop.Enums;
using Vsite.Domain.Website.Entities;
using Vsite.Domain.Website.Enums;
using Vsite.Infrastructure.Persistence;
using WebsiteEntity = Vsite.Domain.Website.Entities.Website;

namespace Vsite.IntegrationTests.Website;

/// <summary>
/// Global Query Filter (Quyết định #21.2) trên Website/Theme/Page/PageDraft — test bắt buộc theo
/// backend/CLAUDE.md: "query từ shop A không thấy dữ liệu shop B"; tenant null → rỗng (fail-closed). Cần Docker.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class WebsiteTenantIsolationTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private readonly Guid _shopAId = Guid.NewGuid();
    private readonly Guid _shopBId = Guid.NewGuid();
    private WebsiteEntity _websiteA = null!;
    private WebsiteEntity _websiteB = null!;
    private Page _pageA = null!;
    private Page _pageB = null!;

    public WebsiteTenantIsolationTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        await using var db = CreateContext(new TestTenantContext());
        await db.Database.MigrateAsync();

        db.Shops.AddRange(
            new Shop(_shopAId, "Shop A", $"shop-a-{_shopAId:N}", ShopKind.Hosted),
            new Shop(_shopBId, "Shop B", $"shop-b-{_shopBId:N}", ShopKind.Hosted));
        await db.SaveChangesAsync();

        _websiteA = WebsiteEntity.Create(_shopAId);
        _websiteB = WebsiteEntity.Create(_shopBId);
        db.Websites.AddRange(_websiteA, _websiteB);
        await db.SaveChangesAsync();

        _pageA = Page.NewComposable(_shopAId, _websiteA.Id, "", "Trang chủ A", 0);
        _pageB = Page.NewComposable(_shopBId, _websiteB.Id, "", "Trang chủ B", 0);
        db.Pages.AddRange(_pageA, _pageB);
        db.Themes.AddRange(
            Theme.Create(_websiteA.Id, _shopAId, """{"schemaVersion":1}"""),
            Theme.Create(_websiteB.Id, _shopBId, """{"schemaVersion":1}"""));
        db.PageDrafts.AddRange(
            PageDraft.Create(_shopAId, _pageA.Id, """{"schemaVersion":1,"id":"root","type":"Page","children":[]}"""),
            PageDraft.Create(_shopBId, _pageB.Id, """{"schemaVersion":1,"id":"root","type":"Page","children":[]}"""));
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask; // mọi id đều là GUID mới — không chạm dữ liệu test khác

    [Fact]
    public async Task Queries_scoped_to_shop_A_do_not_see_shop_B_rows()
    {
        await using var db = CreateContext(new TestTenantContext(_shopAId));

        var websites = await db.Websites.Where(w => w.Id == _websiteA.Id || w.Id == _websiteB.Id).ToListAsync();
        var themes = await db.Themes.Where(t => t.Id == _websiteA.Id || t.Id == _websiteB.Id).ToListAsync();
        var pages = await db.Pages.Where(p => p.Id == _pageA.Id || p.Id == _pageB.Id).ToListAsync();
        var drafts = await db.PageDrafts.Where(d => d.Id == _pageA.Id || d.Id == _pageB.Id).ToListAsync();

        Assert.Equal(_shopAId, Assert.Single(websites).ShopId);
        Assert.Equal(_shopAId, Assert.Single(themes).ShopId);
        Assert.Equal(_shopAId, Assert.Single(pages).ShopId);
        Assert.Equal(_shopAId, Assert.Single(drafts).ShopId);
    }

    [Fact]
    public async Task Shop_A_cannot_read_shop_B_page_draft_by_id()
    {
        await using var db = CreateContext(new TestTenantContext(_shopAId));

        Assert.Null(await db.PageDrafts.SingleOrDefaultAsync(d => d.Id == _pageB.Id));
        Assert.Null(await db.Pages.SingleOrDefaultAsync(p => p.Id == _pageB.Id));
    }

    [Fact]
    public async Task Query_with_unresolved_tenant_sees_nothing_fail_closed()
    {
        await using var db = CreateContext(new TestTenantContext(shopId: null));

        Assert.Empty(await db.Websites.Where(w => w.Id == _websiteA.Id || w.Id == _websiteB.Id).ToListAsync());
        Assert.Empty(await db.Themes.Where(t => t.Id == _websiteA.Id || t.Id == _websiteB.Id).ToListAsync());
        Assert.Empty(await db.Pages.Where(p => p.Id == _pageA.Id || p.Id == _pageB.Id).ToListAsync());
        Assert.Empty(await db.PageDrafts.Where(d => d.Id == _pageA.Id || d.Id == _pageB.Id).ToListAsync());
    }

    [Fact]
    public async Task Soft_deleted_page_is_hidden_by_the_global_filter()
    {
        await using var db = CreateContext(new TestTenantContext(_shopAId));
        var extra = Page.NewComposable(_shopAId, _websiteA.Id, "tam-xoa", "Tạm xoá", 5);
        db.Pages.Add(extra);
        await db.SaveChangesAsync();

        extra.SoftDelete();
        await db.SaveChangesAsync();

        await using var verify = CreateContext(new TestTenantContext(_shopAId));
        Assert.Null(await verify.Pages.SingleOrDefaultAsync(p => p.Id == extra.Id));
    }

    private AppDbContext CreateContext(TestTenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_postgres.ConnectionString).Options;
        return new AppDbContext(options, tenantContext);
    }
}
