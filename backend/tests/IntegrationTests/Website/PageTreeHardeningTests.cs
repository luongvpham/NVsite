using Vsite.Application.Website.Validation;
using Vsite.Domain.Website.Enums;
using Vsite.Infrastructure.Website;

namespace Vsite.IntegrationTests.Website;

/// <summary>
/// WEBSITE-001 fix round 1 (review đợt B): JSON độc hại không được gây 500, binding thiếu source, link url nguy hiểm,
/// imageId sai kiểu, kiểm khởi động tree-rules ↔ props schema. Không cần Docker.
/// </summary>
public sealed class PageTreeHardeningTests
{
    private static TreeValidationResult Validate(string json) =>
        TreeFixtures.CreateValidator().Validate(json, PageKind.Composable);

    [Theory]
    [InlineData("""{"schemaVersion":1,"schemaVersion":1,"id":"root","type":"Page","children":[]}""")]
    [InlineData("""{"schemaVersion":1,"id":"root","type":"Page","children":[{"id":"c_hero1","type":"Hero","variant":"Hero01","props":{"title":"a","title":"b"}}]}""")]
    [InlineData("""{"schemaVersion":1,"id":"root","type":"Page","children":[{"id":"c_hero1","type":"Hero","props":{"cta":{"label":"a","label":"b"}}}]}""")]
    [InlineData("""{"schemaVersion":1,"id":"root","type":"Page","children":[{"id":"c_hero1","id":"c_hero2","type":"Hero"}]}""")]
    public void Duplicate_json_keys_are_an_envelope_error_not_a_500(string json)
    {
        var result = Validate(json);

        var error = Assert.Single(result.Errors);
        Assert.Equal("TREE_ENVELOPE_INVALID", error.Code);
        Assert.Equal("TREE_INVALID", result.ProblemCode);
    }

