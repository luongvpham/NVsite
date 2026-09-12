using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace ComponentSchemaTests;

/// <summary>
/// Quyết định #60 phía .NET: BE không port Zod, chỉ validate bằng JSON Schema sinh cùng nguồn
/// với manifest. Test này chứng minh <c>JsonSchema.Net</c> đọc được
/// <c>packages/builder-components/generated/props-schemas.json</c> và cho verdict đúng —
/// tương đương các case đã test ở phía TS (schema-equivalence.test.ts).
///
/// Yêu cầu: đã chạy `pnpm gen:registry` trước (props-schemas.json là generated, .gitignore).
/// </summary>
public sealed class PropsSchemaEquivalenceTests
{
    private static readonly Lazy<JsonObject> PropsSchemas = new(LoadPropsSchemas);

    [Theory]
    [MemberData(nameof(HeroCases))]
    public void Hero_ValidatesLikeZod(string caseName, string json, bool expectedValid)
    {
        var schema = GetSchema("Hero");
        var element = JsonDocument.Parse(json).RootElement;

        var result = schema.Evaluate(element);

        Assert.True(result.IsValid == expectedValid, $"case '{caseName}': mong đợi {expectedValid}, JsonSchema.Net trả {result.IsValid}");
    }

    public static IEnumerable<object[]> HeroCases()
    {
        yield return new object[] { "empty (mọi prop optional)", "{}", true };
        yield return new object[] { "valid full", """{"title":"x","image":{"imageId":"a"},"align":"center","overlayOpacity":40}""", true };
        yield return new object[] { "overlayOpacity string thay vì number", """{"overlayOpacity":"nhiều"}""", false };
        yield return new object[] { "title vượt maxLength 120", $$"""{"title":"{{new string('x', 121)}}"}""", false };
        yield return new object[] { "align không trong enum", """{"align":"diagonal"}""", false };
        yield return new object[] { "image thiếu imageId (required)", """{"image":{"alt":"x"}}""", false };
        yield return new object[] { "cta.target hợp lệ (external)", """{"cta":{"target":{"kind":"external","url":"https://x.com"}}}""", true };
        yield return new object[] { "cta.target sai kind", """{"cta":{"target":{"kind":"unknown","url":"x"}}}""", false };
    }

    [Theory]
    [MemberData(nameof(GalleryCases))]
    public void Gallery_ValidatesLikeZod(string caseName, string json, bool expectedValid)
    {
        var schema = GetSchema("Gallery");
        var element = JsonDocument.Parse(json).RootElement;

        var result = schema.Evaluate(element);

        Assert.True(result.IsValid == expectedValid, $"case '{caseName}': mong đợi {expectedValid}, JsonSchema.Net trả {result.IsValid}");
    }

    public static IEnumerable<object[]> GalleryCases()
    {
        yield return new object[] { "valid 1 item", """{"items":[{"image":{"imageId":"a"}}]}""", true };
        yield return new object[] { "items rỗng vi phạm minItems:1", """{"items":[]}""", false };
        yield return new object[] { "item thiếu imageId", """{"items":[{"image":{}}]}""", false };
        yield return new object[] { "columns ngoài min/max", """{"columns":10}""", false };
    }

    private static JsonSchema GetSchema(string componentType)
    {
        var schemaNode = PropsSchemas.Value[componentType]
            ?? throw new InvalidOperationException($"Không tìm thấy schema cho '{componentType}' trong props-schemas.json");
        return JsonSchema.FromText(schemaNode.ToJsonString());
    }

    private static JsonObject LoadPropsSchemas()
    {
        var repoRoot = FindRepoRoot();
        var path = Path.Combine(repoRoot, "packages", "builder-components", "generated", "props-schemas.json");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Không tìm thấy '{path}'. Chạy `pnpm gen:registry` trong packages/builder-components trước khi chạy test này.",
                path);
        }

        var json = File.ReadAllText(path);
        return JsonNode.Parse(json)?.AsObject()
            ?? throw new InvalidOperationException("props-schemas.json rỗng hoặc không parse được.");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "pnpm-workspace.yaml")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Không tìm thấy repo root (pnpm-workspace.yaml).");
    }
}
