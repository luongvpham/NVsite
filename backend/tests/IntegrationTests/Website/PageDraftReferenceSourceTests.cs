using Vsite.Application.Media.Dtos;
using Vsite.Application.Website.Commands.CreatePage;
using Vsite.Domain.Media.Entities;
using Vsite.Infrastructure.Website;
using static Vsite.IntegrationTests.Website.WebsiteHandlerKit;

namespace Vsite.IntegrationTests.Website;

/// <summary>
/// T11 (WEBSITE-001, MEDIA-001 #2, R13/R14) — <see cref="PageDraftReferenceSource"/> trên Postgres thật, qua handler lưu draft thật
/// (validator Media thật) nên cây trong DB là cây hợp lệ do đúng đường sản phẩm ghi ra.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PageDraftReferenceSourceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private string Cs => postgres.ConnectionString;

    public Task InitializeAsync() => MigrateAsync(Cs);

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Một ảnh Library và các clone của nó trong shop.</summary>
    private async Task<(Guid LibraryId, IReadOnlyList<Guid> CloneIds)> AddLibraryWithClonesAsync(Guid shopId, int clones)
    {
        await using var db = NewDb(Cs, shopId);
        var library = MediaAsset.NewLibrary(shopId, $"shops/{shopId}/library/{Guid.NewGuid():N}.webp", 100, 100, 10, 0.5f, 0.5f, null, null);
        db.MediaAssets.Add(library);
        await db.SaveChangesAsync();
        var ids = new List<Guid>();
        for (var i = 0; i < clones; i++)
        {
            var clone = MediaAsset.NewClone(library, $"shops/{shopId}/clone/{Guid.NewGuid():N}.webp", 100, 100, 10, "p,cover", 0.5f, 0.5f);
            db.MediaAssets.Add(clone);
            await db.SaveChangesAsync();
            ids.Add(clone.Id);
        }

        return (library.Id, ids);
    }

    private static async Task SaveTreeAsync(Request request, Guid pageId, string tree)
    {
        var draft = await request.GetDraft(pageId);
        await request.SaveDraft(pageId, tree, draft.Version);
    }

    private static Task<IReadOnlyList<MediaReferenceDto>> FindAsync(Request request, Guid assetId) =>
        new PageDraftReferenceSource(request.Db, request.Tenant, S.Validator).FindAsync(assetId, default);

    [Fact]
    public async Task Reports_each_page_once_that_uses_any_clone_of_the_library_image_case_insensitive_guid()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var (library, clones) = await AddLibraryWithClonesAsync(seeded.ShopId, 2);
        var (otherLibrary, otherClones) = await AddLibraryWithClonesAsync(seeded.ShopId, 1);
        var pageB = await request.CreatePage(new CreatePageCommand("trang-b", "Trang B"));
        var pageC = await request.CreatePage(new CreatePageCommand("trang-c", "Trang C"));

        // Home: HAI chỗ (hero dùng clone 1, gallery dùng clone 2) -> vẫn MỘT mục. Trang B: GUID viết HOA. Trang C: ảnh của library khác.
        await SaveTreeAsync(request, seeded.Home.Id, TreeWith(
            Hero("c_hero1", clones[0].ToString("D")), Gallery("c_gall1", clones[1].ToString("D"))));
        await SaveTreeAsync(request, pageB.Id, TreeWith(Hero("c_hero1", clones[0].ToString("D").ToUpperInvariant())));
        await SaveTreeAsync(request, pageC.Id, TreeWith(Hero("c_hero1", otherClones[0].ToString("D"))));

        var references = await FindAsync(request, library);

        Assert.All(references, r => Assert.Equal(MediaReferenceKind.PageDraft, r.Kind));
        Assert.Equal(2, references.Count);
        Assert.Contains(references, r => r.PageId == seeded.Home.Id && r.PageTitle == seeded.Home.Title);
        Assert.Contains(references, r => r.PageId == pageB.Id && r.PageTitle == "Trang B");
        var other = await FindAsync(request, otherLibrary);
        Assert.Equal(pageC.Id, Assert.Single(other).PageId);
    }

    [Fact]
    public async Task Soft_deleted_page_draft_is_not_reported()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var (library, clones) = await AddLibraryWithClonesAsync(seeded.ShopId, 1);
        var page = await request.CreatePage(new CreatePageCommand("se-xoa", "Sẽ xoá"));
        await SaveTreeAsync(request, page.Id, TreeWith(Hero("c_hero1", clones[0].ToString("D"))));
        Assert.Single(await FindAsync(request, library));

        await request.DeletePage(page.Id);

        Assert.Empty(await FindAsync(request, library));
    }

    [Fact]
    public async Task Image_used_in_another_shop_is_not_reported_and_unrelated_asset_is_empty()
    {
        var a = await AddWebsiteAsync(Cs);
        var b = await AddWebsiteAsync(Cs);
        await using var requestA = new Request(Cs, a.ShopId);
        await using var requestB = new Request(Cs, b.ShopId);
        var (libraryA, _) = await AddLibraryWithClonesAsync(a.ShopId, 1);
        var (libraryB, clonesB) = await AddLibraryWithClonesAsync(b.ShopId, 1);
        await SaveTreeAsync(requestB, b.Home.Id, TreeWith(Hero("c_hero1", clonesB[0].ToString("D"))));

        Assert.Empty(await FindAsync(requestA, libraryB)); // shop A hỏi id của shop B
        Assert.Empty(await FindAsync(requestA, libraryA)); // shop A có ảnh nhưng không trang nào dùng
        Assert.Empty(await FindAsync(requestA, Guid.NewGuid()));
        Assert.Single(await FindAsync(requestB, libraryB)); // đối chứng: shop B thấy chính tham chiếu của mình
    }

    [Fact]
    public async Task Unresolved_tenant_returns_empty()
    {
        var seeded = await AddWebsiteAsync(Cs);
        var (library, clones) = await AddLibraryWithClonesAsync(seeded.ShopId, 1);
        await using (var request = new Request(Cs, seeded.ShopId))
        {
            await SaveTreeAsync(request, seeded.Home.Id, TreeWith(Hero("c_hero1", clones[0].ToString("D"))));
        }

        await using var noTenant = new Request(Cs, shopId: null);

        Assert.Empty(await FindAsync(noTenant, library));
    }
}
