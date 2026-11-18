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

    [Fact]
    public void PageDraft_mark_published_clears_dirty_and_mark_dirty_sets_it_without_touching_tree()
    {
        var draft = PageDraft.Create(ShopId, Guid.NewGuid(), """{"schemaVersion":1,"id":"root","type":"Page","children":[]}""");
        var tree = draft.Tree;

        draft.MarkPublished();
        Assert.False(draft.IsDirty);
        Assert.Equal(tree, draft.Tree);

        draft.MarkDirty();
        Assert.True(draft.IsDirty);
        Assert.Equal(tree, draft.Tree);
    }

    // ---------------------------------------------------------------- SitePublication / publish

    private const string Snapshot = """{"schemaVersion":1,"version":1,"pages":[]}""";

    [Fact]
    public void SitePublication_create_sets_fields_and_rejects_bad_input()
    {
        var websiteId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var at = DateTimeOffset.UtcNow;

        var pub = SitePublication.Create(ShopId, websiteId, 3, Snapshot, "ghi chú", userId, at);

        Assert.Equal(ShopId, pub.ShopId);
        Assert.Equal(websiteId, pub.WebsiteId);
        Assert.Equal(3, pub.Version);
        Assert.Equal(Snapshot, pub.Snapshot);
        Assert.Equal("ghi chú", pub.Note);
        Assert.Equal(userId, pub.PublishedByUserId);
        Assert.Equal(at, pub.PublishedAt);

        Assert.Equal("SITE_PUBLICATION_VERSION_INVALID",
            Assert.Throws<DomainException>(() => SitePublication.Create(ShopId, websiteId, 0, Snapshot, null, null, at)).ErrorCode);
        Assert.Equal("SITE_PUBLICATION_SNAPSHOT_REQUIRED",
            Assert.Throws<DomainException>(() => SitePublication.Create(ShopId, websiteId, 1, " ", null, null, at)).ErrorCode);
        Assert.Equal("SITE_PUBLICATION_NOTE_TOO_LONG",
            Assert.Throws<DomainException>(() => SitePublication.Create(ShopId, websiteId, 1, Snapshot, new string('n', 201), null, at)).ErrorCode);
    }

    [Fact]
    public void Publish_points_current_and_moves_draft_to_published_but_keeps_suspended()
    {
        var website = WebsiteEntity.Create(ShopId);
        var pub = SitePublication.Create(ShopId, website.Id, 1, Snapshot, null, null, DateTimeOffset.UtcNow);

        website.Publish(pub);
        Assert.Equal(pub.Id, website.CurrentPublicationId);
        Assert.Equal(WebsiteStatus.Published, website.Status);

        // Suspended giữ nguyên khi publish (G6).
        website.Suspend();
        var pub2 = SitePublication.Create(ShopId, website.Id, 2, Snapshot, null, null, DateTimeOffset.UtcNow);
        website.Publish(pub2);
        Assert.Equal(pub2.Id, website.CurrentPublicationId);
        Assert.Equal(WebsiteStatus.Suspended, website.Status);
    }

    [Fact]
    public void PointTo_changes_only_the_pointer_not_the_status()
    {
        var website = WebsiteEntity.Create(ShopId);
        var pub1 = SitePublication.Create(ShopId, website.Id, 1, Snapshot, null, null, DateTimeOffset.UtcNow);
        var pub2 = SitePublication.Create(ShopId, website.Id, 2, Snapshot, null, null, DateTimeOffset.UtcNow);
        website.Publish(pub2);

        website.PointTo(pub1);
        Assert.Equal(pub1.Id, website.CurrentPublicationId);
        Assert.Equal(WebsiteStatus.Published, website.Status);
    }

    [Fact]
    public void Publish_and_point_to_reject_publication_of_another_website()
    {
        var website = WebsiteEntity.Create(ShopId);
        var foreign = SitePublication.Create(ShopId, Guid.NewGuid(), 1, Snapshot, null, null, DateTimeOffset.UtcNow);

        Assert.Equal("SITE_PUBLICATION_WEBSITE_MISMATCH",
            Assert.Throws<DomainException>(() => website.Publish(foreign)).ErrorCode);
        Assert.Equal("SITE_PUBLICATION_WEBSITE_MISMATCH",
            Assert.Throws<DomainException>(() => website.PointTo(foreign)).ErrorCode);
        Assert.Null(website.CurrentPublicationId);
    }

    [Fact]
    public void Reinstate_returns_to_published_when_a_publication_exists_otherwise_draft()
    {
        var withoutPub = WebsiteEntity.Create(ShopId);
        withoutPub.Suspend();
        withoutPub.Reinstate();
        Assert.Equal(WebsiteStatus.Draft, withoutPub.Status);

        var withPub = WebsiteEntity.Create(ShopId);
        withPub.Publish(SitePublication.Create(ShopId, withPub.Id, 1, Snapshot, null, null, DateTimeOffset.UtcNow));
        withPub.Suspend();
        withPub.Reinstate();
        Assert.Equal(WebsiteStatus.Published, withPub.Status);
    }
}
