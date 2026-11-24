using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vsite.Domain.Authorization;

namespace Vsite.IntegrationTests;

/// <summary>
/// JOBS-001 T3 / #114 mục 6 — mọi route <c>/api/platform/*</c> mang policy <c>PlatformAdmin</c> (duyệt
/// <see cref="EndpointDataSource"/> thật), và luồng 1-2 (<c>Vsite.Api/Tenancy/**</c>) không có nhánh
/// bypass PlatformAdmin. Không cần Docker.
/// </summary>
public sealed class PlatformRoutePolicyTests
{
    [Fact]
    public void Every_api_platform_route_carries_PlatformAdmin_policy()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Identity"] = "Host=localhost;Port=1;Database=fake;Username=fake;Password=fake",
                    ["ConnectionStrings:Redis"] = "localhost:1",
                    ["Jwt:SigningKey"] = "test-signing-key-not-for-production-use-32-chars-min",
                    ["Jwt:Issuer"] = "vsite-test",
                    ["Jwt:AccessTokenLifetimeMinutes"] = "15",
                    ["Auth:ApiBaseUrl"] = "http://localhost",
                });
            });
        });

        var platformRoutes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => (e.RoutePattern.RawText ?? string.Empty).TrimStart('/')
                .StartsWith("api/platform/", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(platformRoutes); // group có được map không?

        var missing = platformRoutes
            .Where(e => !e.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Any(a => string.Equals(a.Policy, AuthPolicies.PlatformAdmin, StringComparison.Ordinal)))
            .Select(e => e.RoutePattern.RawText)
            .ToList();
        Assert.True(missing.Count == 0, "Route /api/platform/* thiếu policy PlatformAdmin: " + string.Join(", ", missing));

        // Không route nào được mở cửa bằng AllowAnonymous.
        Assert.All(platformRoutes, e => Assert.Null(e.Metadata.GetMetadata<IAllowAnonymous>()));
    }

    [Fact]
    public void Tenancy_sources_never_mention_PlatformAdmin()
    {
        var tenancyDir = FindTenancyDir();
        var offenders = Directory.EnumerateFiles(tenancyDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains("PlatformAdmin", StringComparison.Ordinal))
            .ToList();

        Assert.True(offenders.Count == 0, "Luồng 1-2 không được có nhánh PlatformAdmin (#114 mục 6): " + string.Join(", ", offenders));
    }

    private static string FindTenancyDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = System.IO.Path.Combine(dir.FullName, "backend", "src", "Vsite.Api", "Tenancy");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Không tìm thấy backend/src/Vsite.Api/Tenancy từ " + AppContext.BaseDirectory);
    }
}
