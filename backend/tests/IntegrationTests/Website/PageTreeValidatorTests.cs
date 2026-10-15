using System.Text;
using System.Text.Json.Nodes;
using Vsite.Application.Website.Options;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Website.Enums;

namespace Vsite.IntegrationTests.Website;

/// <summary>
/// T4, WEBSITE-001 (#60, #67) — validate TOÀN cây. Không cần Docker. Một theory duyệt ĐÚNG thư mục fixture dùng chung
/// với FE (packages/builder-components/tests/fixtures/tree); thêm ca R3, R8, R12, idempotent.
/// </summary>
public sealed class PageTreeValidatorTests
{
    public static IEnumerable<object[]> TreeCases() => TreeFixtures.TreeCases();

    [Fact]
    public void Fixture_directory_has_the_expected_shape()
    {
        // Chống theory rỗng/âm thầm mất fixture khi đường dẫn đổi.
        Assert.True(TreeFixtures.TreeCases().Count() >= 25, "thiếu fixture cây");
        Assert.True(TreeFixtures.PairCases().Count() >= 7, "thiếu fixture stored/incoming");
    }

    [Theory]
    [MemberData(nameof(TreeCases))]
    public async Task Fixture_is_validated_as_labelled(string fileName)
    {
        var fixture = TreeFixtures.Load(fileName);
        var pageKind = Enum.Parse<PageKind>(fixture["pageKind"]!.GetValue<string>());
        var overrideTypes = fixture["rulesOverride"]?["types"]?.AsObject();
        var rulesPath = overrideTypes is null ? null : TreeFixtures.MergeRulesOverride(overrideTypes);
        try
        {
            var validator = TreeFixtures.CreateValidator(rulesPath);
            var treeJson = fixture["tree"]!.ToJsonString();

            var result = validator.Validate(treeJson, pageKind);
            var codes = result.Errors.Select(e => e.Code).ToHashSet();

            // Ảnh (h): fake IMediaReferenceValidator dựng từ context.media; chạy khi cây đã qua validate cấu trúc.
            if (result.IsValid)
            {
                var media = new TreeFixtures.FixtureMediaValidator(fixture["context"]?["media"]?.AsObject());
                try
                {
                    await media.EnsureValidAsync(result.ImageIds.Select(TreeFixtures.ToGuid).ToList(), [], default);
                }
                catch (UnprocessableException ex)
                {
                    codes.Add(ex.ErrorCode);
                }
            }

            if (fixture["expect"]!.GetValue<string>() == "valid")
            {
                Assert.True(codes.Count == 0, $"{fileName}: không mong lỗi nhưng có [{string.Join(", ", codes)}]: " +
                    string.Join("; ", result.Errors.Select(e => $"{e.Path} {e.Code} {e.Message}")));
                var expectSanitized = fixture["expectSanitized"]?.GetValue<bool>() ?? false;
                Assert.Equal(expectSanitized, result.Sanitized);

                var expectedTree = fixture["expectedTree"] ?? fixture["tree"];
                Assert.True(JsonNode.DeepEquals(expectedTree, JsonNode.Parse(result.CleanTree!)),
                    $"{fileName}: cây lưu khác mong đợi: {result.CleanTree}");
            }
            else
            {
                var expected = fixture["errorCodes"]!.AsArray().Select(n => n!.GetValue<string>()).ToHashSet();
                Assert.True(expected.SetEquals(codes),
                    $"{fileName}: mong [{string.Join(", ", expected.Order())}] nhưng có [{string.Join(", ", codes.Order())}]");
                if (!result.IsValid)
                {
                    Assert.Null(result.CleanTree);
                }
            }
        }
        finally
        {
            if (rulesPath is not null)
            {
                File.Delete(rulesPath);
            }
        }
    }

