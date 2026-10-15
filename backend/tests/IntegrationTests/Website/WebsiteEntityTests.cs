using Vsite.Domain.Exceptions;
using Vsite.Domain.Website.Entities;
using Vsite.Domain.Website.Enums;
using WebsiteEntity = Vsite.Domain.Website.Entities.Website;

namespace Vsite.IntegrationTests.Website;

/// <summary>T2, WEBSITE-001 (#92, #98) — unit test thuần cho Website/Theme/PageDraft, không cần Docker.</summary>
public sealed class WebsiteEntityTests
{
    private static readonly Guid ShopId = Guid.NewGuid();

    [Fact]
    public void Create_defaults_to_vi_draft_with_empty_tracking()
    {
        var website = WebsiteEntity.Create(ShopId);

        Assert.Equal(ShopId, website.ShopId);
        Assert.Equal("vi", website.DefaultLocale);
        Assert.Equal(["vi"], website.Locales);
        Assert.Equal(WebsiteStatus.Draft, website.Status);
        Assert.Equal("{}", website.TrackingSnippets);
        Assert.Null(website.DefaultOgImageId);
        Assert.Null(website.FaviconAssetId);
    }

    [Fact]
    public void Create_rejects_default_locale_not_in_locales()
    {
        var ex = Assert.Throws<DomainException>(() => WebsiteEntity.Create(ShopId, "vi", ["en"]));

        Assert.Equal("WEBSITE_DEFAULT_LOCALE_NOT_ENABLED", ex.ErrorCode);
    }

    [Fact]
    public void SetLocales_requires_default_locale_inside_and_no_duplicates_or_blanks()
    {
        var website = WebsiteEntity.Create(ShopId);

        website.SetLocales(["vi", "en"], "en");
        Assert.Equal("en", website.DefaultLocale);
        Assert.Equal(["vi", "en"], website.Locales);

        Assert.Equal("WEBSITE_DEFAULT_LOCALE_NOT_ENABLED",
            Assert.Throws<DomainException>(() => website.SetLocales(["vi"], "en")).ErrorCode);
        Assert.Equal("WEBSITE_LOCALES_INVALID",
            Assert.Throws<DomainException>(() => website.SetLocales(["vi", "VI"], "vi")).ErrorCode);
        Assert.Equal("WEBSITE_LOCALES_INVALID",
            Assert.Throws<DomainException>(() => website.SetLocales([], "vi")).ErrorCode);
        Assert.Equal("WEBSITE_LOCALES_INVALID",
            Assert.Throws<DomainException>(() => website.SetLocales(["vi", " "], "vi")).ErrorCode);

        // lỗi không làm hỏng trạng thái đang có
        Assert.Equal("en", website.DefaultLocale);
    }

    [Fact]
    public void Seo_defaults_enforce_length_limits()
    {
        var website = WebsiteEntity.Create(ShopId);

        website.SetSeoDefaults(new string('a', 160), new string('b', 320));
        Assert.Equal(160, website.DefaultSeoTitle!.Length);

        Assert.Equal("WEBSITE_SEO_TOO_LONG",
            Assert.Throws<DomainException>(() => website.SetSeoDefaults(new string('a', 161), null)).ErrorCode);
        Assert.Equal("WEBSITE_SEO_TOO_LONG",
            Assert.Throws<DomainException>(() => website.SetSeoDefaults(null, new string('b', 321))).ErrorCode);
    }

    [Fact]
    public void Images_and_tracking_are_set_through_methods()
    {
        var website = WebsiteEntity.Create(ShopId);
        var og = Guid.NewGuid();
        var fav = Guid.NewGuid();

        website.SetDefaultOgImage(og);
        website.SetFavicon(fav);
        website.SetTrackingSnippets("""{"ga4":"G-ABCDEF1"}""");

        Assert.Equal(og, website.DefaultOgImageId);
        Assert.Equal(fav, website.FaviconAssetId);
        Assert.Contains("G-ABCDEF1", website.TrackingSnippets);

        website.SetDefaultOgImage(null);
        Assert.Null(website.DefaultOgImageId);
        Assert.Equal("WEBSITE_TRACKING_INVALID",
            Assert.Throws<DomainException>(() => website.SetTrackingSnippets(" ")).ErrorCode);
    }

    [Fact]
    public void Theme_is_keyed_by_website_and_replace_requires_tokens()
    {
        var websiteId = Guid.NewGuid();
        var theme = Theme.Create(websiteId, ShopId, """{"schemaVersion":1}""", "spa-lavender");

        Assert.Equal(websiteId, theme.WebsiteId);
        Assert.Equal(websiteId, theme.Id);
        Assert.Equal(ShopId, theme.ShopId);
        Assert.Equal("spa-lavender", theme.PresetKey);

        theme.Replace("""{"schemaVersion":1,"colors":{}}""", null);
        Assert.Null(theme.PresetKey);
        Assert.Contains("colors", theme.Tokens);

        Assert.Equal("THEME_TOKENS_REQUIRED", Assert.Throws<DomainException>(() => theme.Replace("", null)).ErrorCode);
        Assert.Equal("THEME_PRESET_TOO_LONG", Assert.Throws<DomainException>(() => theme.Replace("{}", new string('p', 51))).ErrorCode);
    }

    [Fact]
    public void PageDraft_is_keyed_by_page_starts_dirty_and_replace_keeps_dirty()
    {
        var pageId = Guid.NewGuid();
        var draft = PageDraft.Create(ShopId, pageId, """{"schemaVersion":1,"id":"root","type":"Page","children":[]}""");

        Assert.Equal(pageId, draft.PageId);
        Assert.Equal(pageId, draft.Id);
        Assert.True(draft.IsDirty); // G11

        draft.Replace("""{"schemaVersion":1,"id":"root","type":"Page","children":[{"id":"c_abcde","type":"Hero"}]}""");
        Assert.Contains("c_abcde", draft.Tree);
        Assert.True(draft.IsDirty);

        Assert.Equal("PAGE_DRAFT_TREE_REQUIRED", Assert.Throws<DomainException>(() => draft.Replace(" ")).ErrorCode);
    }
}
