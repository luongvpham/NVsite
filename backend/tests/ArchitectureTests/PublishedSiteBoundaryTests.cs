using System.Text.RegularExpressions;
using Xunit;

namespace ArchitectureTests;

/// <summary>
/// WEBSITE-004 T7 (R3, 05 §7) — đọc công khai chỉ đi qua bản publish BẤT BIẾN. Mã nguồn
/// <c>Vsite.Application/Website/Queries/GetPublishedSite/*.cs</c> không được chạm <c>PageDrafts</c>, <c>Pages</c>, <c>Themes</c>
/// (đọc draft/theme sống ⇒ vỡ tính bất biến) và không <c>IgnoreQueryFilters</c> (tenant isolation, #21).
/// Quét văn bản SAU khi bỏ comment (comment được phép NÓI về chúng). Test "không mù": <see cref="Scanner_catches_planted_forbidden_tokens"/>.
/// </summary>
public sealed class PublishedSiteBoundaryTests
{
    private static readonly string[] Forbidden = ["PageDrafts", "Pages", "Themes", "IgnoreQueryFilters"];

    private static readonly Regex Comments = new(@"//[^\n]*|/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);

    private static IReadOnlyList<string> Violations(string source) =>
        Forbidden.Where(token => Regex.IsMatch(Comments.Replace(source, string.Empty), $@"\b{token}\b")).ToList();

    private static string HandlerDirectory() =>
        Path.Combine(FindRepoRoot(), "backend", "src", "Vsite.Application", "Website", "Queries", "GetPublishedSite");

    [Fact]
    public void GetPublishedSite_sources_do_not_touch_drafts_pages_themes_or_IgnoreQueryFilters()
    {
        var files = Directory.EnumerateFiles(HandlerDirectory(), "*.cs", SearchOption.AllDirectories).ToList();
        Assert.True(files.Count >= 2, "Không tìm thấy nguồn GetPublishedSite — thư mục bị đổi tên? Test này sẽ mù nếu để yên.");

        var violations = files
            .SelectMany(f => Violations(File.ReadAllText(f)).Select(v => $"{Path.GetFileName(f)}: {v}"))
            .ToList();

        Assert.True(
            violations.Count == 0,
            "GetPublishedSite chỉ được đọc SitePublications + Websites (con trỏ) + Shops: " + string.Join("; ", violations));
    }

    [Theory]
    [InlineData("var d = await db.PageDrafts.ToListAsync();", "PageDrafts")]
    [InlineData("var p = db.Pages.Where(x => x.IsEnabled);", "Pages")]
    [InlineData("var t = db\n    .Themes\n    .FirstAsync();", "Themes")]
    [InlineData("db.SitePublications.IgnoreQueryFilters()", "IgnoreQueryFilters")]
    [InlineData("db.Websites\n  .IgnoreQueryFilters ()", "IgnoreQueryFilters")]
    public void Scanner_catches_planted_forbidden_tokens(string source, string expected)
    {
        Assert.Contains(expected, Violations(source));
    }

    [Fact]
    public void Scanner_ignores_comments_and_allowed_sets()
    {
        const string source = """
            // không được chạm db.PageDrafts, db.Pages, db.Themes
            /* IgnoreQueryFilters() cấm */
            var p = await db.SitePublications.Where(x => x.ShopId == id).FirstOrDefaultAsync();
            var w = await db.Websites.Select(x => x.Status).FirstOrDefaultAsync();
            """;

        Assert.Empty(Violations(source));
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
