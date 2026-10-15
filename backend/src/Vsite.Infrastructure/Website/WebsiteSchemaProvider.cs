using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Vsite.Application.Website.Interfaces;

namespace Vsite.Infrastructure.Website;

/// <summary>
/// Nạp 3 artifact JSON Schema do codegen sinh (#60, #97) bằng <c>JsonSchema.Net</c>: vỏ Tree, vỏ Tokens,
/// props từng component. Thiếu/hỏng file → exception ngay ở constructor (fail-fast, Program.cs resolve eager).
/// </summary>
public sealed class WebsiteSchemaProvider : IWebsiteSchemaProvider
{
    private static readonly EvaluationOptions ListOptions = new() { OutputFormat = OutputFormat.List };

    private readonly JsonSchema _treeEnvelope;
    private readonly JsonSchema _themeTokens;
    private readonly Dictionary<string, JsonSchema> _props;

    public WebsiteSchemaProvider(string treeEnvelopePath, string themeTokensPath, string propsSchemasPath)
    {
        _treeEnvelope = Load(treeEnvelopePath);
        _themeTokens = Load(themeTokensPath);

        var propsText = WebsiteConfigFile.ReadText(propsSchemasPath);
        JsonObject? root;
        try
        {
            root = JsonNode.Parse(propsText) as JsonObject;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"{Path.GetFileName(propsSchemasPath)} tại '{propsSchemasPath}' không phải JSON hợp lệ: {ex.Message}", ex);
        }

        if (root is null)
        {
            throw WebsiteConfigFile.BadShape(propsSchemasPath, "gốc phải là object { Type: schema }.");
        }

        _props = new Dictionary<string, JsonSchema>(StringComparer.Ordinal);
        foreach (var (type, schema) in root)
        {
            try
            {
                _props[type] = FromText(schema!.ToJsonString());
            }
            catch (Exception ex) when (ex is JsonException or JsonSchemaException)
            {
                throw new InvalidOperationException(
                    $"{Path.GetFileName(propsSchemasPath)} tại '{propsSchemasPath}': schema của '{type}' không hợp lệ: {ex.Message}", ex);
            }
        }

        if (_props.Count == 0)
        {
            throw WebsiteConfigFile.BadShape(propsSchemasPath, "không có schema component nào.");
        }
    }

    public IReadOnlyList<SchemaViolation> ValidateTreeEnvelope(string json) => Evaluate(_treeEnvelope, json);

    public IReadOnlyList<SchemaViolation> ValidateThemeTokens(string json) => Evaluate(_themeTokens, json);

    public bool HasPropsSchema(string componentType) => _props.ContainsKey(componentType);

    public IReadOnlyList<SchemaViolation> ValidateProps(string componentType, string propsJson) =>
        _props.TryGetValue(componentType, out var schema)
            ? Evaluate(schema, propsJson)
            : throw new KeyNotFoundException($"Không có props schema cho component '{componentType}'.");

    // Registry RIÊNG cho mỗi schema: schema có `$id` mà đăng ký vào registry toàn cục thì lần dựng thứ hai
    // (nhiều host trong cùng process — WebApplicationFactory, test) ném "Overwriting registered schemas".
    private static JsonSchema FromText(string text) =>
        JsonSchema.FromText(text, new BuildOptions { SchemaRegistry = new SchemaRegistry() });

    private static JsonSchema Load(string path)
    {
        var text = WebsiteConfigFile.ReadText(path);
        try
        {
            return FromText(text);
        }
        catch (Exception ex) when (ex is JsonException or JsonSchemaException)
        {
            throw new InvalidOperationException($"{Path.GetFileName(path)} tại '{path}' không phải JSON Schema hợp lệ: {ex.Message}", ex);
        }
    }

    private static List<SchemaViolation> Evaluate(JsonSchema schema, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var result = schema.Evaluate(doc.RootElement, ListOptions);
        var violations = new List<SchemaViolation>();
        if (result.IsValid)
        {
            return violations;
        }

        foreach (var detail in result.Details ?? [])
        {
            if (detail.Errors is null)
            {
                continue;
            }

            foreach (var error in detail.Errors)
            {
                violations.Add(new SchemaViolation(detail.InstanceLocation.ToString(), error.Value));
            }
        }

        if (violations.Count == 0)
        {
            violations.Add(new SchemaViolation("", "Không khớp schema."));
        }

        return violations;
    }
}
