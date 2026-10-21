using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Vsite.Api.Identity;
using Vsite.Infrastructure.Persistence;
using static Vsite.IntegrationTests.RefreshCookieTestHelpers;

namespace Vsite.IntegrationTests;

/// <summary>
/// IDENTITY-002 / #101 — refresh token chỉ sống trong cookie httpOnly; `/auth/logout` thu hồi + xoá.
/// Đi qua pipeline HTTP thật (`IdentityApiFactory`).
/// </summary>
[Collection(IdentityApiCollection.Name)]
public sealed class RefreshTokenCookieTests(IdentityApiFactory factory)
{
    private const string Password = "Password123!";

    // Tắt cookie jar: test tự gắn header Cookie để điều khiển token nào được gửi (#101).
    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    [Fact]
    public async Task Login_sets_httpOnly_cookie_and_body_has_no_refresh_token() // R1, R6
    {
        var email = await RegisterAsync();

        var response = await LoginAsync(email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var setCookie = FindRefreshSetCookie(response);
        Assert.NotNull(setCookie);
        Assert.NotEmpty(CookieValue(setCookie));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
        // Factory chạy môi trường Development → KHÔNG Secure (http). Bắt lỗi endpoint truyền ngược cờ môi trường.
        Assert.DoesNotContain("secure", setCookie, StringComparison.OrdinalIgnoreCase);

        var body = await ReadJsonAsync(response);
        Assert.True(body.TryGetProperty("accessToken", out _));
        Assert.True(body.TryGetProperty("accessTokenExpiresAt", out _));
        Assert.False(body.TryGetProperty("refreshToken", out _));
        Assert.False(body.TryGetProperty("refreshTokenExpiresAt", out _));
    }

    [Fact]
    public async Task Failed_login_does_not_set_cookie()
    {
        var email = await RegisterAsync();

        var response = await LoginAsync(email, "WrongPassword123!");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(FindRefreshSetCookie(response));
    }

    [Fact]
    public async Task Refresh_reads_cookie_rotates_it_and_returns_access_token_only() // R1
    {
        var email = await RegisterAsync();
        var first = await ReadTokenPairAsync(await LoginAsync(email));

        var response = await _client.SendAsync(Post("/api/auth/refresh-token", null, first.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rotated = await ReadTokenPairAsync(response);
        Assert.NotEqual(first.RefreshToken, rotated.RefreshToken);
        var body = await ReadJsonAsync(response);
        Assert.False(body.TryGetProperty("refreshToken", out _));
    }

    [Theory] // R2, M4 — không có phiên để xoá: 401 và KHÔNG đụng cookie
    [InlineData(null)]
    [InlineData("%20")]
    public async Task Refresh_without_a_usable_cookie_is_401_and_sets_nothing(string? cookie)
    {
        var response = await _client.SendAsync(Post("/api/auth/refresh-token", null, cookie));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(FindRefreshSetCookie(response));
        Assert.Equal("UNAUTHORIZED", (await ReadJsonAsync(response)).GetProperty("error_code").GetString());
    }

    [Fact] // I2 — cookie sai/hết hạn/đã thu hồi phải bị xoá, nếu không mỗi lần mở trang lại bị coi là reuse
    public async Task Refresh_with_a_garbage_cookie_is_401_and_clears_it()
    {
        var response = await _client.SendAsync(Post("/api/auth/refresh-token", null, "not-a-real-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var cleared = FindRefreshSetCookie(response);
        Assert.NotNull(cleared);
        Assert.Empty(CookieValue(cleared));
        Assert.Contains("path=/api/auth", cleared, StringComparison.OrdinalIgnoreCase);
        var body = await ReadJsonAsync(response);
        Assert.Equal("UNAUTHORIZED", body.GetProperty("error_code").GetString());
        Assert.Equal(401, body.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Refresh_ignores_a_refresh_token_sent_in_the_body()
    {
        var email = await RegisterAsync();
        var tokens = await ReadTokenPairAsync(await LoginAsync(email));

        var request = Post("/api/auth/refresh-token", null);
        request.Content = JsonContent.Create(new { refreshToken = tokens.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Reusing_a_rotated_cookie_is_401_and_revokes_the_whole_scope()
    {
        var email = await RegisterAsync();
        var first = await ReadTokenPairAsync(await LoginAsync(email));
        var second = await ReadTokenPairAsync(await _client.SendAsync(Post("/api/auth/refresh-token", null, first.RefreshToken)));

        var reuse = await _client.SendAsync(Post("/api/auth/refresh-token", null, first.RefreshToken));
        var afterReuse = await _client.SendAsync(Post("/api/auth/refresh-token", null, second.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
        Assert.Empty(CookieValue(FindRefreshSetCookie(reuse)!)); // I2 — cookie đã thu hồi bị xoá khỏi trình duyệt
    }

    [Fact]
    public async Task Logout_revokes_only_that_token_and_clears_cookie() // R5
    {
        var email = await RegisterAsync();
        var current = await ReadTokenPairAsync(await LoginAsync(email));
        var other = await ReadTokenPairAsync(await LoginAsync(email));

        var response = await _client.SendAsync(Post("/api/auth/logout", null, current.RefreshToken));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cleared = FindRefreshSetCookie(response);
        Assert.NotNull(cleared);
        Assert.Empty(CookieValue(cleared));
        Assert.Contains("path=/api/auth", cleared, StringComparison.OrdinalIgnoreCase);

        // Token khác của cùng user (phiên khác) vẫn dùng được: logout không thu hồi cả nhóm.
        // (Kiểm TRƯỚC: dùng lại token đã logout bị coi là reuse và thu hồi cả nhóm theo #3.)
        Assert.Equal(HttpStatusCode.OK,
            (await _client.SendAsync(Post("/api/auth/refresh-token", null, other.RefreshToken))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _client.SendAsync(Post("/api/auth/refresh-token", null, current.RefreshToken))).StatusCode);
    }

    [Fact]
    public async Task Logout_is_idempotent_for_missing_unknown_or_already_revoked_token() // R4
    {
        var email = await RegisterAsync();
        var tokens = await ReadTokenPairAsync(await LoginAsync(email));
        var sibling = await ReadTokenPairAsync(await LoginAsync(email));

        await _client.SendAsync(Post("/api/auth/logout", null, tokens.RefreshToken));
        var again = await _client.SendAsync(Post("/api/auth/logout", null, tokens.RefreshToken));
        var unknown = await _client.SendAsync(Post("/api/auth/logout", null, "not-a-real-token"));
        var noCookie = await _client.SendAsync(Post("/api/auth/logout", null));

        foreach (var response in new[] { again, unknown, noCookie })
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Empty(CookieValue(FindRefreshSetCookie(response)!)); // luôn xoá cookie (R4)
        }

        // Logout lặp lại KHÔNG bị coi là "reuse" → không thu hồi cả nhóm.
        Assert.Equal(HttpStatusCode.OK,
            (await _client.SendAsync(Post("/api/auth/refresh-token", null, sibling.RefreshToken))).StatusCode);
    }

    [Fact]
    public async Task Logout_only_touches_the_presented_token_row()
    {
        var email = await RegisterAsync();
        var tokens = await ReadTokenPairAsync(await LoginAsync(email));
        await ReadTokenPairAsync(await LoginAsync(email));

        await _client.SendAsync(Post("/api/auth/logout", null, tokens.RefreshToken));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await db.Users.Where(u => u.EmailNormalized == email.ToUpperInvariant()).Select(u => u.Id).SingleAsync();
        var revoked = await db.RefreshTokens.CountAsync(t => t.UserId == userId && t.RevokedAt != null);
        var live = await db.RefreshTokens.CountAsync(t => t.UserId == userId && t.RevokedAt == null);
        Assert.Equal(1, revoked);
        Assert.Equal(1, live);
    }

    [Theory] // T3 — dev gọi chéo cổng (Portal :5173, web :3000) cần CORS cho phép gửi/nhận cookie
    [InlineData("http://localhost:5173")]
    [InlineData("http://localhost:3000")]
    public async Task Dev_cors_allows_credentials_for_known_origins(string origin)
    {
        var request = Post("/api/auth/refresh-token", null, "x");
        request.Headers.Add("Origin", origin);

        var response = await _client.SendAsync(request);

        Assert.Equal(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
    }

    [Fact]
    public async Task Dev_cors_does_not_allow_unknown_origin()
    {
        var request = Post("/api/auth/refresh-token", null, "x");
        request.Headers.Add("Origin", "http://evil.example");

        var response = await _client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Theory] // R6 — `Secure` theo môi trường, không theo IsHttps (chưa có UseForwardedHeaders)
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Cookie_flags_follow_environment(bool isDevelopment, bool expectSecure)
    {
        var context = new DefaultHttpContext();
        var expires = DateTimeOffset.UtcNow.AddDays(30);

        RefreshTokenCookie.Append(context.Response, "tok", expires, isDevelopment);

        var header = context.Response.Headers.SetCookie.ToString();
        Assert.Contains("vsite_rt=tok", header, StringComparison.Ordinal);
        Assert.Contains("httponly", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", header, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", header, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(expectSecure, header.Contains("secure", StringComparison.OrdinalIgnoreCase));
    }

    // ---- helpers ----

    private async Task<string> RegisterAsync()
    {
        var email = $"rt-{Guid.NewGuid():N}@example.test";
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, Password, "Cookie"));
        var token = factory.EmailSpy.ExtractLastTokenFor(email);
        var verify = await _client.GetAsync($"/api/auth/verify-email?token={Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        return email;
    }

    private Task<HttpResponseMessage> LoginAsync(string email, string password = Password) =>
        _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
}
