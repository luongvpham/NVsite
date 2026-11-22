using System.Text.Json.Nodes;
using Vsite.Application.Website.Navigation;
using Vsite.Application.Website.Publishing;
using Vsite.Domain.Website.Entities;
using Vsite.Domain.Website.Enums;
using WebsiteEntity = Vsite.Domain.Website.Entities.Website;
using ThemeEntity = Vsite.Domain.Website.Entities.Theme;

namespace Vsite.IntegrationTests.Website;

/// <summary>
/// T2, WEBSITE-004 (#94, #97) — <see cref="SnapshotBuilder"/> thuần, KHÔNG cần Docker. Dùng catalog/artifact thật ở
/// repo root qua <see cref="WebsiteHandlerKit"/> và <see cref="TreeFixtures"/>; dựng entity trong bộ nhớ.
/// </summary>
public sealed class SnapshotBuilderTests
{
    private static ThemeEntity SeedTheme(Guid websiteId, Guid shopId, string? presetKey = "spa-lavender") =>
        ThemeEntity.Create(websiteId, shopId, WebsiteHandlerKit.S.ThemeTokens.DefaultsJson, presetKey);

    private static string EmptyComposableTree() =>
        WebsiteHandlerKit.S.Validator.Validate(
            """{"schemaVersion":1,"id":"root","type":"Page","children":[]}""", PageKind.Composable).CleanTree!;

    /// <summary>Website trắng (home + Contact + NotFound) dựng qua seeder thật, trong bộ nhớ.</summary>
    private static (WebsiteEntity Website, ThemeEntity Theme, List<SnapshotPage> Pages) SeedWebsite()
    {
        var shopId = Guid.NewGuid();
        var websiteId = Guid.NewGuid();
        var website = WebsiteEntity.Create(shopId, id: websiteId);
        var theme = SeedTheme(websiteId, shopId);

        var seeder = WebsiteHandlerKit.S.Seeder;
        var (home, homeDraft) = seeder.CreateHome(shopId, websiteId, 0);
        var system = seeder.EnsureSystemPages(shopId, websiteId, [], 1);

        var pages = new List<SnapshotPage> { new(home, homeDraft.Tree) };
        for (var i = 0; i < system.Pages.Count; i++)
        {
            pages.Add(new SnapshotPage(system.Pages[i], system.Drafts[i].Tree));
        }

        return (website, theme, pages);
    }

    [Fact]
    public void Snapshot_from_seeded_website_is_valid_against_schema()
    {
        var (website, theme, pages) = SeedWebsite();

        var json = SnapshotBuilder.Build(website, theme, version: 1, pages, media: [], navigation: []);

        var violations = WebsiteHandlerKit.S.Schemas.ValidateSiteSnapshot(json);
        Assert.Empty(violations);
    }

    [Fact]
    public void Top_level_shape_matches_spec()
    {
        var (website, theme, pages) = SeedWebsite();

        var root = JsonNode.Parse(SnapshotBuilder.Build(website, theme, version: 7, pages, media: [], navigation: []))!.AsObject();

        Assert.Equal(1, root["schemaVersion"]!.GetValue<int>());
        Assert.Equal(7, root["version"]!.GetValue<int>());
        Assert.Equal("vi", root["defaultLocale"]!.GetValue<string>());
        Assert.Equal(new[] { "vi" }, root["locales"]!.AsArray().Select(n => n!.GetValue<string>()));
        Assert.Empty(root["navigation"]!["items"]!.AsArray());
        Assert.Empty(root["tracking"]!.AsObject()); // TrackingSnippets mặc định "{}"
        Assert.Empty(root["media"]!.AsObject());

        // seo mặc định: mọi field null, không thiếu khoá nào.
        var seo = root["seo"]!.AsObject();
        foreach (var key in new[] { "defaultTitle", "defaultDescription", "ogImageId", "faviconAssetId" })
        {
            Assert.True(seo.ContainsKey(key));
            Assert.Null(seo[key]);
        }
    }

