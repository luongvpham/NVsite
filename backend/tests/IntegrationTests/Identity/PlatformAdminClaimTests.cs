using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vsite.Api.Identity;
using Vsite.Domain.Authorization;
using Vsite.Domain.Identity;
using Vsite.Domain.Shop.Entities;
using Vsite.Domain.Shop.Enums;
using Vsite.Infrastructure.Persistence;
using static Vsite.IntegrationTests.RefreshCookieTestHelpers;

namespace Vsite.IntegrationTests;

/// <summary>
/// JOBS-001 T1 / #114 mục 1–3, 5 — claim <c>role = "PlatformAdmin"</c> chỉ phát cho token audience
/// <c>vsite-portal</c>, tính lại từ <c>User.RoleId</c> ở cả login lẫn refresh; policy
/// <see cref="AuthPolicies.PlatformAdmin"/> kiểm role claim VÀ audience.
/// </summary>
[Collection(IdentityApiCollection.Name)]
public sealed class PlatformAdminClaimTests(IdentityApiFactory factory)
{
    private const string Password = "Password123!";
    private const string PortalHost = "admin.vsite.local";

    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    [Fact]
    public async Task Portal_login_of_platform_admin_has_role_claim()
    {
        var email = await RegisterAsync(PortalHost);
        await SetRoleAsync(email, WellKnownRoles.PlatformAdminId);

        var pair = await ReadTokenPairAsync(await LoginAsync(email, PortalHost));

        Assert.Equal(new[] { "PlatformAdmin" }, RoleClaims(pair.AccessToken));
        Assert.Equal("vsite-portal", Audience(pair.AccessToken));
    }

    [Fact]
    public async Task Portal_login_of_regular_user_has_no_role_claim()
    {
        var email = await RegisterAsync(PortalHost);

        var pair = await ReadTokenPairAsync(await LoginAsync(email, PortalHost));

        Assert.Empty(RoleClaims(pair.AccessToken));
    }

    [Fact]
    public async Task Shop_login_of_platform_admin_has_no_role_claim() // R1
    {
        var shopHost = await CreateShopHostAsync();
        var email = await RegisterAsync(shopHost);
        await SetRoleAsync(email, WellKnownRoles.PlatformAdminId);

        var pair = await ReadTokenPairAsync(await LoginAsync(email, shopHost));

        Assert.StartsWith("shop:", Audience(pair.AccessToken), StringComparison.Ordinal);
        Assert.Empty(RoleClaims(pair.AccessToken));
    }

    [Fact]
    public async Task Main_login_of_platform_admin_has_no_role_claim()
    {
        var email = await RegisterAsync(null);
        await SetRoleAsync(email, WellKnownRoles.PlatformAdminId);

        var pair = await ReadTokenPairAsync(await LoginAsync(email, null));

        Assert.Equal("vsite-main", Audience(pair.AccessToken));
        Assert.Empty(RoleClaims(pair.AccessToken));
    }

    [Fact]
    public async Task Refresh_after_role_removed_drops_the_claim()
    {
        var email = await RegisterAsync(PortalHost);
        await SetRoleAsync(email, WellKnownRoles.PlatformAdminId);
        var first = await ReadTokenPairAsync(await LoginAsync(email, PortalHost));
        Assert.Single(RoleClaims(first.AccessToken));

        await SetRoleAsync(email, WellKnownRoles.PlatformUserId);
        var refreshed = await ReadTokenPairAsync(
            await _client.SendAsync(Post("/api/auth/refresh-token", PortalHost, first.RefreshToken)));

        Assert.Empty(RoleClaims(refreshed.AccessToken));
    }

    [Fact]
    public async Task Refresh_while_still_admin_keeps_the_claim()
    {
        var email = await RegisterAsync(PortalHost);
        await SetRoleAsync(email, WellKnownRoles.PlatformAdminId);
        var first = await ReadTokenPairAsync(await LoginAsync(email, PortalHost));

        var refreshed = await ReadTokenPairAsync(
            await _client.SendAsync(Post("/api/auth/refresh-token", PortalHost, first.RefreshToken)));

        Assert.Equal(new[] { "PlatformAdmin" }, RoleClaims(refreshed.AccessToken));
    }

