using Vsite.Domain.Exceptions;
using Vsite.Domain.Website.Entities;
using Vsite.Domain.Website.Enums;

namespace Vsite.IntegrationTests.Website;

/// <summary>T2, WEBSITE-001 (#33, #46, #98) — invariant của <see cref="Page"/>. Không cần Docker.</summary>
public sealed class PageEntityTests
{
    private static readonly Guid ShopId = Guid.NewGuid();
    private static readonly Guid WebsiteId = Guid.NewGuid();

    private static Page Composable(string slug = "gioi-thieu") => Page.NewComposable(ShopId, WebsiteId, slug, "Giới thiệu", 1);

    [Fact]
    public void NewComposable_has_no_system_type_and_is_enabled_and_indexable()
    {
        var page = Composable();

        Assert.Equal(PageKind.Composable, page.Kind);
        Assert.Null(page.SystemType);
        Assert.Equal("gioi-thieu", page.Slug);
        Assert.Equal(ShopId, page.ShopId);
        Assert.Equal(WebsiteId, page.WebsiteId);
        Assert.True(page.IsEnabled);
        Assert.False(page.NoIndex);
        Assert.Equal(1, page.SortOrder);
    }

    [Fact]
    public void Home_page_is_composable_with_empty_slug()
    {
        var home = Composable("");

        Assert.Equal("", home.Slug);
        Assert.True(home.IsHomePage);
        Assert.False(home.CanBeDeleted);
        Assert.False(Composable().IsHomePage);
        Assert.True(Composable().CanBeDeleted);
    }

    [Fact]
    public void NewSystem_requires_slug_except_NotFound_which_has_null_slug_and_noindex()
    {
        var contact = Page.NewSystem(ShopId, WebsiteId, SystemType.Contact, "lien-he", "Liên hệ", 2);
        Assert.Equal(PageKind.System, contact.Kind);
        Assert.Equal(SystemType.Contact, contact.SystemType);
        Assert.Equal("lien-he", contact.Slug);
        Assert.False(contact.CanBeDeleted);

        var notFound = Page.NewSystem(ShopId, WebsiteId, SystemType.NotFound, null, "404", 3);
        Assert.Null(notFound.Slug);
        Assert.True(notFound.NoIndex);
        Assert.False(notFound.CanBeDeleted);
    }

    [Theory]
    [InlineData(SystemType.Contact, null)]   // thiếu slug
    [InlineData(SystemType.Contact, "")]     // "" chỉ cho Composable
    [InlineData(SystemType.NotFound, "404")] // NotFound không có route công khai
    [InlineData(SystemType.NotFound, "")]
    public void NewSystem_rejects_slug_that_breaks_slug_null_iff_NotFound(SystemType type, string? slug)
    {
        var ex = Assert.Throws<DomainException>(() => Page.NewSystem(ShopId, WebsiteId, type, slug, "x", 0));

        Assert.Equal("PAGE_SLUG_INVALID", ex.ErrorCode);
    }

    [Fact]
    public void NewComposable_rejects_null_slug()
    {
        Assert.Equal("PAGE_SLUG_INVALID",
            Assert.Throws<DomainException>(() => Page.NewComposable(ShopId, WebsiteId, null!, "x", 0)).ErrorCode);
    }

    [Theory]
    [InlineData("Gioi-Thieu")]
    [InlineData("gioi_thieu")]
    [InlineData("-a")]
    [InlineData("a-")]
    [InlineData("a--b")]
    [InlineData("a/b")]
    [InlineData("../x")]
    [InlineData("giới-thiệu")]
    public void Slug_format_is_enforced(string slug)
    {
        Assert.Equal("PAGE_SLUG_INVALID", Assert.Throws<DomainException>(() => Composable(slug)).ErrorCode);
    }

    [Fact]
    public void Slug_longer_than_120_is_rejected_but_120_passes()
    {
        Assert.Equal(120, Composable(new string('a', 120)).Slug!.Length);
        Assert.Equal("PAGE_SLUG_INVALID", Assert.Throws<DomainException>(() => Composable(new string('a', 121))).ErrorCode);
    }

    [Fact]
    public void Title_is_required_and_limited()
    {
        Assert.Equal("PAGE_TITLE_INVALID",
            Assert.Throws<DomainException>(() => Page.NewComposable(ShopId, WebsiteId, "a", " ", 0)).ErrorCode);
        Assert.Equal("PAGE_TITLE_INVALID",
            Assert.Throws<DomainException>(() => Page.NewComposable(ShopId, WebsiteId, "a", new string('t', 161), 0)).ErrorCode);
    }

    [Fact]
    public void ChangeSlug_is_allowed_for_composable_but_never_for_system_pages()
    {
        var page = Composable();
        page.ChangeSlug("lien-he-nhanh");
        Assert.Equal("lien-he-nhanh", page.Slug);

        var system = Page.NewSystem(ShopId, WebsiteId, SystemType.Contact, "lien-he", "Liên hệ", 2);
        Assert.Equal("PAGE_SYSTEM_SLUG_FIXED", Assert.Throws<DomainException>(() => system.ChangeSlug("khac")).ErrorCode);
        Assert.Equal("PAGE_SLUG_INVALID", Assert.Throws<DomainException>(() => page.ChangeSlug(null!)).ErrorCode);
    }

    [Fact]
    public void NotFound_cannot_clear_noindex()
    {
        var notFound = Page.NewSystem(ShopId, WebsiteId, SystemType.NotFound, null, "404", 3);

        Assert.Equal("PAGE_NOINDEX_REQUIRED", Assert.Throws<DomainException>(() => notFound.SetNoIndex(false)).ErrorCode);
        Composable().SetNoIndex(true);
    }

    [Fact]
    public void Seo_limits_and_og_image_and_enabled_and_sort_order()
    {
        var page = Composable();
        var og = Guid.NewGuid();

        page.SetSeo(new string('a', 160), new string('b', 320));
        page.SetOgImage(og);
        page.SetEnabled(false);
        page.SetSortOrder(7);

        Assert.Equal(og, page.OgImageId);
        Assert.False(page.IsEnabled);
        Assert.Equal(7, page.SortOrder);
        Assert.Equal("PAGE_SEO_TOO_LONG", Assert.Throws<DomainException>(() => page.SetSeo(new string('a', 161), null)).ErrorCode);
        Assert.Equal("PAGE_SEO_TOO_LONG", Assert.Throws<DomainException>(() => page.SetSeo(null, new string('b', 321))).ErrorCode);
    }

    [Fact]
    public void SoftDelete_marks_deleted_for_composable_and_refuses_home_and_system()
    {
        var page = Composable();
        page.SoftDelete();
        Assert.True(page.IsDeleted);

        Assert.Equal("PAGE_NOT_DELETABLE", Assert.Throws<DomainException>(() => Composable("").SoftDelete()).ErrorCode);
        Assert.Equal("PAGE_NOT_DELETABLE", Assert.Throws<DomainException>(
            () => Page.NewSystem(ShopId, WebsiteId, SystemType.Contact, "lien-he", "Liên hệ", 2).SoftDelete()).ErrorCode);
    }
}
