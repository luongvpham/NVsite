using System.Text.Json.Nodes;
using Vsite.Application.Media.Interfaces;
using Vsite.Application.Website.Options;
using Vsite.Application.Website.Validation;
using Vsite.Application.Website.Versioning;
using Vsite.Domain.Exceptions;
using Vsite.Infrastructure.Website;

namespace Vsite.IntegrationTests.Website;

/// <summary>
/// Truy cập bộ fixture dùng chung FE+BE (S0-T4) ở packages/builder-components/tests/fixtures/tree — test KHÔNG chép
/// fixture đi chỗ khác — và dựng validator thật từ config/artifact thật ở repo root.
/// </summary>
internal static class TreeFixtures
{
    public static readonly string Root = FindRepoRoot();

    public static string Dir { get; } = Path.Combine(Root, "packages", "builder-components", "tests", "fixtures", "tree");

    public static string Config(string name) => Path.Combine(Root, "config", name);

    public static string Generated(string name) => Path.Combine(Root, "packages", "builder-components", "generated", name);

    public static JsonObject Load(string fileName) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(Dir, fileName)))!.AsObject();

    private static IEnumerable<string> AllFiles() =>
        Directory.EnumerateFiles(Dir, "*.json").Select(Path.GetFileName).OfType<string>().OrderBy(n => n, StringComparer.Ordinal);

    /// <summary>Fixture có <c>tree</c> (chạy toàn pipeline).</summary>
    public static IEnumerable<object[]> TreeCases() =>
        AllFiles().Where(n => Load(n).ContainsKey("tree")).Select(n => new object[] { n });

    /// <summary>Fixture cặp stored/incoming (chạy SystemPageGuard).</summary>
    public static IEnumerable<object[]> PairCases() =>
        AllFiles().Where(n => Load(n).ContainsKey("stored")).Select(n => new object[] { n });

    public static PageTreeValidator CreateValidator(
        string? rulesPath = null, PageTreeLimits? limits = null, DocumentVersioning? versioning = null)
    {
        var rules = new TreeRulesCatalog(rulesPath ?? Generated("tree-rules.json"));
        var schemas = new WebsiteSchemaProvider(
            Generated("tree-envelope.schema.json"), Generated("theme-tokens.schema.json"), Generated("props-schemas.json"));
        return new PageTreeValidator(
            rules,
            new BindingSourceCatalog(Config("binding-sources.json")),
            schemas,
            new RichTextSanitizer(new SanitizeProfileCatalog(Config("sanitize-profiles.json"))),
            limits ?? new PageTreeLimits(),
            versioning ?? new DocumentVersioning([]));
    }

    public static RichTextSanitizer CreateSanitizer() => new(new SanitizeProfileCatalog(Config("sanitize-profiles.json")));

    /// <summary>tree-rules.json + <c>rulesOverride.types</c> của fixture, ghi ra file tạm (TreeRulesCatalog đọc từ đường dẫn).</summary>
    public static string MergeRulesOverride(JsonObject overrideTypes)
    {
        var merged = JsonNode.Parse(File.ReadAllText(Generated("tree-rules.json")))!.AsObject();
        var types = merged["types"]!.AsObject();
        foreach (var (name, rule) in overrideTypes)
        {
            types[name] = rule!.DeepClone();
        }

        var path = Path.Combine(Path.GetTempPath(), $"tree-rules-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, merged.ToJsonString());
        return path;
    }

    /// <summary>Id ảnh trong fixture là chuỗi ("media_clone_1"); validator thật nhận Guid → ánh xạ xác định.</summary>
    public static Guid ToGuid(string imageId) =>
        new(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(imageId)));

    /// <summary>Fake <see cref="IMediaReferenceValidator"/> dựng từ bảng <c>context.media</c> của fixture (README, mục Ảnh).</summary>
    public sealed class FixtureMediaValidator : IMediaReferenceValidator
    {
        private readonly HashSet<Guid> _valid = [];

        public FixtureMediaValidator(JsonObject? media)
        {
            foreach (var (id, entry) in media ?? [])
            {
                var kind = entry?["kind"]?.GetValue<string>();
                var shop = entry?["shop"]?.GetValue<string>() ?? "own";
                var deleted = entry?["deleted"]?.GetValue<bool>() ?? false;
                if ((kind is "Clone" or "Direct") && shop == "own" && !deleted)
                {
                    _valid.Add(ToGuid(id));
                }
            }
        }

        public Task EnsureValidAsync(
            IReadOnlyCollection<Guid> treeImageIds, IReadOnlyCollection<Guid> businessImageIds, CancellationToken ct)
        {
            if (treeImageIds.Any(id => !_valid.Contains(id)))
            {
                throw new UnprocessableException("MEDIA_INVALID_IMAGE_REFERENCE", "Ảnh không hợp lệ.");
            }

            return Task.CompletedTask;
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

