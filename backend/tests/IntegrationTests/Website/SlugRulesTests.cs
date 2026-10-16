using Vsite.Application.Website.Validation;
using Vsite.Domain.Exceptions;
using Vsite.Domain.ReservedRoutes;
using Vsite.Infrastructure.Configuration;
using static Vsite.IntegrationTests.Website.WebsiteHandlerKit;

namespace Vsite.IntegrationTests.Website;

/// <summary>T8 (WEBSITE-001, R9, #98) — luật Slug trang. Không cần DB. Dùng cấu hình tiêm có key reservedShopSitePaths.</summary>
public sealed class SlugRulesTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("EN")]
    [InlineData("vi")]
    [InlineData("Vi")]
    [InlineData("lien-he")]
    [InlineData("san-pham")]
    [InlineData("danh-muc")]
    [InlineData("dat-lich")]
    [InlineData("tim-kiem")]
    [InlineData("media")]
    [InlineData("api")]
    [InlineData("assets")]
    [InlineData("static")]
    [InlineData("MEDIA")]
    public void Reserved_slug_is_422_PAGE_SLUG_RESERVED(string slug)
    {
        var ex = Assert.Throws<UnprocessableException>(() => S.Slugs.EnsureAllowed(slug));

        Assert.Equal("PAGE_SLUG_RESERVED", ex.ErrorCode);
    }

    [Theory]
    [InlineData("blog")]
    [InlineData("about")]
    [InlineData("help")]
    [InlineData("gioi-thieu")]
    [InlineData("a")]
    [InlineData("tin-tuc-2026")]
    public void Ordinary_slug_passes_including_blog_and_about(string slug) => S.Slugs.EnsureAllowed(slug);

    [Theory]
    [InlineData("")]
    [InlineData("../etc")]
    [InlineData("%2f")]
    [InlineData("a/b")]
    [InlineData("-a")]
    [InlineData("a-")]
    [InlineData("a--b")]
    [InlineData("a b")]
    [InlineData("gioi-thiệu")]
    [InlineData("日本")]
    [InlineData("a\n")]
    [InlineData("Blog")]
    public void Malformed_slug_is_422_PAGE_SLUG_INVALID(string slug)
    {
        var ex = Assert.Throws<UnprocessableException>(() => S.Slugs.EnsureAllowed(slug));

        Assert.Equal("PAGE_SLUG_INVALID", ex.ErrorCode);
    }

    [Fact]
    public void Slug_longer_than_120_is_invalid_and_exactly_120_passes()
    {
        S.Slugs.EnsureAllowed(new string('a', 120));

        var ex = Assert.Throws<UnprocessableException>(() => S.Slugs.EnsureAllowed(new string('a', 121)));
        Assert.Equal("PAGE_SLUG_INVALID", ex.ErrorCode);
    }

    [Fact]
    public void Null_slug_is_invalid()
    {
        var ex = Assert.Throws<UnprocessableException>(() => S.Slugs.EnsureAllowed(null));
        Assert.Equal("PAGE_SLUG_INVALID", ex.ErrorCode);
    }

    [Fact]
    public void Missing_reservedShopSitePaths_key_means_only_locales_and_system_routes_are_reserved()
    {
        var rules = new SlugRules(S.Locales, S.SystemPages, new FixedReservedRoutes(new ReservedRoutes([], [])));

        rules.EnsureAllowed("media"); // chưa có key → không chặn (provider trả rỗng)
        Assert.Throws<UnprocessableException>(() => rules.EnsureAllowed("en"));
        Assert.Throws<UnprocessableException>(() => rules.EnsureAllowed("lien-he"));
    }

    [Fact(Skip = "chờ người thêm reservedShopSitePaths vào config/reserved-routes.json — plan §4")]
    public void Real_config_reserves_media_and_api()
    {
        var provider = new ReservedRoutesProvider(TreeFixtures.Config("reserved-routes.json"));
        var rules = new SlugRules(S.Locales, S.SystemPages, provider);

        Assert.Throws<UnprocessableException>(() => rules.EnsureAllowed("media"));
        Assert.Throws<UnprocessableException>(() => rules.EnsureAllowed("api"));
        rules.EnsureAllowed("blog");
        rules.EnsureAllowed("about");
    }
}
