using System.Reflection;
using System.Text.Json;
using NetArchTest.Rules;
using Xunit;

namespace ArchitectureTests;

/// <summary>
/// Ranh giới module (Quyết định #1, sửa 2026-09-13): module là một FOLDER + NAMESPACE
/// `Vsite.{Layer}.{Module}`, không còn là .csproj riêng. Vì mất enforce ở tầng compile, test này là
/// lớp chặn DUY NHẤT — nó phải fail thật, đừng nới lỏng khi thấy đỏ.
///
/// Danh sách module + chiều phụ thuộc đọc từ `docs/architecture/dependency-map.json` (nguồn sự thật
/// duy nhất, Quyết định #17) — không hardcode lần thứ hai ở đây.
/// </summary>
public sealed class ModuleBoundaryTests
{
    private static readonly string[] Layers = ["Domain", "Application", "Infrastructure", "Api"];

    /// <summary>
    /// Namespace segment KHÔNG phải module — hạ tầng dùng chung, mọi module được phụ thuộc tự do.
    /// Thêm vào đây là quyết định có ý thức: một segment nằm ở đây nghĩa là nó không bao giờ bị
    /// kiểm ranh giới.
    /// </summary>
    private static readonly Dictionary<string, string[]> SharedSegments = new()
    {
        ["Domain"] = ["Common", "Abstractions", "Exceptions", "Authorization", "Pagination", "ReservedRoutes"],
        ["Application"] = ["Common"],
        ["Infrastructure"] = ["Persistence", "Configuration"],
        ["Api"] = ["Auth", "ExceptionHandling", "OpenApi", "Tenancy"],
    };

    private static readonly Dictionary<string, Assembly> Assemblies = new()
    {
        ["Domain"] = typeof(Vsite.Domain.Common.BaseEntity).Assembly,
        ["Application"] = typeof(Vsite.Application.Common.Interfaces.IAppDbContext).Assembly,
        ["Infrastructure"] = typeof(Vsite.Infrastructure.DependencyInjection).Assembly,
        ["Api"] = typeof(Program).Assembly,
    };

    [Fact]
    public void Module_Does_Not_Depend_On_Undeclared_Module()
    {
        var modules = LoadModules();
        var violations = new List<string>();

        foreach (var (module, allowed) in modules)
        {
            var forbidden = modules.Keys
                .Where(other => other != module && !allowed.Contains(other))
                .SelectMany(other => Layers.Select(layer => $"Vsite.{layer}.{other}"))
                .ToArray();

            if (forbidden.Length == 0)
            {
                continue;
            }

            foreach (var layer in Layers)
            {
                var result = Types.InAssembly(Assemblies[layer])
                    .That().ResideInNamespace($"Vsite.{layer}.{module}")
                    .Should().NotHaveDependencyOnAny(forbidden)
                    .GetResult();

                if (!result.IsSuccessful)
                {
                    var names = string.Join(", ", result.FailingTypeNames ?? []);
                    violations.Add($"module '{module}' (tầng {layer}) phụ thuộc module chưa khai trong dependency-map.json — tại: {names}");
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    [Fact]
    public void Every_Module_Namespace_Is_Declared_In_DependencyMap()
    {
        var modules = LoadModules();
        var violations = new List<string>();

        foreach (var layer in Layers)
        {
            var prefix = $"Vsite.{layer}.";

            var segments = Assemblies[layer].GetTypes()
                .Select(t => t.Namespace)
                .Where(ns => ns is not null && ns.StartsWith(prefix, StringComparison.Ordinal))
                .Select(ns => ns![prefix.Length..].Split('.')[0])
                .Distinct(StringComparer.Ordinal);

            foreach (var segment in segments)
            {
                if (modules.ContainsKey(segment) || SharedSegments[layer].Contains(segment))
                {
                    continue;
                }

                violations.Add(
                    $"namespace 'Vsite.{layer}.{segment}' không phải module đã khai trong " +
                    "docs/architecture/dependency-map.json, cũng không nằm trong " +
                    $"ModuleBoundaryTests.SharedSegments[\"{layer}\"]. Khai nó ở đúng một trong hai chỗ.");
            }
        }

        Assert.True(violations.Count == 0, string.Join("\n", violations.Distinct()));
    }

    private static Dictionary<string, string[]> LoadModules()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "dependency-map.json");
        Assert.True(File.Exists(path), $"Không tìm thấy dependency-map.json tại '{path}' — kiểm tra CopyToOutputDirectory trong ArchitectureTests.csproj.");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetProperty("modules").EnumerateObject().ToDictionary(
            m => m.Name,
            m => m.Value.GetProperty("dependsOn").EnumerateArray().Select(v => v.GetString()!).ToArray(),
            StringComparer.Ordinal);
    }
}
