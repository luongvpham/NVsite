using System.Text.Json;
using Vsite.Infrastructure.Website;

namespace Vsite.IntegrationTests.Website;

/// <summary>
/// T1, WEBSITE-001 (#97) — các catalog đọc config/ + artifact codegen một lần lúc startup. Không cần Docker.
/// Đọc ĐÚNG file ở repo root (không phải bản copy) — cùng nguồn với FE.
/// </summary>
public sealed class WebsiteConfigCatalogTests
{
    private static readonly string Root = FindRepoRoot();
    private static string Config(string name) => Path.Combine(Root, "config", name);
    private static string Generated(string name) => Path.Combine(Root, "packages", "builder-components", "generated", name);

    [Fact]
    public void Locales_contains_vi_and_en_case_insensitively()
    {
        var catalog = new LocaleCatalog(Config("locales.json"));

        Assert.Contains("vi", catalog.Codes);
        Assert.True(catalog.Contains("EN"));
        Assert.False(catalog.Contains("fr"));
    }

    [Fact]
    public void ThemeTokens_exposes_whitelists_and_defaults()
    {
        var catalog = new ThemeTokenCatalog(Config("theme-tokens.json"));

        Assert.Contains("primary", catalog.ColorKeys);
        Assert.Contains("Inter", catalog.Fonts);
        Assert.Equal("^#[0-9a-fA-F]{6}$", catalog.ColorPattern);
        Assert.Contains("comfortable", catalog.Scales);
        Assert.Equal(1, JsonDocument.Parse(catalog.DefaultsJson).RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public void SystemPages_covers_every_system_type_and_lists_all_routes()
    {
        var catalog = new SystemPageCatalog(Config("system-pages.json"));

        foreach (var type in new[] { "Contact", "NotFound", "ProductListing", "ProductDetail", "ProductSearch", "Booking" })
        {
            Assert.True(catalog.Pages.ContainsKey(type), $"thiếu {type}");
        }

        Assert.Empty(catalog.Pages["NotFound"].Routes);
        Assert.True(catalog.Pages["Contact"].SeedOnCreate);
        Assert.Equal("lien-he", catalog.Pages["Contact"].Routes[0]);
        Assert.Contains("danh-muc", catalog.AllRoutes);
        Assert.Contains("dat-lich", catalog.AllRoutes);
    }

    [Fact]
    public void SanitizeProfiles_loads_profiles_and_dangerous_block_is_not_a_profile()
    {
        var catalog = new SanitizeProfileCatalog(Config("sanitize-profiles.json"));

        Assert.True(catalog.Profiles.ContainsKey("basic"));
        Assert.True(catalog.Profiles.ContainsKey("inline"));
        Assert.False(catalog.Profiles.ContainsKey("dangerous"));
        Assert.Contains("script", catalog.Dangerous.Tags);
        Assert.Contains("on", catalog.Dangerous.AttrPrefixes);
        Assert.Contains("javascript", catalog.Dangerous.Schemes);
        Assert.Contains("href", catalog.Profiles["basic"].Attrs["a"]);
    }

    [Fact]
    public void BindingSources_does_not_contain_Review()
    {
        var catalog = new BindingSourceCatalog(Config("binding-sources.json"));

        Assert.DoesNotContain("Review", catalog.Sources);
        Assert.True(catalog.Contains("Service"));
        Assert.False(catalog.Contains("Review"));
    }

    [Fact]
    public void TreeRules_reads_types_and_prop_kinds()
    {
        var catalog = new TreeRulesCatalog(Generated("tree-rules.json"));

        Assert.Equal(1, catalog.SchemaVersion);
        var richText = catalog.Types["RichText"];
        Assert.Contains("System", richText.AllowedInPageKinds);
        Assert.Contains("content", richText.EditableInSystemPageProps);
        Assert.Equal("richText", richText.Props["content"].Kind);
        Assert.Equal("basic", richText.Props["content"].Profile);
        Assert.Equal(["Service", "ServiceGroup"], catalog.Types["ServiceGrid"].Props["source"].Sources);
        Assert.Equal("image", catalog.Types["Gallery"].Props["items"].ItemProps!["image"].Kind);
        Assert.Equal(1, catalog.Types["Hero"].MaxPerPage);
    }

    [Fact]
    public void Schemas_validate_envelope_theme_and_props()
    {
        var schemas = new WebsiteSchemaProvider(
            Generated("tree-envelope.schema.json"), Generated("theme-tokens.schema.json"), Generated("props-schemas.json"));

        Assert.Empty(schemas.ValidateTreeEnvelope("""{"schemaVersion":1,"id":"root","type":"Page","children":[]}"""));
        Assert.NotEmpty(schemas.ValidateTreeEnvelope("""{"schemaVersion":"1","id":"root","type":"Page","children":[]}"""));
        Assert.Empty(schemas.ValidateThemeTokens(JsonSerializer.Serialize(
            JsonDocument.Parse(File.ReadAllText(Config("theme-tokens.json"))).RootElement.GetProperty("defaults"))));
        Assert.NotEmpty(schemas.ValidateThemeTokens("""{"schemaVersion":1,"colors":{"primary":"red"}}"""));
        Assert.True(schemas.HasPropsSchema("Hero"));
        Assert.Empty(schemas.ValidateProps("Hero", "{}"));
        Assert.NotEmpty(schemas.ValidateProps("Hero", """{"overlayOpacity":"nhiều"}"""));
    }

    [Theory]
    [InlineData(typeof(LocaleCatalog), "locales.json")]
    [InlineData(typeof(ThemeTokenCatalog), "theme-tokens.json")]
    [InlineData(typeof(SystemPageCatalog), "system-pages.json")]
    [InlineData(typeof(SanitizeProfileCatalog), "sanitize-profiles.json")]
    [InlineData(typeof(BindingSourceCatalog), "binding-sources.json")]
    [InlineData(typeof(TreeRulesCatalog), "tree-rules.json")]
    public void Missing_file_throws_naming_the_file(Type catalogType, string fileName)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), fileName);

        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => Activator.CreateInstance(catalogType, path));

        var inner = Assert.IsType<FileNotFoundException>(ex.InnerException);
        Assert.Contains(fileName, inner.Message);
    }

    [Fact]
    public void Missing_schema_artifact_throws_with_gen_registry_hint()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "tree-envelope.schema.json");

        var ex = Assert.Throws<FileNotFoundException>(() => new WebsiteSchemaProvider(
            missing, Generated("theme-tokens.schema.json"), Generated("props-schemas.json")));

        Assert.Contains("tree-envelope.schema.json", ex.Message);
        Assert.Contains("gen:registry", ex.Message);
    }

    [Fact]
    public void Malformed_shape_throws_invalid_operation()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-locales.json");
        File.WriteAllText(path, """{"not":"an array"}""");
        try
        {
            Assert.Throws<InvalidOperationException>(() => new LocaleCatalog(path));
        }
        finally
        {
            File.Delete(path);
        }
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
