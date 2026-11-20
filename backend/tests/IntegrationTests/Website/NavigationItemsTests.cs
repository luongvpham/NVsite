using Vsite.Application.Website.Navigation;
using Vsite.Application.Website.Versioning;
using Vsite.Domain.Website.Enums;

namespace Vsite.IntegrationTests.Website;

/// <summary>T2 (WEBSITE-007, #113) — hàm thuần ghép/kiểm/lọc menu và đọc/ghi tài liệu. Không cần DB.</summary>
public sealed class NavigationItemsTests
{
    private static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid(), NF = Guid.NewGuid();
    private static readonly DocumentVersioning Versioning = new([]);

    private static NavPage P(Guid id, string title, int order, SystemType? st = null, bool enabled = true) => new(id, title, st, enabled, order);

    private static readonly NavPage[] Pages =
    [
        P(C, "Cc", 3),
        P(NF, "404", 0, SystemType.NotFound),
        P(B, "Bb", 2),
        P(A, "Aa", 2),
    ];

    private static NavItem Pg(Guid id, string? label = null, bool hidden = false, params NavItem[] children) =>
        new(NavItem.PageKey(id), label, null, hidden, children);

    private static NavItem Custom(string? label, string? url, bool hidden = false) => new("custom", label, url, hidden, []);

    private static string[] Keys(IEnumerable<NavItem> items) => items.Select(i => i.Key).ToArray();

    private static string[] Errs(IEnumerable<NavError> e) => e.Select(x => $"{x.Path} {x.Code}").ToArray();

    [Fact]
    public void Merge_NoStored_AutoGeneratesBySortOrderThenTitle_SkipsNotFound_KeepsDisabled()
    {
        var pages = Pages.Append(P(Guid.NewGuid(), "Off", 9, enabled: false)).ToArray();
        var merged = NavigationItems.Merge(null, pages);
        Assert.Equal(4, merged.Count);
        Assert.Equal([NavItem.PageKey(A), NavItem.PageKey(B), NavItem.PageKey(C)], Keys(merged.Take(3)));
        Assert.All(merged, i => Assert.True(!i.Hidden && i.Label is null && i.Url is null && i.Children.Count == 0));
        Assert.DoesNotContain(NavItem.PageKey(NF), Keys(merged));
    }

    [Fact]
    public void Merge_NewPageAppendedHiddenAtEnd()
    {
        var merged = NavigationItems.Merge([Pg(B), Pg(A)], Pages);
        Assert.Equal([NavItem.PageKey(B), NavItem.PageKey(A), NavItem.PageKey(C)], Keys(merged));
        Assert.True(merged[2].Hidden);
        Assert.False(merged[0].Hidden);
    }

    [Fact]
    public void Merge_PageInsideGroupIsNotReAppended()
    {
        var merged = NavigationItems.Merge([Pg(A, children: Pg(B))], Pages);
        Assert.Equal([NavItem.PageKey(A), NavItem.PageKey(C)], Keys(merged));
        Assert.Single(merged[0].Children);
    }

    [Fact]
    public void Merge_DeletedPageDroppedWithItsChildren_DeletedChildDropped_CustomKept()
    {
        var stored = new[]
        {
            Pg(Guid.NewGuid(), children: Pg(B)),
            Pg(A, children: [Pg(Guid.NewGuid()), Custom("x", "https://x.vn")]),
        };
        var merged = NavigationItems.Merge(stored, Pages);
        // B (con của cha đã xoá) bị bỏ cùng cha, nên được nối lại cuối, ẩn, theo SortOrder/Title.
        Assert.Equal([NavItem.PageKey(A), NavItem.PageKey(B), NavItem.PageKey(C)], Keys(merged));
        Assert.True(Assert.Single(merged[0].Children).IsCustom);
        Assert.True(merged[1].Hidden && merged[2].Hidden);
    }

    [Fact]
    public void Validate_Valid_NoErrors()
    {
        var items = new[] { Pg(A, "Home", children: [Pg(B), Custom("Fb", "https://fb.com/x")]), Custom("Mail", "mailto:a@b.vn") };
        Assert.Empty(NavigationItems.Validate(items, Pages));
    }

    [Fact]
    public void Validate_TooDeep_PathPointsAtGrandchildren()
    {
        var items = new[] { Pg(A, children: Pg(B, children: Pg(C))) };
        Assert.Equal(["/items/0/children/0/children NAV_TOO_DEEP"], Errs(NavigationItems.Validate(items, Pages)));
    }

    [Fact]
    public void Validate_PageNotFound_AndMalformedKey()
    {
        var items = new[] { Pg(Guid.NewGuid()), new NavItem("page:nope", null, null, false, []) };
        Assert.Equal(
            ["/items/0/key NAV_PAGE_NOT_FOUND", "/items/1/key NAV_PAGE_NOT_FOUND"],
            Errs(NavigationItems.Validate(items, Pages)));
    }