    [Fact]
    public async Task Refresh_after_role_granted_adds_the_claim()
    {
        // Refresh tính từ DB, không chép từ token cũ — chiều ngược lại của ca "gỡ role".
        var email = await RegisterAsync(PortalHost);
        var first = await ReadTokenPairAsync(await LoginAsync(email, PortalHost));
        Assert.Empty(RoleClaims(first.AccessToken));

        await SetRoleAsync(email, WellKnownRoles.PlatformAdminId);
        var refreshed = await ReadTokenPairAsync(
            await _client.SendAsync(Post("/api/auth/refresh-token", PortalHost, first.RefreshToken)));

        Assert.Equal(new[] { "PlatformAdmin" }, RoleClaims(refreshed.AccessToken));
    }

    // ---- policy (#114 mục 5): kiểm role claim VÀ audience, không dựa vào bước validate token ----

    [Theory]
    [InlineData("vsite-portal", "PlatformAdmin", true)]
    [InlineData("vsite-portal", null, false)]
    [InlineData("vsite-main", "PlatformAdmin", false)]
    [InlineData("shop:11111111-1111-1111-1111-111111111111", "PlatformAdmin", false)]
    public async Task Policy_requires_both_role_and_portal_audience(string audience, string? role, bool expected)
    {
        var claims = new List<Claim> { new("sub", Guid.NewGuid().ToString()), new("aud", audience) };
        if (role is not null)
        {
            claims.Add(new Claim("role", role));
        }

        // roleType = "role" như TokenValidationParameters.RoleClaimType, để RequireRole khớp.
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test", nameType: "name", roleType: "role"));
        var authz = factory.Services.GetRequiredService<IAuthorizationService>();

        var result = await authz.AuthorizeAsync(user, AuthPolicies.PlatformAdmin);

        Assert.Equal(expected, result.Succeeded);
    }

    [Fact]
    public async Task Bearer_validation_maps_role_claim_so_RequireRole_matches()
    {
        // Token thật đi qua JwtBearer: RoleClaimType = "role" phải được đặt, nếu không RequireRole
        // không bao giờ khớp dù claim có trong token.
        var email = await RegisterAsync(PortalHost);
        await SetRoleAsync(email, WellKnownRoles.PlatformAdminId);
        var pair = await ReadTokenPairAsync(await LoginAsync(email, PortalHost));

        var options = factory.Services
            .GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>>()
            .Get(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme);

        Assert.Equal("role", options.TokenValidationParameters.RoleClaimType);
        var validated = new JwtSecurityTokenHandler { MapInboundClaims = false }
            .ValidateToken(pair.AccessToken, options.TokenValidationParameters, out _);
        Assert.True(validated.IsInRole("PlatformAdmin"));
    }

    // ---- helpers ----

    private async Task<string> RegisterAsync(string? host)
    {
        var email = $"pa-{Guid.NewGuid():N}@example.test";
        var register = Post("/api/auth/register", host);
        register.Content = JsonContent.Create(new RegisterRequest(email, Password, "Platform Admin Test"));
        await _client.SendAsync(register);

        var token = factory.EmailSpy.ExtractLastTokenFor(email);
        var verify = new HttpRequestMessage(HttpMethod.Get, $"/api/auth/verify-email?token={Uri.EscapeDataString(token)}");
        if (host is not null)
        {
            verify.Headers.Host = host;
        }

        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(verify)).StatusCode);
        return email;
    }

    private Task<HttpResponseMessage> LoginAsync(string email, string? host)
    {
        var request = Post("/api/auth/login", host);
        request.Content = JsonContent.Create(new LoginRequest(email, Password));
        return _client.SendAsync(request);
    }

    private async Task<string> CreateShopHostAsync()
    {
        var shopId = Guid.NewGuid();
        var slug = $"pa-{shopId:N}";
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Shops.Add(new Shop(shopId, "Platform Admin Claim Shop", slug, ShopKind.Hosted, status: ShopStatus.Active));
        await db.SaveChangesAsync();
        return $"{slug}.vsite.local";
    }

    private async Task SetRoleAsync(string email, Guid roleId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var normalized = email.ToUpperInvariant();
        var updated = await db.Users.Where(u => u.EmailNormalized == normalized)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.RoleId, roleId));
        Assert.Equal(1, updated);
    }

    private static string[] RoleClaims(string jwt) =>
        new JwtSecurityTokenHandler().ReadJwtToken(jwt).Claims.Where(c => c.Type == "role").Select(c => c.Value).ToArray();

    private static string Audience(string jwt) =>
        new JwtSecurityTokenHandler().ReadJwtToken(jwt).Audiences.Single();
}