    [Fact]
    public void Same_key_in_sibling_objects_is_not_a_duplicate()
    {
        var result = Validate("""{"schemaVersion":1,"id":"root","type":"Page","children":[{"id":"c_sect1","type":"Section","variant":"Section01"},{"id":"c_sect2","type":"Section","variant":"Section01"}]}""");

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => $"{e.Path} {e.Code}")));
    }

    [Theory]
    [InlineData("""{"schemaVersion":1,"id":"root","type":"Page","children":[{"id":"c_rich1","type":"RichText","variant":"RichText01","props":{"content":"x\uD800"}}]}""")]
    [InlineData("""{"schemaVersion":1,"id":"root","type":"Page","children":[{"id":"c_rich1","type":"RichText","variant":"RichText01","props":{"content":"\uDC00x"}}]}""")]
    [InlineData("""{"schemaVersion":1,"id":"root","type":"Page","children":[{"id":"c_rich1","type":"RichText","variant":"RichText01","props":{"a\uD800":1}}]}""")]
    [InlineData("""{"schemaVersion":1,"id":"c_\uD800root","type":"Page","children":[]}""")]
    public void Lone_surrogates_are_an_envelope_error_not_a_500(string json)
    {
        var result = Validate(json);

        var error = Assert.Single(result.Errors);
        Assert.Equal("TREE_ENVELOPE_INVALID", error.Code);
    }

    [Fact]
    public void Valid_surrogate_pair_is_accepted()
    {
        var result = Validate("""{"schemaVersion":1,"id":"root","type":"Page","children":[{"id":"c_hero1","type":"Hero","variant":"Hero01","props":{"title":"vui 😀"}}]}""");

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => $"{e.Path} {e.Code}")));
    }

    [Fact]
    public void System_page_guard_does_not_throw_on_duplicate_keys()
    {
        var dup = """{"schemaVersion":1,"id":"root","type":"Page","children":[{"id":"c_cont1","type":"RichText","variant":"RichText01","props":{"content":"a","content":"b"}}]}""";
        var rules = new TreeRulesCatalog(TreeFixtures.Generated("tree-rules.json"));

        var errors = SystemPageGuard.Compare(dup, dup, rules);

        Assert.Equal(["SYSTEM_PAGE_STRUCTURE_CHANGED"], errors.Select(e => e.Code));
    }

    [Theory]
    [InlineData("""{"source":"Service"}""", true)]
    [InlineData("""{}""", false)]
    [InlineData("""{"source":null}""", false)]
    [InlineData("""{"source":7}""", false)]
    [InlineData("""{"source":{}}""", false)]
    [InlineData("""{"source":"Review"}""", false)]
    [InlineData("""{"source":"Product"}""", false)]
    public void Binding_source_must_be_a_declared_string(string binding, bool valid)
    {
        var json = $$$"""{"schemaVersion":1,"id":"root","type":"Page","children":[{"id":"c_grid1","type":"ServiceGrid","variant":"ServiceGrid01","props":{"source":{{{binding}}}}}]}""";

        var result = Validate(json);

        if (valid)
        {
            Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => $"{e.Path} {e.Code}")));
            return;
        }

        Assert.Contains(result.Errors, e => e.Code == "BINDING_SOURCE_NOT_ALLOWED" && e.Path == "/children/0/props/source/source");
    }

    [Theory]
    [InlineData("https://vsite.vn/x?a=1&b=2", true)]
    [InlineData("HTTP://vsite.vn", true)]
    [InlineData("mailto:a@b.vn", true)]
    [InlineData("tel:+84123", true)]
    [InlineData("/lien-he", true)]
    [InlineData("/", true)]
    [InlineData("#top", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("JaVa\\tScript:alert(1)", false)]
    [InlineData("java\\nscript:alert(1)", false)]
    [InlineData("  javascript:alert(1)", false)]
    [InlineData("java&#115;cript&colon;alert(1)", false)]
    [InlineData("java&#x73;cript:alert(1)", false)]
    [InlineData("data:text/html,hi", false)]
    [InlineData("vbscript:msgbox(1)", false)]
    [InlineData("ftp://x.vn", false)]
    [InlineData("//evil.example", false)]
    [InlineData("/\\\\evil.example", false)]
    [InlineData("evil.example/x", false)]
    [InlineData("", false)]
    public void External_link_url_is_checked_by_scheme_whitelist(string url, bool valid)
    {
        var json = """{"schemaVersion":1,"id":"root","type":"Page","children":[{"id":"c_hero1","type":"Hero","variant":"Hero01","props":{"cta":{"label":"x","target":{"kind":"external","url":"@@"}}}}]}""".Replace("@@", url);

        var result = Validate(json);

        if (valid)
        {
            Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => $"{e.Path} {e.Code}")));
            return;
        }

        var error = Assert.Single(result.Errors, e => e.Code == "LINK_URL_UNSAFE");
        Assert.Equal("/children/0/props/cta/target/url", error.Path);
    }

    [Theory]
    [InlineData("""{"kind":"external"}""")]
    [InlineData("""{"kind":"external","url":null}""")]
    [InlineData("""{"kind":"external","url":5}""")]
    public void External_link_without_a_string_url_is_unsafe(string target)
    {
        var json = """{"schemaVersion":1,"id":"root","type":"Page","children":[{"id":"c_hero1","type":"Hero","variant":"Hero01","props":{"cta":{"label":"x","target":@@}}}]}""".Replace("@@", target);

        Assert.Contains(Validate(json).Errors, e => e.Code == "LINK_URL_UNSAFE");
    }

    [Fact]
    public void Non_external_link_kinds_carry_no_url_and_pass()
    {
        var json = """{"schemaVersion":1,"id":"root","type":"Page","children":[{"id":"c_hero1","type":"Hero","variant":"Hero01","props":{"cta":{"label":"x","target":{"kind":"page","pageId":"abc"}}}}]}""";

        Assert.True(Validate(json).IsValid);
    }

    [Fact]
    public void Link_url_inside_a_list_item_is_checked_too()
    {
        var overrideTypes = System.Text.Json.Nodes.JsonNode.Parse("""
            {
              "Links": {
                "acceptsChildren": false, "allowedChildTypes": null, "allowedInPageKinds": ["Composable"],
                "maxPerPage": null, "editableInSystemPageProps": [], "variants": ["Links01"],
                "props": { "items": { "kind": "list", "itemProps": { "to": { "kind": "link" } } } }
              }
            }
            """)!.AsObject();
        var path = TreeFixtures.MergeRulesOverride(overrideTypes);
        try
        {
            var json = """{"schemaVersion":1,"id":"root","type":"Page","children":[{"id":"c_link1","type":"Links","variant":"Links01","props":{"items":[{"to":{"kind":"external","url":"/ok"}},{"to":{"kind":"external","url":"javascript:alert(1)"}}]}}]}""";

            var error = Assert.Single(TreeFixtures.CreateValidator(path).Validate(json, PageKind.Composable).Errors);

            Assert.Equal("LINK_URL_UNSAFE", error.Code);
            Assert.Equal("/children/0/props/items/1/to/url", error.Path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Image_id_that_is_not_a_string_is_prop_invalid()
    {
        var json = """{"schemaVersion":1,"id":"root","type":"Page","children":[{"id":"c_hero1","type":"Hero","variant":"Hero01","props":{"image":{"imageId":123}}}]}""";

        var result = Validate(json);

        Assert.Contains(result.Errors, e => e.Code == "PROP_INVALID" && e.Path.StartsWith("/children/0/props/image", StringComparison.Ordinal));
        Assert.Empty(result.ImageIds);
    }

    [Fact]
    public void Every_tree_rules_type_has_a_props_schema_in_the_real_artifacts()
    {
        var rules = new TreeRulesCatalog(TreeFixtures.Generated("tree-rules.json"));
        var schemas = new WebsiteSchemaProvider(
            TreeFixtures.Generated("tree-envelope.schema.json"), TreeFixtures.Generated("theme-tokens.schema.json"), TreeFixtures.Generated("props-schemas.json"));

        WebsiteConfigConsistency.EnsureEveryTypeHasPropsSchema(rules, schemas);
    }

    [Fact]
    public void A_type_without_a_props_schema_fails_fast_naming_the_type()
    {
        var path = TreeFixtures.MergeRulesOverride(System.Text.Json.Nodes.JsonNode.Parse("""
            { "Ghost": { "acceptsChildren": false, "allowedChildTypes": null, "allowedInPageKinds": ["Composable"],
              "maxPerPage": null, "editableInSystemPageProps": [], "variants": ["Ghost01"], "props": {} } }
            """)!.AsObject());
        try
        {
            var rules = new TreeRulesCatalog(path);
            var schemas = new WebsiteSchemaProvider(
                TreeFixtures.Generated("tree-envelope.schema.json"), TreeFixtures.Generated("theme-tokens.schema.json"), TreeFixtures.Generated("props-schemas.json"));

            var ex = Assert.Throws<InvalidOperationException>(() => WebsiteConfigConsistency.EnsureEveryTypeHasPropsSchema(rules, schemas));
            Assert.Contains("Ghost", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
