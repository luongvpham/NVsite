using Vsite.Application.Website.Publishing;
using Vsite.Application.Website.Seeding;
using Vsite.Application.Website.Validation;
using Vsite.Domain.Website.Enums;
using Vsite.Infrastructure.Website;

namespace Vsite.IntegrationTests.Website;

/// <summary>
/// T3, WEBSITE-004 — kiểm prop bắt buộc theo variant khi publish. Thuần (đọc artifact/config thật ở repo root),
/// không cần Docker. Checker chạy trên CleanTree nên test dựng thẳng shape cây đã sạch.
/// </summary>
public sealed class RequiredPropsCheckerTests
{
    private static RequiredPropsChecker Checker() =>
        new(new TreeRulesCatalog(TreeFixtures.Generated("tree-rules.json")));

    private static string Tree(params string[] nodes) =>
        $$"""{"schemaVersion":1,"id":"root","type":"Page","children":[{{string.Join(",", nodes)}}]}""";

    [Fact]
    public void Hero01_missing_image_reports_one_error_at_props_image()
    {
        var errors = Checker().Check(Tree("""{"id":"c_1","type":"Hero","variant":"Hero01","props":{"title":"Xin chào"}}"""));

        var error = Assert.Single(errors);
        Assert.Equal("/children/0/props/image", error.Path);
        Assert.Equal(TreeErrorCodes.RequiredPropMissing, error.Code);
    }

    [Fact]
    public void Hero01_with_title_and_image_is_valid()
    {
        var errors = Checker().Check(Tree(
            """{"id":"c_1","type":"Hero","variant":"Hero01","props":{"title":"Xin chào","image":{"imageId":"abc"}}}"""));

        Assert.Empty(errors);
    }

    [Fact]
    public void Hero02_does_not_require_image()
    {
        var errors = Checker().Check(Tree("""{"id":"c_1","type":"Hero","variant":"Hero02","props":{"title":"Xin chào"}}"""));

        Assert.Empty(errors);
    }

    [Fact]
    public void Text_prop_with_only_whitespace_is_missing()
    {
        var errors = Checker().Check(Tree("""{"id":"c_1","type":"Hero","variant":"Hero02","props":{"title":"   "}}"""));

        var error = Assert.Single(errors);
        Assert.Equal("/children/0/props/title", error.Path);
    }

    [Fact]
    public void RichText_with_no_visible_text_is_missing()
    {
        var errors = Checker().Check(Tree("""{"id":"c_1","type":"RichText","variant":"RichText01","props":{"content":"<p></p>"}}"""));

        var error = Assert.Single(errors);
        Assert.Equal("/children/0/props/content", error.Path);
    }

    [Fact]
    public void RichText_with_visible_text_is_valid()
    {
        var errors = Checker().Check(Tree(
            """{"id":"c_1","type":"RichText","variant":"RichText01","props":{"content":"<p>Nội dung</p>"}}"""));

        Assert.Empty(errors);
    }

    [Fact]
    public void List_prop_empty_is_missing()
    {
        var errors = Checker().Check(Tree(
            """{"id":"c_1","type":"Gallery","variant":"Gallery01","props":{"items":[]}}"""));

        var error = Assert.Single(errors);
        Assert.Equal("/children/0/props/items", error.Path);
    }

    [Fact]
    public void List_prop_with_items_is_valid()
    {
        var errors = Checker().Check(Tree(
            """{"id":"c_1","type":"Gallery","variant":"Gallery01","props":{"items":[{"image":{"imageId":"x"}}]}}"""));

        Assert.Empty(errors);
    }

    [Fact]
    public void Binding_prop_without_source_is_missing()
    {
        var errors = Checker().Check(Tree(
            """{"id":"c_1","type":"ServiceGrid","variant":"ServiceGrid01","props":{}}"""));

        var error = Assert.Single(errors);
        Assert.Equal("/children/0/props/source", error.Path);
    }

    [Fact]
    public void Absent_props_object_reports_each_required_prop()
    {
        var errors = Checker().Check(Tree("""{"id":"c_1","type":"Hero","variant":"Hero01"}"""));

        Assert.Equal(2, errors.Count); // image + title
        Assert.All(errors, e => Assert.Equal(TreeErrorCodes.RequiredPropMissing, e.Code));
    }

    [Fact]
    public void Nested_node_reports_full_path()
    {
        var errors = Checker().Check(Tree(
            """{"id":"c_sec","type":"Section","variant":"Section01","props":{},"children":[{"id":"c_h","type":"Hero","variant":"Hero02","props":{}}]}"""));

        var error = Assert.Single(errors);
        Assert.Equal("/children/0/children/0/props/title", error.Path);
    }

    [Fact]
    public void Empty_tree_has_no_errors()
    {
        var errors = Checker().Check("""{"schemaVersion":1,"id":"root","type":"Page","children":[]}""");

        Assert.Empty(errors);
    }

    [Fact]
    public void Seed_trees_have_no_required_prop_errors()
    {
        var validator = TreeFixtures.CreateValidator();
        var seeder = new WebsiteSeeder(new SystemPageCatalog(TreeFixtures.Config("system-pages.json")), validator);
        var checker = Checker();

        var home = seeder.EmptyTree();
        Assert.Empty(checker.Check(home));

        var seeded = seeder.EnsureSystemPages(Guid.NewGuid(), Guid.NewGuid(), Array.Empty<SystemType>(), 1);
        Assert.NotEmpty(seeded.Drafts); // Contact + NotFound
        foreach (var draft in seeded.Drafts)
        {
            Assert.Empty(checker.Check(draft.Tree));
        }
    }
}