    [Theory]
    [MemberData(nameof(TreeCases))]
    public void Valid_fixture_is_idempotent_after_save(string fileName)
    {
        var fixture = TreeFixtures.Load(fileName);
        if (fixture["expect"]!.GetValue<string>() != "valid")
        {
            return;
        }

        var pageKind = Enum.Parse<PageKind>(fixture["pageKind"]!.GetValue<string>());
        var validator = TreeFixtures.CreateValidator();

        var first = validator.Validate(fixture["tree"]!.ToJsonString(), pageKind);
        var second = validator.Validate(first.CleanTree!, pageKind);

        Assert.True(second.IsValid);
        Assert.False(second.Sanitized);
        Assert.Equal(first.CleanTree, second.CleanTree);
    }

    [Fact]
    public void Errors_carry_json_pointer_paths_and_all_errors_are_returned_at_once()
    {
        var tree = Tree(Node("c_hero1", "Hero", "Hero01", """{ "nope": 1, "overlayOpacity": "x" }"""), Node("c_dupe1", "Hero", "Hero09"));

        var result = TreeFixtures.CreateValidator().Validate(tree, PageKind.Composable);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "PROP_UNKNOWN" && e.Path == "/children/0/props/nope");
        Assert.Contains(result.Errors, e => e.Code == "PROP_INVALID" && e.Path == "/children/0/props/overlayOpacity");
        Assert.Contains(result.Errors, e => e.Code == "NODE_VARIANT_UNKNOWN" && e.Path == "/children/1/variant");
        Assert.Contains(result.Errors, e => e.Code == "NODE_MAX_PER_PAGE_EXCEEDED" && e.Path == "/children/1");
        Assert.All(result.Errors, e => Assert.False(string.IsNullOrWhiteSpace(e.Message)));
    }

    [Fact]
    public void Image_ids_are_collected_from_list_and_group_too()
    {
        var tree = Tree(
            Node("c_hero1", "Hero", "Hero01", """{ "image": { "imageId": "img_hero" } }"""),
            Node("c_gall1", "Gallery", "Gallery01", """{ "items": [ { "image": { "imageId": "img_a" } }, { "image": { "imageId": "img_b" } } ] }"""));

        var result = TreeFixtures.CreateValidator().Validate(tree, PageKind.Composable);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => $"{e.Path} {e.Code}")));
        Assert.Equal(["img_a", "img_b", "img_hero"], result.ImageIds.Order().ToArray());
    }

    // R3 — Review chỉ nằm sâu: list → group → binding (type giả qua rulesOverride), walker đi MỌI độ sâu.
    [Fact]
    public void R3_review_binding_is_found_in_group_inside_list()
    {
        var overrideTypes = JsonNode.Parse("""
            {
              "Deep": {
                "acceptsChildren": false, "allowedChildTypes": null, "allowedInPageKinds": ["Composable"],
                "maxPerPage": null, "editableInSystemPageProps": [], "variants": ["Deep01"],
                "props": { "rows": { "kind": "list", "itemProps": { "box": { "kind": "group", "itemProps": {
                  "feed": { "kind": "binding", "sources": ["Service", "Product"] } } } } } }
              }
            }
            """)!.AsObject();
        var path = TreeFixtures.MergeRulesOverride(overrideTypes);
        try
        {
            var tree = Tree(Node("c_deep1", "Deep", "Deep01",
                """{ "rows": [ { "box": { "feed": { "source": "Service" } } }, { "box": { "feed": { "source": "Review" } } } ] }"""));

            var result = TreeFixtures.CreateValidator(path).Validate(tree, PageKind.Composable);

            var error = Assert.Single(result.Errors);
            Assert.Equal("BINDING_SOURCE_NOT_ALLOWED", error.Code);
            Assert.Equal("/children/0/props/rows/1/box/feed/source", error.Path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // R8 — giới hạn cứng chạy TRƯỚC validate sâu; không stack overflow.
    [Fact]
    public void R8_tree_ten_thousand_levels_deep_is_rejected_without_stack_overflow()
    {
        var result = TreeFixtures.CreateValidator().Validate(NestedSections(10_000), PageKind.Composable);

        var error = Assert.Single(result.Errors);
        Assert.Equal("TREE_TOO_DEEP", error.Code);
        Assert.Equal("TREE_INVALID", result.ProblemCode);
    }

    [Fact]
    public void R8_depth_limit_is_inclusive_at_twenty_and_rejects_twenty_one()
    {
        var ok = TreeFixtures.CreateValidator().Validate(NestedSections(20), PageKind.Composable);
        Assert.True(ok.IsValid, string.Join("; ", ok.Errors.Select(e => $"{e.Path} {e.Code}")));

        var tooDeep = TreeFixtures.CreateValidator().Validate(NestedSections(21), PageKind.Composable);
        Assert.Equal(["TREE_TOO_DEEP"], tooDeep.Errors.Select(e => e.Code));
    }

    [Fact]
    public void R8_too_many_nodes_is_rejected_before_deep_validation()
    {
        var tree = Tree(Enumerable.Range(0, 2001).Select(i => Node($"c_{i:D6}", "Section", "Section01")).ToArray());

        var result = TreeFixtures.CreateValidator().Validate(tree, PageKind.Composable);

        Assert.Equal(["TREE_TOO_MANY_NODES"], result.Errors.Select(e => e.Code));

        var exactly = Tree(Enumerable.Range(0, 2000).Select(i => Node($"c_{i:D6}", "Section", "Section01")).ToArray());
        Assert.True(TreeFixtures.CreateValidator().Validate(exactly, PageKind.Composable).IsValid);
    }

    [Fact]
    public void R8_hundred_thousand_nodes_and_huge_richtext_are_rejected_by_body_size()
    {
        var many = Tree(Enumerable.Range(0, 100_000).Select(i => Node($"c_{i:D6}", "Section", "Section01")).ToArray());
        Assert.Equal(["TREE_BODY_TOO_LARGE"], TreeFixtures.CreateValidator().Validate(many, PageKind.Composable).Errors.Select(e => e.Code));

        var huge = Tree(Node("c_rich1", "RichText", "RichText01", $$"""{ "content": "<p>{{new string('a', 5_000_000)}}</p>" }"""));
        Assert.Equal(["TREE_BODY_TOO_LARGE"], TreeFixtures.CreateValidator().Validate(huge, PageKind.Composable).Errors.Select(e => e.Code));
    }

    [Fact]
    public void R8_limits_come_from_options_not_constants()
    {
        var limits = new PageTreeLimits { MaxNodes = 2, MaxDepth = 1, MaxBodyBytes = 10_000, MaxErrors = 50 };
        var validator = TreeFixtures.CreateValidator(limits: limits);

        Assert.Equal(["TREE_TOO_MANY_NODES"],
            validator.Validate(Tree(Node("c_aaaaa", "Section", "Section01"), Node("c_bbbbb", "Section", "Section01"), Node("c_ccccc", "Section", "Section01")), PageKind.Composable)
                .Errors.Select(e => e.Code));
        Assert.Equal(["TREE_TOO_DEEP"], validator.Validate(NestedSections(2), PageKind.Composable).Errors.Select(e => e.Code));
        Assert.Equal(["TREE_BODY_TOO_LARGE"],
            validator.Validate(Tree(Node("c_rich1", "RichText", "RichText01", $$"""{ "content": "{{new string('a', 20_000)}}" }""")), PageKind.Composable)
                .Errors.Select(e => e.Code));
    }

    [Fact]
    public void Errors_are_capped_at_max_errors()
    {
        var nodes = Enumerable.Range(0, 100).Select(i => Node($"c_{i:D6}", "NoSuchType", null)).ToArray();
        var validator = TreeFixtures.CreateValidator(limits: new PageTreeLimits { MaxErrors = 5 });

        var result = validator.Validate(Tree(nodes), PageKind.Composable);

        Assert.Equal(5, result.Errors.Count);
        Assert.True(result.ErrorsTruncated);
    }

    // R12 — không bao giờ 500: JSON hỏng, gốc không phải object, phiên bản lạ.
    [Theory]
    [InlineData("not json at all", "TREE_ENVELOPE_INVALID")]
    [InlineData("", "TREE_ENVELOPE_INVALID")]
    [InlineData("[]", "TREE_ENVELOPE_INVALID")]
    [InlineData("null", "TREE_ENVELOPE_INVALID")]
    [InlineData("42", "TREE_ENVELOPE_INVALID")]
    [InlineData("""{ "schemaVersion": 1.5, "id": "root", "type": "Page", "children": [] }""", "TREE_ENVELOPE_INVALID")]
    [InlineData("""{ "schemaVersion": null, "id": "root", "type": "Page", "children": [] }""", "TREE_ENVELOPE_INVALID")]
    [InlineData("""{ "schemaVersion": 0, "id": "root", "type": "Page", "children": [] }""", "TREE_ENVELOPE_INVALID")]
    [InlineData("""{ "schemaVersion": -3, "id": "root", "type": "Page", "children": [] }""", "TREE_ENVELOPE_INVALID")]
    [InlineData("""{ "schemaVersion": 999, "id": "root", "type": "Page", "children": [] }""", "SCHEMA_VERSION_UNSUPPORTED")]
    [InlineData("""{ "schemaVersion": 99999999999999999999, "id": "root", "type": "Page", "children": [] }""", "SCHEMA_VERSION_UNSUPPORTED")]
    [InlineData("""{ "schemaVersion": 999, "id": "toor", "type": "Page", "children": [] }""", "TREE_ENVELOPE_INVALID")]
    public void R12_bad_documents_return_a_result_not_an_exception(string json, string expectedCode)
    {
        var result = TreeFixtures.CreateValidator().Validate(json, PageKind.Composable);

        Assert.NotEmpty(result.Errors);
        Assert.All(result.Errors, e => Assert.Equal(expectedCode, e.Code));
        Assert.Equal(expectedCode == "SCHEMA_VERSION_UNSUPPORTED" ? "SCHEMA_VERSION_UNSUPPORTED" : "TREE_INVALID", result.ProblemCode);
    }

    [Fact]
    public void Richtext_unsafe_reports_path_of_the_prop()
    {
        var tree = Tree(Node("c_rich1", "RichText", "RichText01", """{ "content": "<p>x</p><script>1</script>" }"""));

        var result = TreeFixtures.CreateValidator().Validate(tree, PageKind.Composable);

        var error = Assert.Single(result.Errors);
        Assert.Equal("RICHTEXT_UNSAFE", error.Code);
        Assert.Equal("/children/0/props/content", error.Path);
    }

    [Fact]
    public void Richtext_with_non_dangerous_cuts_is_replaced_by_the_clean_value()
    {
        // Hero.subtitle (profile inline) cắt <p> ngoài whitelist nhưng không nguy hiểm → lưu bản sạch.
        var tree = Tree(Node("c_hero1", "Hero", "Hero01", """{ "subtitle": "<p>Hi</p><strong>x</strong>" }"""));

        var result = TreeFixtures.CreateValidator().Validate(tree, PageKind.Composable);

        Assert.True(result.IsValid);
        Assert.True(result.Sanitized);
        Assert.Equal("Hi<strong>x</strong>", JsonNode.Parse(result.CleanTree!)!["children"]![0]!["props"]!["subtitle"]!.GetValue<string>());
    }

    private static string NestedSections(int depth)
    {
        var sb = new StringBuilder("""{ "schemaVersion": 1, "id": "root", "type": "Page", "children": [""");
        for (var i = 0; i < depth; i++)
        {
            sb.Append($$"""{ "id": "c_{{i:D6}}", "type": "Section", "variant": "Section01", "children": [""");
        }

        for (var i = 0; i < depth; i++)
        {
            sb.Append("] }");
        }

        return sb.Append("] }").ToString();
    }

    private static string Node(string id, string type, string? variant, string? props = null) =>
        $$"""{ "id": "{{id}}", "type": "{{type}}"{{(variant is null ? "" : $$""", "variant": "{{variant}}" """)}}{{(props is null ? "" : $$""", "props": {{props}}""")}} }""";

    private static string Tree(params string[] nodes) =>
        $$"""{ "schemaVersion": 1, "id": "root", "type": "Page", "children": [{{string.Join(",", nodes)}}] }""";
}
