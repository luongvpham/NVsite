using Vsite.Application.Website.Validation;
using Vsite.Infrastructure.Website;

namespace Vsite.IntegrationTests.Website;

/// <summary>T5b, WEBSITE-001 (#46, R6) — khoá cấu trúc System page. Chạy fixture stored/incoming dùng chung. Không cần Docker.</summary>
public sealed class SystemPageGuardTests
{
    private static readonly TreeRulesCatalog Rules = new(TreeFixtures.Generated("tree-rules.json"));

    public static IEnumerable<object[]> PairCases() => TreeFixtures.PairCases();

    [Theory]
    [MemberData(nameof(PairCases))]
    public void Fixture_pair_is_compared_as_labelled(string fileName)
    {
        var fixture = TreeFixtures.Load(fileName);

        var errors = SystemPageGuard.Compare(fixture["stored"]!.ToJsonString(), fixture["incoming"]!.ToJsonString(), Rules);

        if (fixture["expect"]!.GetValue<string>() == "valid")
        {
            Assert.True(errors.Count == 0, $"{fileName}: " + string.Join("; ", errors.Select(e => $"{e.Path} {e.Code}")));
        }
        else
        {
            var expected = fixture["errorCodes"]!.AsArray().Select(n => n!.GetValue<string>()).ToHashSet();
            Assert.True(expected.SetEquals(errors.Select(e => e.Code)), $"{fileName}: có [{string.Join(", ", errors.Select(e => e.Code))}]");
        }
    }

    [Fact]
    public void Editable_props_come_from_tree_rules_for_the_node_type()
    {
        var stored = Tree("""{ "content": "<p>a</p>" }""");
        var edited = Tree("""{ "content": "<p>b</p>" }""");

        Assert.Empty(SystemPageGuard.Compare(stored, edited, Rules));
        // Truyền tay danh sách editable rỗng: đổi content bị chặn.
        var error = Assert.Single(SystemPageGuard.Compare(stored, edited, _ => []));
        Assert.Equal("SYSTEM_PAGE_PROP_NOT_EDITABLE", error.Code);
        Assert.Equal("/children/0/props/content", error.Path);
    }

    [Fact]
    public void Non_editable_prop_added_removed_or_changed_is_detected_and_key_order_is_irrelevant()
    {
        var stored = Tree("""{ "content": "<p>a</p>", "align": "left" }""");

        Assert.Empty(SystemPageGuard.Compare(stored, Tree("""{ "align": "left", "content": "<p>a</p>" }"""), Rules));
        Assert.Equal(["SYSTEM_PAGE_PROP_NOT_EDITABLE"],
            SystemPageGuard.Compare(stored, Tree("""{ "content": "<p>a</p>" }"""), Rules).Select(e => e.Code));
        Assert.Equal(["SYSTEM_PAGE_PROP_NOT_EDITABLE"],
            SystemPageGuard.Compare(stored, Tree("""{ "content": "<p>a</p>", "align": "left", "extra": 1 }"""), Rules).Select(e => e.Code));
    }

    [Fact]
    public void Canonical_comparison_ignores_number_formatting_and_nested_key_order()
    {
        var stored = Tree("""{ "content": "x", "align": { "b": 1, "a": [1, 2] } }""");
        var incoming = Tree("""{ "content": "x", "align": { "a": [1.0, 2], "b": 1.00 } }""");

        Assert.Empty(SystemPageGuard.Compare(stored, incoming, Rules));
        Assert.NotEmpty(SystemPageGuard.Compare(stored, Tree("""{ "content": "x", "align": { "a": [2, 1], "b": 1 } }"""), Rules));
    }

    [Fact]
    public void Malformed_documents_are_reported_not_thrown()
    {
        var errors = SystemPageGuard.Compare("{ nope", Tree("{}"), Rules);

        Assert.Equal(["SYSTEM_PAGE_STRUCTURE_CHANGED"], errors.Select(e => e.Code));
    }

    [Fact]
    public void Deep_trees_do_not_overflow_the_stack()
    {
        var open = string.Concat(Enumerable.Range(0, 30).Select(i => $$"""{ "id": "c_{{i:D5}}", "type": "Section", "variant": "Section01", "children": ["""));
        var close = string.Concat(Enumerable.Repeat("] }", 30));
        var json = $$"""{ "schemaVersion": 1, "id": "root", "type": "Page", "children": [{{open}}{{close}}] }""";

        Assert.Empty(SystemPageGuard.Compare(json, json, Rules));
    }

    private static string Tree(string props) =>
        $$"""{ "schemaVersion": 1, "id": "root", "type": "Page", "children": [ { "id": "c_cont1", "type": "RichText", "variant": "RichText01", "props": {{props}} } ] }""";
}
