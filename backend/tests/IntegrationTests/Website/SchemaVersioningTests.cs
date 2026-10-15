using System.Text.Json.Nodes;
using Vsite.Application.Website.Versioning;

namespace Vsite.IntegrationTests.Website;

/// <summary>T5, WEBSITE-001 (#94, R12) — khung upcaster cho Tree và Tokens. Không cần Docker.</summary>
public sealed class SchemaVersioningTests
{
    /// <summary>Upcaster giả v0 → v1: đổi tên trường <c>title</c> thành <c>name</c>.</summary>
    private sealed class FakeV0ToV1(DocumentKind kind) : IDocumentUpcaster
    {
        public DocumentKind Kind => kind;
        public int FromVersion => 0;

        public JsonObject Upcast(JsonObject document)
        {
            if (document.Remove("title", out var title))
            {
                document["name"] = title;
            }

            return document;
        }
    }

    private sealed class FakeV1ToV2(DocumentKind kind) : IDocumentUpcaster
    {
        public DocumentKind Kind => kind;
        public int FromVersion => 1;

        public JsonObject Upcast(JsonObject document)
        {
            document["added"] = true;
            return document;
        }
    }

    private static DocumentVersioning Versioning(params IDocumentUpcaster[] upcasters) => new(upcasters);

    [Fact]
    public void Current_versions_are_one_for_tree_and_tokens()
    {
        var versioning = Versioning();

        Assert.Equal(1, versioning.Current(DocumentKind.Tree));
        Assert.Equal(1, versioning.Current(DocumentKind.Tokens));
        Assert.Equal(1, DocumentSchemaVersions.Tree);
    }

    [Theory]
    [InlineData(DocumentKind.Tree)]
    [InlineData(DocumentKind.Tokens)]
    public void Current_version_passes_through_untouched(DocumentKind kind)
    {
        var doc = JsonNode.Parse("""{ "schemaVersion": 1, "x": 1 }""");

        var result = Versioning(new FakeV0ToV1(kind)).Upcast(kind, doc);

        Assert.Equal(UpcastStatus.Current, result.Status);
        Assert.True(JsonNode.DeepEquals(doc, result.Document));
    }

    [Theory]
    [InlineData(DocumentKind.Tree)]
    [InlineData(DocumentKind.Tokens)]
    public void Old_version_is_upgraded_by_the_fake_upcaster(DocumentKind kind)
    {
        var doc = JsonNode.Parse("""{ "schemaVersion": 0, "title": "A" }""");

        var result = Versioning(new FakeV0ToV1(kind)).Upcast(kind, doc);

        Assert.Equal(UpcastStatus.Upcasted, result.Status);
        Assert.Equal(1, result.Document!["schemaVersion"]!.GetValue<int>());
        Assert.Equal("A", result.Document["name"]!.GetValue<string>());
        Assert.Null(result.Document["title"]);
    }

    [Fact]
    public void Upcasters_are_chained_to_the_current_version_and_are_per_kind()
    {
        var versioning = new DocumentVersioning(
            [new FakeV0ToV1(DocumentKind.Tree), new FakeV1ToV2(DocumentKind.Tree), new FakeV0ToV1(DocumentKind.Tokens)],
            treeCurrent: 2, tokensCurrent: 1);

        var tree = versioning.Upcast(DocumentKind.Tree, JsonNode.Parse("""{ "schemaVersion": 0, "title": "A" }"""));
        Assert.Equal(UpcastStatus.Upcasted, tree.Status);
        Assert.Equal(2, tree.Document!["schemaVersion"]!.GetValue<int>());
        Assert.True(tree.Document["added"]!.GetValue<bool>());

        // Tokens vẫn ở v1: không bị upcaster của Tree (v1→v2) đụng tới.
        var tokens = versioning.Upcast(DocumentKind.Tokens, JsonNode.Parse("""{ "schemaVersion": 1 }"""));
        Assert.Equal(UpcastStatus.Current, tokens.Status);
        Assert.Null(tokens.Document!["added"]);
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 2 }""")]
    [InlineData("""{ "schemaVersion": 999 }""")]
    [InlineData("""{ "schemaVersion": 99999999999999999999 }""")]
    public void Newer_than_current_is_unsupported(string json)
    {
        var result = Versioning().Upcast(DocumentKind.Tree, JsonNode.Parse(json));

        Assert.Equal(UpcastStatus.Unsupported, result.Status);
        Assert.Null(result.Document);
    }

    [Theory]
    [InlineData("""{ }""")]
    [InlineData("""{ "schemaVersion": "1" }""")]
    [InlineData("""{ "schemaVersion": 1.5 }""")]
    [InlineData("""{ "schemaVersion": null }""")]
    [InlineData("""{ "schemaVersion": true }""")]
    [InlineData("""{ "schemaVersion": [1] }""")]
    [InlineData("""[1]""")]
    [InlineData("""42""")]
    [InlineData("""null""")]
    public void Missing_or_non_integer_version_is_malformed_not_an_exception(string json)
    {
        var result = Versioning().Upcast(DocumentKind.Tree, JsonNode.Parse(json));

        Assert.Equal(UpcastStatus.Malformed, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public void Integral_decimal_like_1_0_counts_as_integer_version_one()
    {
        var result = Versioning().Upcast(DocumentKind.Tree, JsonNode.Parse("""{ "schemaVersion": 1.0 }"""));

        Assert.Equal(UpcastStatus.Current, result.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Version_below_one_without_an_upcaster_is_malformed(int version)
    {
        var result = Versioning().Upcast(DocumentKind.Tree, JsonNode.Parse($$"""{ "schemaVersion": {{version}} }"""));

        Assert.Equal(UpcastStatus.Malformed, result.Status);
    }

    [Fact]
    public void Duplicate_upcaster_for_the_same_step_fails_fast_at_construction()
    {
        Assert.Throws<InvalidOperationException>(() => Versioning(new FakeV0ToV1(DocumentKind.Tree), new FakeV0ToV1(DocumentKind.Tree)));
    }
}
