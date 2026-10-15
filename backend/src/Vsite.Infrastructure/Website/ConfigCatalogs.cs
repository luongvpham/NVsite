using System.Text.Json;
using System.Text.RegularExpressions;
using Vsite.Application.Website.Interfaces;

namespace Vsite.Infrastructure.Website;

/// <summary>config/locales.json — mảng mã locale.</summary>
public sealed class LocaleCatalog : ILocaleCatalog
{
    private readonly HashSet<string> _set;

    public LocaleCatalog(string filePath)
    {
        using var doc = WebsiteConfigFile.Parse(filePath);
        Codes = WebsiteConfigFile.StringArray(doc.RootElement, filePath, "locales");
        _set = new HashSet<string>(Codes, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> Codes { get; }

    public bool Contains(string code) => _set.Contains(code);
}

/// <summary>config/theme-tokens.json.</summary>
public sealed class ThemeTokenCatalog : IThemeTokenCatalog
{
    public ThemeTokenCatalog(string filePath)
    {
        using var doc = WebsiteConfigFile.Parse(filePath);
        var root = doc.RootElement;

        ColorKeys = WebsiteConfigFile.StringArray(WebsiteConfigFile.Required(root, filePath, "colorKeys", JsonValueKind.Array), filePath, "colorKeys");
        ColorPattern = WebsiteConfigFile.Required(root, filePath, "colorPattern", JsonValueKind.String).GetString()!;
        try
        {
            _ = new Regex(ColorPattern);
        }
        catch (ArgumentException ex)
        {
            throw WebsiteConfigFile.BadShape(filePath, $"'colorPattern' không biên dịch được: {ex.Message}");
        }

        Fonts = WebsiteConfigFile.StringArray(WebsiteConfigFile.Required(root, filePath, "fonts", JsonValueKind.Array), filePath, "fonts");
        Scales = WebsiteConfigFile.StringArray(WebsiteConfigFile.Required(root, filePath, "scale", JsonValueKind.Array), filePath, "scale");
        Radii = WebsiteConfigFile.StringArray(WebsiteConfigFile.Required(root, filePath, "radius", JsonValueKind.Array), filePath, "radius");
        Shadows = WebsiteConfigFile.StringArray(WebsiteConfigFile.Required(root, filePath, "shadow", JsonValueKind.Array), filePath, "shadow");
        SectionGaps = WebsiteConfigFile.StringArray(WebsiteConfigFile.Required(root, filePath, "sectionGap", JsonValueKind.Array), filePath, "sectionGap");

        var defaults = WebsiteConfigFile.Required(root, filePath, "defaults", JsonValueKind.Object);
        WebsiteConfigFile.Required(defaults, filePath, "schemaVersion", JsonValueKind.Number);
        DefaultsJson = defaults.GetRawText();
    }

    public IReadOnlyList<string> ColorKeys { get; }
    public string ColorPattern { get; }
    public IReadOnlyList<string> Fonts { get; }
    public IReadOnlyList<string> Scales { get; }
    public IReadOnlyList<string> Radii { get; }
    public IReadOnlyList<string> Shadows { get; }
    public IReadOnlyList<string> SectionGaps { get; }
    public string DefaultsJson { get; }
}

/// <summary>config/system-pages.json.</summary>
public sealed class SystemPageCatalog : ISystemPageCatalog
{
    public SystemPageCatalog(string filePath)
    {
        using var doc = WebsiteConfigFile.Parse(filePath);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw WebsiteConfigFile.BadShape(filePath, "gốc phải là object { SystemType: {...} }.");
        }

        var pages = new Dictionary<string, SystemPageDefinition>(StringComparer.Ordinal);
        var routes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            var routeList = WebsiteConfigFile.StringArray(
                WebsiteConfigFile.Required(property.Value, filePath, "routes", JsonValueKind.Array),
                filePath, $"{property.Name}.routes", allowEmpty: true);
            var phase = WebsiteConfigFile.Required(property.Value, filePath, "phase", JsonValueKind.Number).GetInt32();
            if (!property.Value.TryGetProperty("seedOnCreate", out var seed) || seed.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw WebsiteConfigFile.BadShape(filePath, $"thiếu hoặc sai kiểu '{property.Name}.seedOnCreate' (bool).");
            }

            pages[property.Name] = new SystemPageDefinition(routeList, phase, seed.GetBoolean());
            routes.UnionWith(routeList);
        }

        if (pages.Count == 0)
        {
            throw WebsiteConfigFile.BadShape(filePath, "không có System page nào.");
        }

        Pages = pages;
        AllRoutes = routes;
    }

    public IReadOnlyDictionary<string, SystemPageDefinition> Pages { get; }
    public IReadOnlySet<string> AllRoutes { get; }
}

/// <summary>config/sanitize-profiles.json — khoá <c>dangerous</c> tách khỏi các profile.</summary>
public sealed class SanitizeProfileCatalog : ISanitizeProfileCatalog
{
    private const string DangerousKey = "dangerous";

    public SanitizeProfileCatalog(string filePath)
    {
        using var doc = WebsiteConfigFile.Parse(filePath);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw WebsiteConfigFile.BadShape(filePath, "gốc phải là object.");
        }

