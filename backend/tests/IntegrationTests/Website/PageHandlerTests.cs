using Microsoft.EntityFrameworkCore;
using Vsite.Application.Website.Commands.CreatePage;
using Vsite.Application.Website.Commands.UpdatePage;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Website.Enums;
using static Vsite.IntegrationTests.Website.WebsiteHandlerKit;

namespace Vsite.IntegrationTests.Website;

/// <summary>T8 (WEBSITE-001, R9, R10, R13, #98) — GetWebsite/ListPages/CreatePage/UpdatePage/DeletePage. Postgres thật.</summary>
[Collection(PostgresCollection.Name)]
public sealed class PageHandlerTests(PostgresFixture postgres) : IAsyncLifetime
{
    private string Cs => postgres.ConnectionString;

    public Task InitializeAsync() => MigrateAsync(Cs);

    public Task DisposeAsync() => Task.CompletedTask;

    private static CreatePageCommand NewPage(string slug, string title = "Giới thiệu") => new(slug, title);

    // ---- GetWebsite ----

    [Fact]
    public async Task GetWebsite_returns_dto_with_home_page_id()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);

        var dto = await request.GetWebsite();

        Assert.Equal(seeded.Website.Id, dto.Id);
        Assert.Equal(seeded.Home.Id, dto.HomePageId);
        Assert.Equal("vi", dto.DefaultLocale);
    }

    [Fact]
    public async Task GetWebsite_before_creation_is_404_WEBSITE_NOT_FOUND()
    {
        var shopId = await AddShopAsync(Cs);
        await using var request = new Request(Cs, shopId);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => request.GetWebsite());

        Assert.Equal("WEBSITE_NOT_FOUND", ex.ErrorCode);
    }

    [Fact]
    public async Task Handlers_fail_closed_when_tenant_is_unresolved()
    {
        await using var request = new Request(Cs, shopId: null);

        Assert.Equal("TENANT_REQUIRED", (await Assert.ThrowsAsync<ForbiddenAccessException>(() => request.GetWebsite())).ErrorCode);
        Assert.Equal("TENANT_REQUIRED", (await Assert.ThrowsAsync<ForbiddenAccessException>(() => request.ListPages())).ErrorCode);
        Assert.Equal("TENANT_REQUIRED", (await Assert.ThrowsAsync<ForbiddenAccessException>(() => request.CreatePage(NewPage("a")))).ErrorCode);
        Assert.Equal("TENANT_REQUIRED", (await Assert.ThrowsAsync<ForbiddenAccessException>(() => request.DeletePage(Guid.NewGuid()))).ErrorCode);
    }

    // ---- ListPages ----

    [Fact]
    public async Task ListPages_reports_deletable_home_dirty_and_is_ordered_by_sort_order()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var created = await request.CreatePage(NewPage("blog", "Blog"));

        var pages = await request.ListPages();

        Assert.Equal(4, pages.Count);
        Assert.Equal(pages.Select(p => p.SortOrder).OrderBy(x => x), pages.Select(p => p.SortOrder));
        var home = pages.Single(p => p.IsHomePage);
        Assert.Equal("", home.Slug);
        Assert.False(home.Deletable);
        Assert.All(pages.Where(p => p.Kind == PageKind.System), p => Assert.False(p.Deletable));
        Assert.All(pages.Where(p => p.Kind == PageKind.System), p => Assert.False(p.IsHomePage));
        var blog = pages.Single(p => p.Id == created.Id);
        Assert.True(blog.Deletable);
        Assert.True(blog.IsDirty);
        Assert.Null(pages.Single(p => p.SystemType == SystemType.NotFound).Slug);
    }

    [Fact]
    public async Task ListPages_does_not_include_deleted_pages_nor_other_shops_pages()
    {
        var a = await AddWebsiteAsync(Cs);
        var b = await AddWebsiteAsync(Cs);
        await using var requestA = new Request(Cs, a.ShopId);
        var extra = await requestA.CreatePage(NewPage("tam-xoa"));
        await requestA.DeletePage(extra.Id);

        var pages = await requestA.ListPages();

        Assert.Equal(3, pages.Count);
        Assert.DoesNotContain(pages, p => p.Id == extra.Id);
        Assert.DoesNotContain(pages, p => b.Pages.Any(x => x.Id == p.Id));
    }

    // ---- CreatePage (R10: chỉ Composable) ----

    [Fact]
    public async Task CreatePage_makes_a_composable_page_with_server_sort_order_and_an_empty_valid_draft()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);

        var first = await request.CreatePage(NewPage("gioi-thieu", "Giới thiệu") with { SeoTitle = "SEO", NoIndex = true });
        var second = await request.CreatePage(NewPage("blog", "Blog"));

        Assert.Equal(PageKind.Composable, first.Kind);
        Assert.Null(first.SystemType);
        Assert.Equal("gioi-thieu", first.Slug);
        Assert.Equal("SEO", first.SeoTitle);
        Assert.True(first.NoIndex);
        Assert.True(first.IsEnabled);
        Assert.False(first.IsHomePage);
        Assert.True(first.Deletable);
        Assert.True(second.SortOrder > first.SortOrder);
        Assert.True(first.SortOrder > seeded.Pages.Max(p => p.SortOrder));

        var draft = await request.GetDraft(first.Id);
        var result = S.Validator.Validate(draft.Tree.GetRawText(), PageKind.Composable);
        Assert.True(result.IsValid);
        Assert.True(draft.IsDirty);
    }

    [Fact]
    public async Task CreatePage_command_cannot_carry_kind_or_system_type()
    {
        var properties = typeof(CreatePageCommand).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain("Kind", properties);
        Assert.DoesNotContain("SystemType", properties);
        Assert.DoesNotContain("ShopId", properties);
        Assert.DoesNotContain("WebsiteId", properties);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task CreatePage_duplicate_slug_is_409_PAGE_SLUG_TAKEN()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        await request.CreatePage(NewPage("blog"));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => request.CreatePage(NewPage("blog")));

        Assert.Equal("PAGE_SLUG_TAKEN", ex.ErrorCode);
    }

    [Fact]
    public async Task CreatePage_slug_can_be_reused_after_soft_delete()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var first = await request.CreatePage(NewPage("blog"));
        await request.DeletePage(first.Id);

        var again = await request.CreatePage(NewPage("blog"));

        Assert.NotEqual(first.Id, again.Id);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("lien-he")]
    [InlineData("san-pham")]
    [InlineData("media")]
    [InlineData("api")]
    public async Task CreatePage_reserved_slug_is_422(string slug)
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);

        var ex = await Assert.ThrowsAsync<UnprocessableException>(() => request.CreatePage(NewPage(slug)));

        Assert.Equal("PAGE_SLUG_RESERVED", ex.ErrorCode);
    }

    [Fact]
    public async Task CreatePage_empty_slug_is_not_allowed_even_though_home_uses_it()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);

        var ex = await Assert.ThrowsAsync<UnprocessableException>(() => request.CreatePage(NewPage("")));

        Assert.Equal("PAGE_SLUG_INVALID", ex.ErrorCode);
    }

    [Fact]
    public async Task CreatePage_blog_and_about_are_allowed()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);

        await request.CreatePage(NewPage("blog"));
        await request.CreatePage(NewPage("about"));
    }

    [Fact]
    public async Task CreatePage_without_a_website_is_404_WEBSITE_NOT_FOUND()
    {
        var shopId = await AddShopAsync(Cs);
        await using var request = new Request(Cs, shopId);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => request.CreatePage(NewPage("blog")));

        Assert.Equal("WEBSITE_NOT_FOUND", ex.ErrorCode);
    }

    [Fact]
    public async Task CreatePage_validates_title_and_seo_lengths_through_the_validator()
    {
        var validator = new CreatePageValidator();

        Assert.False(validator.Validate(new CreatePageCommand("blog", "")).IsValid);
        Assert.False(validator.Validate(new CreatePageCommand("blog", new string('a', 161))).IsValid);
        Assert.False(validator.Validate(new CreatePageCommand("blog", "ok", SeoTitle: new string('a', 161))).IsValid);
        Assert.False(validator.Validate(new CreatePageCommand("blog", "ok", SeoDescription: new string('a', 321))).IsValid);
        Assert.True(validator.Validate(new CreatePageCommand("blog", "ok")).IsValid);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Two_parallel_creates_of_the_same_slug_yield_one_success_and_one_409()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var a = new Request(Cs, seeded.ShopId);
        await using var b = new Request(Cs, seeded.ShopId);

        var results = await Task.WhenAll(Try(a.CreatePage(NewPage("dua"))), Try(b.CreatePage(NewPage("dua"))));

        Assert.Equal(1, results.Count(r => r is null));
        Assert.Equal("PAGE_SLUG_TAKEN", Assert.Single(results.OfType<AppException>()).ErrorCode);
    }

    // ---- OG image qua IMediaReferenceValidator (bản Library) ----

    [Fact]
    public async Task CreatePage_og_image_must_be_a_library_asset_of_this_shop()
    {
        var seeded = await AddWebsiteAsync(Cs);
        var other = await AddShopAsync(Cs);
        var library = await AddAssetAsync(Cs, seeded.ShopId, "Library");
        var clone = await AddAssetAsync(Cs, seeded.ShopId, "Clone");
        var foreign = await AddAssetAsync(Cs, other, "Library");
        await using var request = new Request(Cs, seeded.ShopId);

        var ok = await request.CreatePage(NewPage("co-anh") with { OgImageId = library });
        Assert.Equal(library, ok.OgImageId);

        foreach (var bad in new[] { clone, foreign, Guid.NewGuid() })
        {
            var ex = await Assert.ThrowsAsync<UnprocessableException>(
                () => request.CreatePage(NewPage($"x-{bad:N}"[..10]) with { OgImageId = bad }));
            Assert.Equal("MEDIA_INVALID_IMAGE_REFERENCE", ex.ErrorCode);
        }
    }

    // ---- UpdatePage ----

    [Fact]
    public async Task UpdatePage_composable_can_change_everything_including_slug()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var page = await request.CreatePage(NewPage("cu"));

        var updated = await request.UpdatePage(new UpdatePageCommand(
            page.Id, "moi", "Tiêu đề mới", "SEO", "Mô tả", null, NoIndex: true, IsEnabled: false));

        Assert.Equal("moi", updated.Slug);
        Assert.Equal("Tiêu đề mới", updated.Title);
        Assert.Equal("SEO", updated.SeoTitle);
        Assert.Equal("Mô tả", updated.SeoDescription);
        Assert.True(updated.NoIndex);
        Assert.False(updated.IsEnabled);
    }

    [Fact]
    public async Task UpdatePage_null_slug_means_unchanged_and_same_slug_is_not_a_conflict_with_itself()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var page = await request.CreatePage(NewPage("giu-nguyen"));

        var unchanged = await request.UpdatePage(new UpdatePageCommand(page.Id, null, "A", null, null, null, false, true));
        var same = await request.UpdatePage(new UpdatePageCommand(page.Id, "giu-nguyen", "B", null, null, null, false, true));

        Assert.Equal("giu-nguyen", unchanged.Slug);
        Assert.Equal("giu-nguyen", same.Slug);
        Assert.Equal("B", same.Title);
    }

    [Fact]
    public async Task UpdatePage_slug_taken_by_another_page_is_409_and_reserved_is_422()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        await request.CreatePage(NewPage("da-co"));
        var page = await request.CreatePage(NewPage("dang-sua"));

        var taken = await Assert.ThrowsAsync<ConflictException>(
            () => request.UpdatePage(new UpdatePageCommand(page.Id, "da-co", "T", null, null, null, false, true)));
        var reserved = await Assert.ThrowsAsync<UnprocessableException>(
            () => request.UpdatePage(new UpdatePageCommand(page.Id, "media", "T", null, null, null, false, true)));

        Assert.Equal("PAGE_SLUG_TAKEN", taken.ErrorCode);
        Assert.Equal("PAGE_SLUG_RESERVED", reserved.ErrorCode);
    }

    [Fact]
    public async Task UpdatePage_system_page_allows_title_seo_enabled_noindex_but_not_slug()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);

        var updated = await request.UpdatePage(new UpdatePageCommand(
            seeded.Contact.Id, null, "Liên hệ mới", "SEO", "Mô tả", null, NoIndex: true, IsEnabled: false));
        Assert.Equal("lien-he", updated.Slug);
        Assert.Equal("Liên hệ mới", updated.Title);
        Assert.False(updated.IsEnabled);
        Assert.True(updated.NoIndex);
        Assert.Equal(PageKind.System, updated.Kind);
        Assert.Equal(SystemType.Contact, updated.SystemType);

        // gửi lại đúng slug hiện tại = không đổi → qua
        await request.UpdatePage(new UpdatePageCommand(seeded.Contact.Id, "lien-he", "Liên hệ", null, null, null, false, true));

        var ex = await Assert.ThrowsAsync<UnprocessableException>(() => request.UpdatePage(
            new UpdatePageCommand(seeded.Contact.Id, "doi-slug", "x", null, null, null, false, true)));
        Assert.Equal("PAGE_SYSTEM_SLUG_FIXED", ex.ErrorCode);
    }

    [Fact]
    public async Task UpdatePage_home_slug_is_fixed_and_notfound_must_stay_noindex()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);

        var home = await Assert.ThrowsAsync<UnprocessableException>(() => request.UpdatePage(
            new UpdatePageCommand(seeded.Home.Id, "trang-chu", "x", null, null, null, false, true)));
        Assert.Equal("PAGE_HOME_SLUG_FIXED", home.ErrorCode);

        var notFound = await Assert.ThrowsAsync<UnprocessableException>(() => request.UpdatePage(
            new UpdatePageCommand(seeded.NotFound.Id, null, "x", null, null, null, NoIndex: false, IsEnabled: true)));
        Assert.Equal("PAGE_NOINDEX_REQUIRED", notFound.ErrorCode);
    }

    [Fact]
    public async Task UpdatePage_of_another_shops_page_is_NotFound()
    {
        var a = await AddWebsiteAsync(Cs);
        var b = await AddWebsiteAsync(Cs);
        await using var requestA = new Request(Cs, a.ShopId);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => requestA.UpdatePage(
            new UpdatePageCommand(b.Home.Id, null, "hack", null, null, null, false, true)));

        Assert.Equal("PAGE_NOT_FOUND", ex.ErrorCode);
        await using var verify = NewDb(Cs, b.ShopId);
        Assert.NotEqual("hack", (await verify.Pages.SingleAsync(p => p.Id == b.Home.Id)).Title);
    }

    // ---- DeletePage (R10) ----

    [Fact]
    public async Task DeletePage_soft_deletes_a_composable_page()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var page = await request.CreatePage(NewPage("xoa-di"));

        await request.DeletePage(page.Id);

        await using var verify = NewDb(Cs, seeded.ShopId);
        Assert.Null(await verify.Pages.SingleOrDefaultAsync(p => p.Id == page.Id));
        Assert.True((await verify.Pages.IgnoreQueryFilters().SingleAsync(p => p.Id == page.Id)).IsDeleted);
    }

    [Fact]
    public async Task DeletePage_home_and_system_pages_are_409_PAGE_NOT_DELETABLE_and_remain()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);

        foreach (var page in new[] { seeded.Home, seeded.Contact, seeded.NotFound })
        {
            var ex = await Assert.ThrowsAsync<ConflictException>(() => request.DeletePage(page.Id));
            Assert.Equal("PAGE_NOT_DELETABLE", ex.ErrorCode);
        }

        Assert.Equal(3, (await request.ListPages()).Count);
    }

    [Fact]
    public async Task DeletePage_of_another_shops_page_is_NotFound_and_unknown_id_too()
    {
        var a = await AddWebsiteAsync(Cs);
        var b = await AddWebsiteAsync(Cs);
        await using var requestA = new Request(Cs, a.ShopId);
        await using var requestB = new Request(Cs, b.ShopId);
        var pageB = await requestB.CreatePage(NewPage("cua-b"));

        var foreign = await Assert.ThrowsAsync<NotFoundException>(() => requestA.DeletePage(pageB.Id));
        var unknown = await Assert.ThrowsAsync<NotFoundException>(() => requestA.DeletePage(Guid.NewGuid()));

        Assert.Equal("PAGE_NOT_FOUND", foreign.ErrorCode);
        Assert.Equal("PAGE_NOT_FOUND", unknown.ErrorCode);
        Assert.Contains(await requestB.ListPages(), p => p.Id == pageB.Id);
    }

    private static async Task<AppException?> Try<T>(Task<T> task)
    {
        try
        {
            await task;
            return null;
        }
        catch (AppException ex)
        {
            return ex;
        }
    }
}