    [Fact]
    public void Theme_tokens_are_embedded_verbatim()
    {
        var (website, theme, pages) = SeedWebsite();

        var root = JsonNode.Parse(SnapshotBuilder.Build(website, theme, 1, pages, [], []))!.AsObject();

        Assert.Equal("spa-lavender", root["theme"]!["presetKey"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(theme.Tokens), root["theme"]!["tokens"]));
    }

    [Fact]
    public void Pages_carry_id_and_home_notfound_markers()
    {
        var (website, theme, pages) = SeedWebsite();

        var arr = JsonNode.Parse(SnapshotBuilder.Build(website, theme, 1, pages, [], []))!["pages"]!.AsArray();

        var home = arr.Single(p => p!["kind"]!.GetValue<string>() == "Composable");
        Assert.Equal("", home!["slug"]!.GetValue<string>());
        Assert.Null(home["systemType"]);
        Assert.False(string.IsNullOrEmpty(home["id"]!.GetValue<string>()));

        var notFound = arr.Single(p => p!["systemType"]?.GetValue<string>() == "NotFound");
        Assert.Null(notFound!["slug"]); // NotFound: slug null
        Assert.True(notFound["seo"]!["noIndex"]!.GetValue<bool>());

        // Mọi page có id là GUID hợp lệ và có đủ khoá seo.
        foreach (var page in arr)
        {
            Assert.True(Guid.TryParse(page!["id"]!.GetValue<string>(), out _));
            Assert.True(page["seo"]!.AsObject().ContainsKey("title"));
            Assert.NotNull(page["tree"]);
        }
    }

    [Fact]
    public void Pages_are_ordered_by_sort_order_then_title()
    {
        var shopId = Guid.NewGuid();
        var websiteId = Guid.NewGuid();
        var website = WebsiteEntity.Create(shopId, id: websiteId);
        var theme = SeedTheme(websiteId, shopId);
        var tree = EmptyComposableTree();

        // Thứ tự đưa vào cố ý lộn xộn: (sort,title) = (2,"B"), (1,"Z"), (1,"A"), (2,"A")
        var pages = new List<SnapshotPage>
        {
            new(Page.NewComposable(shopId, websiteId, "p2b", "B", 2), tree),
            new(Page.NewComposable(shopId, websiteId, "p1z", "Z", 1), tree),
            new(Page.NewComposable(shopId, websiteId, "p1a", "A", 1), tree),
            new(Page.NewComposable(shopId, websiteId, "p2a", "A", 2), tree),
        };

        var titles = JsonNode.Parse(SnapshotBuilder.Build(website, theme, 1, pages, [], []))!["pages"]!.AsArray()
            .Select(p => p!["title"]!.GetValue<string>()).ToArray();

        Assert.Equal(new[] { "A", "Z", "A", "B" }, titles); // sort 1 (A,Z) rồi sort 2 (A,B)
    }

    [Fact]
    public void Media_table_uses_lowercase_guid_keys_and_preset_null_for_library()
    {
        var (website, theme, pages) = SeedWebsite();
        var clone = Guid.NewGuid();
        var library = Guid.NewGuid();
        var media = new List<SnapshotMedia>
        {
            new(clone, "shops/x/clone.webp", "1600x900,cover"),
            new(library, "shops/x/orig.webp", null),
        };

        var table = JsonNode.Parse(SnapshotBuilder.Build(website, theme, 1, pages, media, []))!["media"]!.AsObject();

        Assert.Equal(2, table.Count);
        Assert.Equal("shops/x/clone.webp", table[clone.ToString()]!["storageKey"]!.GetValue<string>());
        Assert.Equal("1600x900,cover", table[clone.ToString()]!["preset"]!.GetValue<string>());
        Assert.Null(table[library.ToString()]!["preset"]);
        Assert.Equal(clone.ToString(), clone.ToString().ToLowerInvariant()); // GUID "D" đã là chữ thường

        // Snapshot có media vẫn hợp lệ theo schema.
        Assert.Empty(WebsiteHandlerKit.S.Schemas.ValidateSiteSnapshot(SnapshotBuilder.Build(website, theme, 1, pages, media, [])));
    }

