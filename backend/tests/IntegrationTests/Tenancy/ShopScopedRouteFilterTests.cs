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
    [InlineData("PATCH", "api/shops/{shopId:guid}", "SHOP_OWNER_REQUIRED")]
    [InlineData("PUT", "api/shops/{shopId:guid}/logo", "MEDIA_OWNER_REQUIRED")]
    [InlineData("DELETE", "api/shops/{shopId:guid}/media/library/{assetId:guid}", "MEDIA_OWNER_REQUIRED")]
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

    /// <summary>#100 (T10, WEBSITE-001) — MỌI endpoint của document OpenAPI "website" phải Owner-only (đọc lẫn ghi), mã lỗi
    /// <c>WEBSITE_OWNER_REQUIRED</c>. Thêm endpoint website mới mà quên <c>RequireShopOwner</c> là RED ngay (#17).</summary>
    [Fact]
    public void Every_website_endpoint_requires_owner_with_WEBSITE_OWNER_REQUIRED() =>
        AssertEveryEndpointOfDocumentIsOwnerOnly("website", "WEBSITE_OWNER_REQUIRED", expectedMinimum: 10);

    private static void AssertEveryEndpointOfDocumentIsOwnerOnly(string document, string errorCode, int expectedMinimum)
    {
        using var factory = CreateFactory();
        var all = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();

        var scoped = all.Where(e => BelongsToDocument(e, document)).ToList();
        Assert.True(scoped.Count >= expectedMinimum, $"Document '{document}' chỉ có {scoped.Count} endpoint (kỳ vọng >= {expectedMinimum}) — map có đủ không?");

        var violations = FindOwnerViolations(all, document, errorCode);
        Assert.True(violations.Count == 0,
            $"Endpoint của document '{document}' thiếu .RequireShopOwner(\"{errorCode}\", …) (#100): " + string.Join(", ", violations));
    }

    /// <summary>Thuộc document nếu khai <c>WithGroupName(document)</c> HOẶC route nằm dưới <c>/api/shops/{shopId}/{document}</c>
    /// (media: thêm <c>PUT …/logo</c>) — kể cả khi quên <c>WithGroupName</c>, để route lạc group không lọt kiểm tra.</summary>
    private static bool BelongsToDocument(RouteEndpoint e, string document)
    {
        if (e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.IEndpointGroupNameMetadata>()?.EndpointGroupName == document)
        {
            return true;
        }

        var raw = (e.RoutePattern.RawText ?? string.Empty).TrimStart('/');
        return System.Text.RegularExpressions.Regex.IsMatch(raw, $@"^api/shops/\{{shopId[^}}]*\}}/{document}(/|$)")
            || (document == "media" && System.Text.RegularExpressions.Regex.IsMatch(raw, @"^api/shops/\{shopId[^}]*\}/logo$"));
    }

    private static List<string> FindOwnerViolations(IEnumerable<RouteEndpoint> endpoints, string document, string errorCode) =>
        endpoints
            .Where(e => BelongsToDocument(e, document))
            .Where(e => e.Metadata.GetMetadata<ShopOwnerRequirement>() is not { } r || r.ErrorCode != errorCode
                || e.Metadata.GetMetadata<ShopMembershipRequiredMarker>() is null)
            .Select(e => $"{e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods.FirstOrDefault()} {e.RoutePattern.RawText}")
            .ToList();

    private static RouteEndpoint FakeEndpoint(string pattern, params object[] metadata)
    {
        var builder = new RouteEndpointBuilder(_ => Task.CompletedTask, Microsoft.AspNetCore.Routing.Patterns.RoutePatternFactory.Parse(pattern), 0);
        foreach (var m in metadata)
        {
            builder.Metadata.Add(m);
        }

        return (RouteEndpoint)builder.Build();
    }

    /// <summary>Chứng minh bộ lọc không mù: route nằm NGOÀI group (không WithGroupName) dưới prefix website/media mà chỉ
    /// RequireShopMembership hoặc sai mã lỗi thì bị bắt; route đúng thì không.</summary>
    [Fact]
    public void Owner_check_catches_routes_outside_the_group_that_forgot_owner_requirement()
    {
        var membershipOnly = FakeEndpoint("/api/shops/{shopId:guid}/website/rogue", new ShopMembershipRequiredMarker());
        var wrongCode = FakeEndpoint("/api/shops/{shopId:guid}/media/rogue", new ShopMembershipRequiredMarker(), new ShopOwnerRequirement("SHOP_OWNER_REQUIRED", "x"));
        var rogueLogo = FakeEndpoint("/api/shops/{shopId:guid}/logo", new ShopMembershipRequiredMarker());
        var good = FakeEndpoint("/api/shops/{shopId:guid}/website/ok", new ShopMembershipRequiredMarker(), new ShopOwnerRequirement("WEBSITE_OWNER_REQUIRED", "x"));
        var unrelated = FakeEndpoint("/api/shops/{shopId:guid}", new ShopMembershipRequiredMarker());

        var website = FindOwnerViolations([membershipOnly, wrongCode, rogueLogo, good, unrelated], "website", "WEBSITE_OWNER_REQUIRED");
        var media = FindOwnerViolations([membershipOnly, wrongCode, rogueLogo, good, unrelated], "media", "MEDIA_OWNER_REQUIRED");

        Assert.Equal(["/api/shops/{shopId:guid}/website/rogue"], website.Select(v => v[(v.IndexOf(' ') + 1)..]).ToList());
        Assert.Equal(2, media.Count);
        Assert.Contains(media, v => v.EndsWith("/media/rogue", StringComparison.Ordinal));
        Assert.Contains(media, v => v.EndsWith("/logo", StringComparison.Ordinal));
    }

    /// <summary>#100 (T10b) — MỌI endpoint của document "media" (kể cả `PUT /shops/{shopId}/logo`) Owner-only với `MEDIA_OWNER_REQUIRED`.</summary>
    [Fact]
    public void Every_media_endpoint_requires_owner_with_MEDIA_OWNER_REQUIRED() =>
        AssertEveryEndpointOfDocumentIsOwnerOnly("media", "MEDIA_OWNER_REQUIRED", expectedMinimum: 10);

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
