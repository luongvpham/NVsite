using System.Xml.Linq;
using Xunit;

namespace ArchitectureTests;

/// <summary>
/// Module không được reference project của module khác (Quyết định #1).
/// Quét trực tiếp ProjectReference trong .csproj dưới backend/src/Modules/*, không cần assembly-level
/// NetArchTest — nên vẫn hoạt động (vacuously pass) kể cả khi backend/src/Modules/ rỗng, như giữa lúc
/// Sample bị xoá (docs/tasks/CLEANUP-SAMPLE.md) và Identity (Bước 3) chưa tồn tại.
/// </summary>
public sealed class ModuleBoundaryTests
{
    private static string ModulesRoot => Path.Combine(FindRepoRoot(), "backend", "src", "Modules");

    [Fact]
    public void No_Module_Project_References_Another_Modules_Project()
    {
        var violations = new List<string>();

        foreach (var moduleDir in Directory.GetDirectories(ModulesRoot))
        {
            var moduleName = Path.GetFileName(moduleDir);

            foreach (var csproj in Directory.GetFiles(moduleDir, "*.csproj", SearchOption.AllDirectories))
            {
                var doc = XDocument.Load(csproj);
                var references = doc.Descendants("ProjectReference")
                    .Select(e => e.Attribute("Include")?.Value)
                    .Where(v => !string.IsNullOrEmpty(v));

                foreach (var reference in references)
                {
                    var resolvedPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(csproj)!, reference!));

                    if (!resolvedPath.StartsWith(ModulesRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // reference ra ngoài Modules/ (vd: Shared) — hợp lệ.
                    }

                    var referencedModule = Path.GetRelativePath(ModulesRoot, resolvedPath).Split(Path.DirectorySeparatorChar)[0];

                    if (!string.Equals(referencedModule, moduleName, StringComparison.OrdinalIgnoreCase))
                    {
                        violations.Add($"{Path.GetFileName(csproj)} (module '{moduleName}') → {reference} (module '{referencedModule}')");
                    }
                }
            }
        }

        Assert.True(violations.Count == 0, "Cross-module reference: " + string.Join("; ", violations));
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
