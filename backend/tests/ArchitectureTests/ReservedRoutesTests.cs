using Shared.ReservedRoutes;
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