        var dangerous = WebsiteConfigFile.Required(doc.RootElement, filePath, DangerousKey, JsonValueKind.Object);
        Dangerous = new DangerousSanitizeRules(
            WebsiteConfigFile.StringArray(WebsiteConfigFile.Required(dangerous, filePath, "tags", JsonValueKind.Array), filePath, "dangerous.tags"),
            WebsiteConfigFile.StringArray(WebsiteConfigFile.Required(dangerous, filePath, "attrPrefixes", JsonValueKind.Array), filePath, "dangerous.attrPrefixes"),
            WebsiteConfigFile.StringArray(WebsiteConfigFile.Required(dangerous, filePath, "schemes", JsonValueKind.Array), filePath, "dangerous.schemes"));

        var profiles = new Dictionary<string, SanitizeProfile>(StringComparer.Ordinal);
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            if (property.Name == DangerousKey)
            {
                continue;
            }

            var tags = WebsiteConfigFile.StringArray(
                WebsiteConfigFile.Required(property.Value, filePath, "tags", JsonValueKind.Array), filePath, $"{property.Name}.tags");
            var attrs = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var attr in WebsiteConfigFile.Required(property.Value, filePath, "attrs", JsonValueKind.Object).EnumerateObject())
            {
                attrs[attr.Name] = WebsiteConfigFile.StringArray(attr.Value, filePath, $"{property.Name}.attrs.{attr.Name}");
            }

            profiles[property.Name] = new SanitizeProfile(tags, attrs);
        }

        if (profiles.Count == 0)
        {
            throw WebsiteConfigFile.BadShape(filePath, "không có profile nào ngoài 'dangerous'.");
        }

        Profiles = profiles;
    }

    public IReadOnlyDictionary<string, SanitizeProfile> Profiles { get; }
    public DangerousSanitizeRules Dangerous { get; }
}

/// <summary>config/binding-sources.json.</summary>
public sealed class BindingSourceCatalog : IBindingSourceCatalog
{
    private readonly HashSet<string> _set;

    public BindingSourceCatalog(string filePath)
    {
        using var doc = WebsiteConfigFile.Parse(filePath);
        Sources = WebsiteConfigFile.StringArray(doc.RootElement, filePath, "binding-sources");
        _set = new HashSet<string>(Sources, StringComparer.Ordinal);
    }

    public IReadOnlyList<string> Sources { get; }

    public bool Contains(string source) => _set.Contains(source);
}

/// <summary>generated/tree-rules.json (artifact codegen).</summary>
public sealed class TreeRulesCatalog : ITreeRulesCatalog
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public TreeRulesCatalog(string filePath)
    {
        var text = WebsiteConfigFile.ReadText(filePath);
        TreeRulesFile? file;
        try
        {
            file = JsonSerializer.Deserialize<TreeRulesFile>(text, Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{Path.GetFileName(filePath)} tại '{filePath}' sai shape: {ex.Message}", ex);
        }

        if (file is null || file.SchemaVersion < 1 || file.Types is null || file.Types.Count == 0)
        {
            throw WebsiteConfigFile.BadShape(filePath, "cần 'schemaVersion' >= 1 và 'types' không rỗng.");
        }

        SchemaVersion = file.SchemaVersion;
        var types = new Dictionary<string, TreeTypeRule>(StringComparer.Ordinal);
        foreach (var (name, raw) in file.Types)
        {
            if (raw.AllowedInPageKinds is null || raw.EditableInSystemPageProps is null || raw.Variants is null || raw.Props is null)
            {
                throw WebsiteConfigFile.BadShape(filePath, $"type '{name}' thiếu allowedInPageKinds/editableInSystemPageProps/variants/props.");
            }

            types[name] = new TreeTypeRule(
                raw.AcceptsChildren, raw.AllowedChildTypes, raw.AllowedInPageKinds, raw.MaxPerPage,
                raw.EditableInSystemPageProps, raw.Variants, ToRules(raw.Props, filePath, name));
        }

        Types = types;
    }

    public int SchemaVersion { get; }
    public IReadOnlyDictionary<string, TreeTypeRule> Types { get; }

    private static Dictionary<string, TreePropRule> ToRules(Dictionary<string, RawProp> raw, string filePath, string where)
    {
        var rules = new Dictionary<string, TreePropRule>(StringComparer.Ordinal);
        foreach (var (name, prop) in raw)
        {
            if (string.IsNullOrEmpty(prop.Kind))
            {
                throw WebsiteConfigFile.BadShape(filePath, $"prop '{where}.{name}' thiếu 'kind'.");
            }

            rules[name] = new TreePropRule(
                prop.Kind, prop.Profile, prop.Sources, prop.Preset,
                prop.ItemProps is null ? null : ToRules(prop.ItemProps, filePath, $"{where}.{name}"));
        }

        return rules;
    }

    private sealed record TreeRulesFile(int SchemaVersion, Dictionary<string, RawType>? Types);

    private sealed record RawType(
        bool AcceptsChildren,
        List<string>? AllowedChildTypes,
        List<string>? AllowedInPageKinds,
        int? MaxPerPage,
        List<string>? EditableInSystemPageProps,
        List<string>? Variants,
        Dictionary<string, RawProp>? Props);

    private sealed record RawProp(
        string? Kind,
        string? Profile,
        List<string>? Sources,
        string? Preset,
        Dictionary<string, RawProp>? ItemProps);
}