    [Fact]
    public void Validate_Duplicate_FlagsSecondOccurrenceAcrossLevels()
    {
        var items = new[] { Pg(A), Pg(B, children: Pg(A)) };
        Assert.Equal(["/items/1/children/0/key NAV_PAGE_DUPLICATE"], Errs(NavigationItems.Validate(items, Pages)));
    }

    [Fact]
    public void Validate_NotFoundPageNotAllowed_ButOtherSystemPagesAllowed()
    {
        var contact = Guid.NewGuid();
        var pages = Pages.Append(P(contact, "Contact", 5, SystemType.Contact)).ToArray();
        Assert.Equal(["/items/0/key NAV_PAGE_NOT_ALLOWED"], Errs(NavigationItems.Validate([Pg(NF), Pg(contact)], pages)));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("  javascript:alert(1)")]
    [InlineData("java&#x09;script:alert(1)")]
    [InlineData("//evil.com")]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_UnsafeCustomUrl(string? url)
    {
        var items = new[] { Pg(A, children: Custom("x", url)) };
        Assert.Equal(["/items/0/children/0/url NAV_URL_UNSAFE"], Errs(NavigationItems.Validate(items, Pages)));
    }

    [Fact]
    public void Validate_UrlTooLong_IsUnsafe()
    {
        var url = "https://x.vn/" + new string('a', 2048);
        Assert.Equal(["/items/0/url NAV_URL_UNSAFE"], Errs(NavigationItems.Validate([Custom("x", url)], Pages)));
    }

    [Fact]
    public void Validate_LabelRules_TrimAndLength()
    {
        var errors = NavigationItems.Validate(
            [Custom("   ", "/x"), Custom(null, "/x"), Pg(A, "   "), Pg(B, new string('a', 61)), Pg(C, "  " + new string('a', 60) + "  ")], Pages);
        Assert.Equal(
            ["/items/0/label NAV_LABEL_REQUIRED", "/items/1/label NAV_LABEL_REQUIRED", "/items/3/label NAV_LABEL_TOO_LONG"],
            Errs(errors));
    }

    [Fact]
    public void Normalize_TrimsLabel_EmptyBecomesNull_PageDropsUrl_KeepsHtmlVerbatim()
    {
        var items = new[] { Pg(A, "  <b>Hi</b> "), Pg(B, "   "), new NavItem(NavItem.PageKey(C), null, "/x", false, []), Custom(" L ", " /x ") };
        var n = NavigationItems.Normalize(items);
        Assert.Equal("<b>Hi</b>", n[0].Label);
        Assert.Null(n[1].Label);
        Assert.Null(n[2].Url);
        Assert.Equal("L", n[3].Label);
        Assert.Equal("/x", n[3].Url);
    }

    [Fact]
    public void Validate_TooManyItems()
    {
        var top = Enumerable.Range(0, 13).Select(_ => Custom("x", "/x")).ToArray();
        Assert.Equal(["/items NAV_TOO_MANY_ITEMS"], Errs(NavigationItems.Validate(top, Pages)));
        var kids = Enumerable.Range(0, 11).Select(_ => Custom("x", "/x")).ToArray();
        Assert.Equal(["/items/0/children NAV_TOO_MANY_ITEMS"], Errs(NavigationItems.Validate([Pg(A, children: kids)], Pages)));
        Assert.Empty(NavigationItems.Validate(top.Take(12).ToArray(), Pages));
    }

    [Fact]
    public void ForSnapshot_DropsHidden_DisabledPages_AndWholeGroupWhenParentOut()
    {
        var published = new HashSet<Guid> { A, C };
        var items = new[]
        {
            Pg(A, "Home", children: [Pg(B), Pg(C), Custom("x", "/x", hidden: true), Custom("y", "/y")]),
            Pg(B, children: Pg(C)),
            Pg(C, hidden: true),
            Custom("ext", "https://x.vn"),
        };
        var snap = NavigationItems.ForSnapshot(items, published);
        Assert.Equal(2, snap.Count);
        Assert.Equal(NavItem.PageKey(A), snap[0].Key);
        Assert.Equal([NavItem.PageKey(C), "custom"], Keys(snap[0].Children));
        Assert.True(snap[1].IsCustom);
    }

    [Fact]
    public void SerializeParse_RoundTrip_CamelCaseWithSchemaVersion()
    {
        var items = new[] { Pg(A, "Home", children: [Pg(B), Custom("Fb", "https://fb.com")]), Pg(C, hidden: true) };
        var json = NavigationItems.Serialize(items, Versioning);
        Assert.Contains("\"schemaVersion\":1", json);
        Assert.Contains("\"items\":[", json);
        Assert.Contains("\"hidden\":true", json);
        var back = NavigationItems.Parse(json, Versioning);
        Assert.Equal(NavigationItems.Serialize(items, Versioning), NavigationItems.Serialize(back, Versioning));
        Assert.Equal(Keys(items), Keys(back));
        Assert.Equal("https://fb.com", back[0].Children[1].Url);
        Assert.True(back[1].Hidden);
        Assert.Equal("Home", back[0].Label);
    }

    [Fact]
    public void Parse_Malformed_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => NavigationItems.Parse("{\"items\":[]}", Versioning));
    }
}
