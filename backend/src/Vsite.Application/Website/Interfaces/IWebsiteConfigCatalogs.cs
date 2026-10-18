namespace Vsite.Application.Website.Interfaces;

// Các catalog read-only của module Website (T1, WEBSITE-001, #97). Nguồn: config/*.json và artifact
// codegen packages/builder-components/generated/* — FE và BE đọc CÙNG file, BE không viết danh sách
// thứ hai (#17, #24). Implement ở Vsite.Infrastructure/Website; đọc một lần lúc startup (fail-fast).

/// <summary>config/locales.json — mã locale (kể cả locale chưa bật, #98).</summary>
public interface ILocaleCatalog
{
    IReadOnlyList<string> Codes { get; }

    /// <summary>So không phân biệt hoa/thường (slug <c>EN</c> vẫn trùng <c>en</c>).</summary>
    bool Contains(string code);
}

/// <summary>config/theme-tokens.json — whitelist token theme + <c>defaults</c> (G2).</summary>
public interface IThemeTokenCatalog
{
    IReadOnlyList<string> ColorKeys { get; }
    string ColorPattern { get; }
    IReadOnlyList<string> Fonts { get; }
    IReadOnlyList<string> Scales { get; }
    IReadOnlyList<string> Radii { get; }
    IReadOnlyList<string> Shadows { get; }
    IReadOnlyList<string> SectionGaps { get; }

    /// <summary>Tokens mặc định của website mới, dạng JSON (đã có <c>schemaVersion</c>).</summary>
    string DefaultsJson { get; }
}

/// <param name="Routes">Đoạn đầu của đường dẫn; <c>Routes[0]</c> là route chính (slug khi seed). NotFound: rỗng.</param>
public sealed record SystemPageDefinition(IReadOnlyList<string> Routes, int Phase, bool SeedOnCreate);

/// <summary>config/system-pages.json — mọi <c>SystemType</c>, kể cả loại chưa seed (G3).</summary>
public interface ISystemPageCatalog
{
    /// <summary>Khoá = tên <c>SystemType</c>.</summary>
    IReadOnlyDictionary<string, SystemPageDefinition> Pages { get; }

    /// <summary>Hợp mọi route của mọi System page — slug trang không được trùng (R9).</summary>
    IReadOnlySet<string> AllRoutes { get; }
}

public sealed record SanitizeProfile(IReadOnlyList<string> Tags, IReadOnlyDictionary<string, IReadOnlyList<string>> Attrs);

/// <summary>Khoá <c>dangerous</c> của sanitize-profiles.json — KHÔNG phải một profile.</summary>
public sealed record DangerousSanitizeRules(
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> AttrPrefixes,
    IReadOnlyList<string> Schemes);

/// <summary>config/sanitize-profiles.json.</summary>
public interface ISanitizeProfileCatalog
{
    IReadOnlyDictionary<string, SanitizeProfile> Profiles { get; }
    DangerousSanitizeRules Dangerous { get; }
}

/// <summary>config/binding-sources.json — không có <c>Review</c> (Site không bao giờ có Review).</summary>
public interface IBindingSourceCatalog
{
    IReadOnlyList<string> Sources { get; }
    bool Contains(string source);
}

/// <summary>Luật của một prop trong tree-rules.json. <c>Kind</c>: text/richText/image/binding/list/group/...</summary>
public sealed record TreePropRule(
    string Kind,
    string? Profile,
    IReadOnlyList<string>? Sources,
    string? Preset,
    IReadOnlyDictionary<string, TreePropRule>? ItemProps);

/// <param name="AllowedChildTypes">null = không giới hạn (khi <paramref name="AcceptsChildren"/>).</param>
public sealed record TreeTypeRule(
    bool AcceptsChildren,
    IReadOnlyList<string>? AllowedChildTypes,
    IReadOnlyList<string> AllowedInPageKinds,
    int? MaxPerPage,
    IReadOnlyList<string> EditableInSystemPageProps,
    IReadOnlyList<string> Variants,
    IReadOnlyDictionary<string, TreePropRule> Props);

/// <summary>generated/tree-rules.json — luật op + bản đồ prop sinh từ manifest (#60).</summary>
public interface ITreeRulesCatalog
{
    int SchemaVersion { get; }
    IReadOnlyDictionary<string, TreeTypeRule> Types { get; }
}

public sealed record SchemaViolation(string Path, string Message);

/// <summary>JSON Schema sinh từ Zod/manifest (#60, #97): vỏ Tree, vỏ Tokens, props từng component.</summary>
public interface IWebsiteSchemaProvider
{
    IReadOnlyList<SchemaViolation> ValidateTreeEnvelope(string json);
    IReadOnlyList<SchemaViolation> ValidateThemeTokens(string json);

    bool HasPropsSchema(string componentType);
    IReadOnlyList<SchemaViolation> ValidateProps(string componentType, string propsJson);
}
