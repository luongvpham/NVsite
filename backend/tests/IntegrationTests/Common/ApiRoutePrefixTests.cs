using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vsite.Application.Common;

namespace Vsite.IntegrationTests;

/// <summary>
/// REFACTOR-API-001 — mọi endpoint API nằm dưới <see cref="ApiRoutes.Prefix"/>. Endpoint mới quên
/// map vào group <c>/api</c> sẽ đè lên namespace URL của slug shop (`vsite.vn/{slug}`) và route SPA
/// của Portal — test này đỏ ngay. Duyệt endpoint đã map thật qua <see cref="EndpointDataSource"/>.
///
/// `/media/*` không phải endpoint (branch middleware ở `Program.cs`) nên không xuất hiện ở đây;
/// `/openapi/{documentName}.json` là ngoại lệ có chủ đích (chỉ để xem khi chạy local).
/// Không cần Docker — chỉ build host, không gửi request.
/// </summary>
public sealed class ApiRoutePrefixTests
{
    private static readonly string[] AllowedOutsideApi = ["openapi/"];

    [Fact]
    public void Every_endpoint_is_mounted_under_api_prefix()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // Giá trị giả — không request nào chạm DB/Redis (cùng khuôn ShopScopedRouteFilterTests).
                    ["ConnectionStrings:Identity"] = "Host=localhost;Port=1;Database=fake;Username=fake;Password=fake",
                    ["ConnectionStrings:Redis"] = "localhost:1",
                    ["Jwt:SigningKey"] = "test-signing-key-not-for-production-use-32-chars-min",
                    ["Jwt:Issuer"] = "vsite-test",
                    ["Jwt:AccessTokenLifetimeMinutes"] = "15",
                    ["Auth:ApiBaseUrl"] = "http://localhost",
                });
            });
        });

        var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => (e.RoutePattern.RawText ?? string.Empty).TrimStart('/'))
            .ToList();

        var prefix = ApiRoutes.Prefix.TrimStart('/') + "/";
        Assert.True(routes.Count(r => r.StartsWith(prefix, StringComparison.Ordinal)) >= 20, "Ít endpoint /api bất thường — group /api có được map không?");

        // operationId tường minh (`.WithName`) cho MỌI endpoint API — tên hàm/hook Orval sinh ra lấy
        // từ đây, không từ path, nên đổi path không làm đổi tên ở FE (REFACTOR-API-001).
        var apiEndpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => (e.RoutePattern.RawText ?? string.Empty).TrimStart('/').StartsWith(prefix, StringComparison.Ordinal))
            .ToList();
        var unnamed = apiEndpoints
            .Where(e => e.Metadata.GetMetadata<IEndpointNameMetadata>() is null)
            .Select(e => e.DisplayName)
            .ToList();
        Assert.True(unnamed.Count == 0, "Endpoint API thiếu .WithName(...) (operationId): " + string.Join(", ", unnamed));
        var names = apiEndpoints.Select(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()!.EndpointName).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        Assert.All(names, n => Assert.Matches("^[a-z][A-Za-z]+$", n));

        var outside = routes
            .Where(r => !r.StartsWith(prefix, StringComparison.Ordinal))
            .Where(r => !AllowedOutsideApi.Any(a => r.StartsWith(a, StringComparison.Ordinal)))
            .ToList();
        Assert.True(outside.Count == 0, $"Endpoint nằm ngoài {ApiRoutes.Prefix}: " + string.Join(", ", outside));
    }
}