    // ---- WEBSITE-007 T4 (#113): navigation.items ----

    private static NavItem PageNav(Guid id, string? label = null, params NavItem[] children) =>
        new(NavItem.PageKey(id), label, null, false, children);

    private static NavItem CustomNav(string label, string url) => new(NavItem.CustomKey, label, url, false, []);

    [Fact]
    public void Navigation_items_with_three_levels_are_written_as_keys_and_validate_against_schema()
    {
        var (website, theme, pages) = SeedWebsite();
        var home = pages[0].Page.Id;
        var contact = pages.Single(p => p.Page.SystemType == SystemType.Contact).Page.Id;
        var nav = new[]
        {
            PageNav(home, null, PageNav(contact, "Liên hệ", CustomNav("Zalo", "https://zalo.me/0901"))),
            CustomNav("Gọi ngay", "tel:0901234567"),
        };

        var json = SnapshotBuilder.Build(website, theme, 1, pages, [], nav);

        Assert.Empty(WebsiteHandlerKit.S.Schemas.ValidateSiteSnapshot(json));
        var expected = JsonNode.Parse(
            "[{\"key\":\"page:" + home + "\",\"label\":null,\"children\":[" +
            "{\"key\":\"page:" + contact + "\",\"label\":\"Liên hệ\",\"children\":[" +
            "{\"key\":\"custom\",\"label\":\"Zalo\",\"url\":\"https://zalo.me/0901\"}]}]}," +
            "{\"key\":\"custom\",\"label\":\"Gọi ngay\",\"url\":\"tel:0901234567\"}]");
        Assert.True(
            JsonNode.DeepEquals(expected, JsonNode.Parse(json)!["navigation"]!["items"]),
            JsonNode.Parse(json)!["navigation"]!.ToJsonString());
    }

    [Fact]
    public void Navigation_page_key_is_lowercase_guid_like_pages_id_even_if_stored_uppercase()
    {
        var (website, theme, pages) = SeedWebsite();
        var home = pages[0].Page.Id;
        var nav = new[] { new NavItem("page:" + home.ToString("D").ToUpperInvariant(), null, null, false, []) };

        var json = SnapshotBuilder.Build(website, theme, 1, pages, [], nav);

        Assert.Equal("page:" + home.ToString("D"), JsonNode.Parse(json)!["navigation"]!["items"]![0]!["key"]!.GetValue<string>());
        Assert.Empty(WebsiteHandlerKit.S.Schemas.ValidateSiteSnapshot(json));
    }

    [Fact]
    public void Snapshot_schema_rejects_a_fourth_level_and_a_hidden_flag()
    {
        var (website, theme, pages) = SeedWebsite();
        var root = JsonNode.Parse(SnapshotBuilder.Build(website, theme, 1, pages, [], []))!.AsObject();

        root["navigation"]!["items"] = JsonNode.Parse(
            "[{\"key\":\"custom\",\"label\":\"a\",\"url\":\"/a\",\"children\":[{\"key\":\"custom\",\"label\":\"b\",\"url\":\"/b\",\"children\":[" +
            "{\"key\":\"custom\",\"label\":\"c\",\"url\":\"/c\",\"children\":[{\"key\":\"custom\",\"label\":\"d\",\"url\":\"/d\"}]}]}]}]");
        Assert.NotEmpty(WebsiteHandlerKit.S.Schemas.ValidateSiteSnapshot(root.ToJsonString()));

        root["navigation"]!["items"] = JsonNode.Parse("[{\"key\":\"custom\",\"label\":\"a\",\"url\":\"/a\",\"hidden\":false}]");
        Assert.NotEmpty(WebsiteHandlerKit.S.Schemas.ValidateSiteSnapshot(root.ToJsonString()));
    }
}
