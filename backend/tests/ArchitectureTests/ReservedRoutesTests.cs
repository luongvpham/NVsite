using Vsite.Domain.ReservedRoutes;
using Vsite.Infrastructure.Configuration;
using Xunit;

namespace ArchitectureTests;

/// <summary>
/// config/reserved-routes.json là nguồn sự thật duy nhất (Quyết định #24) — BE phải đọc được
/// đúng file đó (không phải bản copy tay), và phải có "shop" (#24) + "admin" (#25).
/// </summary>
public sealed class ReservedRoutesTests
{
    [Fact]
    public void ReservedRoutesProvider_Reads_RepoRootConfigFile_And_Contains_RequiredEntries()
    {
        var repoRoot = FindRepoRoot();
        var filePath = Path.Combine(repoRoot, "config", "reserved-routes.json");

        var provider = new ReservedRoutesProvider(filePath);

        Assert.Contains("shop", provider.Routes.ReservedPaths);
        Assert.Contains("admin", provider.Routes.ReservedSubdomains);
        // "media" (Quyết định #24, T3 MEDIA-001) — route /media/{key} phục vụ ảnh qua object storage.
        Assert.Contains("media", provider.Routes.ReservedPaths);
    }

    [Fact]
    public void ReservedShopSitePaths_missing_key_fails_fast()
    {
        // Thiếu key = fail-open (slug `media`/`api` lọt) → provider phải ném, app không khởi động.
        var path = WriteTemp("""{"reservedPaths":["shop"],"reservedSubdomains":["admin"]}""");
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new ReservedRoutesProvider(path));

            Assert.Contains("reservedShopSitePaths", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReservedShopSitePaths_reads_key_when_present()
    {
        var path = WriteTemp("""{"reservedPaths":[],"reservedSubdomains":[],"reservedShopSitePaths":["media","api"]}""");
        try
        {
            var routes = new ReservedRoutesProvider(path).Routes;

            Assert.Equal(["media", "api"], routes.ReservedShopSitePaths);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReservedShopSitePaths_in_repo_config_contains_media_and_api()
    {
        var provider = new ReservedRoutesProvider(Path.Combine(FindRepoRoot(), "config", "reserved-routes.json"));

        Assert.Contains("media", provider.Routes.ReservedShopSitePaths);
        Assert.Contains("api", provider.Routes.ReservedShopSitePaths);
    }

    private static string WriteTemp(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-rr.json");
        File.WriteAllText(path, json);
        return path;
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
