using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vsite.Api.Identity;
using Vsite.Api.Platform;
using Vsite.Domain.Identity;
using Vsite.Domain.Shop.Entities;
using Vsite.Domain.Shop.Enums;
using Vsite.Infrastructure.Persistence;
using static Vsite.IntegrationTests.RefreshCookieTestHelpers;

namespace Vsite.IntegrationTests;

/// <summary>
/// JOBS-001 T3 / #114 mục 5, 6, 8 · #115 — <c>POST /api/platform/jobs/dashboard-session</c> đổi JWT
/// PlatformAdmin lấy cookie dashboard Hangfire; nhóm <c>/platform</c> mang policy <c>PlatformAdmin</c>.
/// </summary>
[Collection(IdentityApiCollection.Name)]
public sealed class PlatformPolicyTests(IdentityApiFactory factory)
{
    private const string Password = "Password123!";
    private const string PortalHost = "admin.vsite.local";
    private const string Path = "/api/platform/jobs/dashboard-session";
    private const string CookiePrefix = "vsite_jobs_dashboard=";

    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    [Fact]
    public async Task Platform_admin_on_portal_gets_204_and_dashboard_cookie()
    {
        var email = await RegisterAsync(PortalHost);
        await SetRoleAsync(email, WellKnownRoles.PlatformAdminId);
        var pair = await ReadTokenPairAsync(await LoginAsync(email, PortalHost));

        var response = await _client.SendAsync(PostWithToken(pair.AccessToken, PortalHost));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var setCookie = response.Headers.GetValues("Set-Cookie")
            .Single(v => v.StartsWith(CookiePrefix, StringComparison.Ordinal));
        var attrs = setCookie.Split(';').Select(s => s.Trim()).ToList();
        Assert.Contains("httponly", attrs, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", attrs, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("path=/hangfire", attrs, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("max-age=900", attrs, StringComparer.OrdinalIgnoreCase);
        // Development (factory) => không Secure; ngoài Development thì có — xem ca unit bên dưới.
        Assert.DoesNotContain("secure", attrs, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(attrs, a => a.StartsWith("domain=", StringComparison.OrdinalIgnoreCase));

        // Cookie phát ra phải được JobsDashboardCookie đọc lại đúng người.
        var cookie = factory.Services.GetRequiredService<JobsDashboardCookie>();
        var value = setCookie[CookiePrefix.Length..].Split(';')[0];
        Assert.True(cookie.TryValidate(Uri.UnescapeDataString(value), out var userId));
        Assert.NotEqual(Guid.Empty, userId);
    }

    [Fact]
    public async Task No_token_gets_401_UNAUTHORIZED()
    {
        var response = await _client.SendAsync(Post(Path, PortalHost));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("UNAUTHORIZED", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Regular_portal_user_gets_403_PLATFORM_ADMIN_REQUIRED()
    {
        var email = await RegisterAsync(PortalHost);
        var pair = await ReadTokenPairAsync(await LoginAsync(email, PortalHost));

        var response = await _client.SendAsync(PostWithToken(pair.AccessToken, PortalHost));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("PLATFORM_ADMIN_REQUIRED", await ErrorCodeAsync(response));
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Shop_token_of_platform_admin_user_gets_403() // R1
    {
        var shopHost = await CreateShopHostAsync();
        var email = await RegisterAsync(shopHost);
        await SetRoleAsync(email, WellKnownRoles.PlatformAdminId);
        var pair = await ReadTokenPairAsync(await LoginAsync(email, shopHost));

        // Gọi trên host admin lẫn host shop: không đường nào ra 204.
        foreach (var host in new[] { PortalHost, shopHost })
        {
            var response = await _client.SendAsync(PostWithToken(pair.AccessToken, host));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.False(response.Headers.Contains("Set-Cookie"));
            // Host admin: ShopMembershipValidationMiddleware chặn token shop sai audience trước khi tới policy;
            // host shop: tới được policy PlatformAdmin ⇒ PLATFORM_ADMIN_REQUIRED. Hai mã khác nhau, đều 403.
            var expected = host == PortalHost ? "TOKEN_SHOP_MISMATCH" : "PLATFORM_ADMIN_REQUIRED";
            Assert.Equal(expected, await ErrorCodeAsync(response));
        }
    }

    [Fact]
    public async Task Main_token_of_platform_admin_user_gets_403_PLATFORM_ADMIN_REQUIRED()
    {
        var email = await RegisterAsync(null);
        await SetRoleAsync(email, WellKnownRoles.PlatformAdminId);
        var pair = await ReadTokenPairAsync(await LoginAsync(email, null));

        var response = await _client.SendAsync(PostWithToken(pair.AccessToken, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("PLATFORM_ADMIN_REQUIRED", await ErrorCodeAsync(response));
    }

    // ---- JobsDashboardCookie: không cần Postgres ----

    [Fact]
    public void Cookie_validate_rejects_tamper_wrong_purpose_and_expiry_without_throwing()
    {
        var dp = new EphemeralDataProtectionProvider();
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 11, 24, 3, 0, 0, TimeSpan.Zero));
        var cookie = new JobsDashboardCookie(dp, clock, isDevelopment: true);
        var userId = Guid.NewGuid();

        var context = new DefaultHttpContext();
        cookie.Issue(context.Response, userId);
        var raw = Uri.UnescapeDataString(
            context.Response.Headers.SetCookie.ToString()[CookiePrefix.Length..].Split(';')[0]);

        Assert.True(cookie.TryValidate(raw, out var read));
        Assert.Equal(userId, read);

        // sửa / rác / rỗng / null
        Assert.False(cookie.TryValidate(raw[..^3] + "AAA", out _));
        Assert.False(cookie.TryValidate("not-a-protected-payload", out _));
        Assert.False(cookie.TryValidate("", out _));
        Assert.False(cookie.TryValidate((string?)null, out _));

        // ký bằng purpose khác (payload hợp lệ, hạn xa)
        // Payload dựng bằng CÙNG serializer/khoá (PascalCase) như payload thật — nếu dùng khoá camelCase thì
        // deserialize ra Guid.Empty và test đỏ-giả-xanh ngay cả khi purpose bị bỏ qua.
        var forgedPayload = JsonSerializer.Serialize(new { UserId = userId, ExpiresAt = new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero) });
        // Đối chứng: cùng payload ký bằng ĐÚNG purpose thì hợp lệ ⇒ payload không phải lý do bị từ chối bên dưới.
        Assert.True(cookie.TryValidate(dp.CreateProtector(JobsDashboardCookie.Purpose).Protect(forgedPayload), out _));
        var foreign = dp.CreateProtector("Another.Purpose").Protect(forgedPayload);
        Assert.False(cookie.TryValidate(foreign, out _));

        // hết hạn: còn hạn tới < expiresAt, hết ở >= expiresAt
        clock.Advance(TimeSpan.FromMinutes(15) - TimeSpan.FromSeconds(1));
        Assert.True(cookie.TryValidate(raw, out _));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.False(cookie.TryValidate(raw, out _));
    }

    [Fact]
    public void Cookie_is_Secure_outside_Development()
    {
        var cookie = new JobsDashboardCookie(new EphemeralDataProtectionProvider(), TimeProvider.System, isDevelopment: false);
        var context = new DefaultHttpContext();

        cookie.Issue(context.Response, Guid.NewGuid());

        var attrs = context.Response.Headers.SetCookie.ToString().Split(';').Select(s => s.Trim()).ToList();
        Assert.Contains("secure", attrs, StringComparer.OrdinalIgnoreCase);
    }

    // ---- helpers ----

    private sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("error_code").GetString();
    }

    private static HttpRequestMessage PostWithToken(string accessToken, string? host)
    {
        var request = Post(Path, host);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private async Task<string> RegisterAsync(string? host)
    {
        var email = $"pp-{Guid.NewGuid():N}@example.test";
        var register = Post("/api/auth/register", host);
        register.Content = JsonContent.Create(new RegisterRequest(email, Password, "Platform Policy Test"));
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
        var slug = $"pp-{shopId:N}";
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Shops.Add(new Shop(shopId, "Platform Policy Shop", slug, ShopKind.Hosted, status: ShopStatus.Active));
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
}
