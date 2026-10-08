using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vsite.Api.Tenancy;

namespace Vsite.IntegrationTests;

/// <summary>
/// Đóng lỗ hổng đã bàn ở SHOP-001: "ai đó thêm endpoint `{shopId}` mới mà quên gắn
/// `ShopMembershipEndpointFilter`" — thay vì hy vọng agent/dev sau nhớ (#17), test này duyệt TOÀN
/// BỘ endpoint đã map thật (qua <see cref="EndpointDataSource"/>, không phải đọc source code bằng
/// regex), tìm route có tham số `shopId`, khẳng định endpoint đó có
/// <see cref="ShopMembershipRequiredMarker"/> — marker CHỈ được gắn bởi
/// <see cref="ShopScopedEndpointExtensions.RequireShopMembership{TBuilder}"/>, nên thiếu marker
/// nghĩa là ai đó gọi `.AddEndpointFilter&lt;ShopMembershipEndpointFilter&gt;()` thẳng (không nên)
/// hoặc quên gắn filter hẳn.
///
/// KHÔNG dùng `IdentityApiFactory`/`ShopApiFactory` (Testcontainers Postgres+Redis, đắt) — chỉ cần
/// build host để đọc `EndpointDataSource`, không gửi request nào chạm DB/cache, nên dùng
/// `WebApplicationFactory&lt;Program&gt;` trần với connection string giả, không cần Docker.
/// </summary>
public sealed class ShopScopedRouteFilterTests
{
    /// <summary>REFACTOR-BE-001 — quyền Owner chuyển từ handler về `RequireShopOwner`. Khoá danh sách
    /// endpoint Owner-only + error_code của từng cái (giữ đúng contract cũ), để không ai lỡ đổi
    /// về `RequireShopMembership()` mà mất kiểm role.</summary>
    [Theory]
    [InlineData("PATCH", "shops/{shopId:guid}", "SHOP_OWNER_REQUIRED")]
    [InlineData("PUT", "shops/{shopId:guid}/logo", "MEDIA_OWNER_REQUIRED")]
    [InlineData("DELETE", "shops/{shopId:guid}/media/library/{assetId:guid}", "MEDIA_OWNER_REQUIRED")]
    public void Owner_only_endpoints_carry_ShopOwnerRequirement(string method, string route, string errorCode)
    {
        using var factory = CreateFactory();
        var endpoint = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(e => string.Equals(e.RoutePattern.RawText?.TrimStart('/'), route, StringComparison.Ordinal)
                && e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods.Contains(method) == true);

        var requirement = endpoint.Metadata.GetMetadata<ShopOwnerRequirement>();
        Assert.NotNull(requirement);
        Assert.Equal(errorCode, requirement.ErrorCode);
        Assert.NotNull(endpoint.Metadata.GetMetadata<ShopMembershipRequiredMarker>());
    }

    [Fact]
    public void Every_route_with_shopId_parameter_requires_ShopMembership_marker()
    {
        using var factory = CreateFactory();

        var endpointDataSource = factory.Services.GetRequiredService<EndpointDataSource>();

        var violations = endpointDataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.Parameters.Any(p => p.Name == "shopId"))
            .Where(e => e.Metadata.GetMetadata<ShopMembershipRequiredMarker>() is null)
            .Select(e => e.RoutePattern.RawText ?? e.DisplayName ?? "(unknown route)")
            .ToList();

        Assert.True(
            violations.Count == 0,
            "Endpoint có {shopId} trong route nhưng THIẾU .RequireShopMembership() " +
            "(Vsite.Api.Tenancy.ShopScopedEndpointExtensions) — Global Query Filter sẽ trả rỗng " +
            "hoặc, nếu ai đó sau này bypass filter, có thể lộ dữ liệu xuyên shop. Route vi phạm: " +
            string.Join(", ", violations));
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // Giá trị giả — test này không gửi request nào chạm DB/Redis thật, chỉ đọc
                    // EndpointDataSource sau khi host build xong.
                    ["ConnectionStrings:Identity"] = "Host=localhost;Port=1;Database=fake;Username=fake;Password=fake",
                    ["ConnectionStrings:Redis"] = "localhost:1",
                    ["Jwt:SigningKey"] = "test-signing-key-not-for-production-use-32-chars-min",
                    ["Jwt:Issuer"] = "vsite-test",
                    ["Jwt:AccessTokenLifetimeMinutes"] = "15",
                    ["Auth:ApiBaseUrl"] = "http://localhost",
                });
            });
        });
}
