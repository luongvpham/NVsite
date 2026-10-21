using Microsoft.AspNetCore.Http;

namespace Vsite.Api.Identity;

/// <summary>
/// #101 — MỘT chỗ duy nhất giữ cờ của cookie refresh token. `HttpOnly` (JS không đọc được),
/// `SameSite=Lax`, `Path=/api/auth` (chỉ gửi tới login/refresh/logout), host-only (KHÔNG đặt
/// `Domain`: mỗi host — admin.vsite.vn, vsite.vn, domain shop — một cookie riêng, khớp token tách theo
/// audience). `Secure` theo MÔI TRƯỜNG chứ không theo `Request.IsHttps` vì backend chưa bật
/// `UseForwardedHeaders` — sau Caddy `IsHttps` luôn false. Không bao giờ log giá trị cookie.
/// </summary>
public static class RefreshTokenCookie
{
    public const string Name = "vsite_rt";
    public const string Path = "/api/auth";

    public static void Append(HttpResponse response, string token, DateTimeOffset expiresAt, bool isDevelopment) =>
        response.Cookies.Append(Name, token, Options(isDevelopment, expiresAt));

    public static string? Read(HttpRequest request) =>
        request.Cookies.TryGetValue(Name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    public static void Delete(HttpResponse response, bool isDevelopment) =>
        response.Cookies.Delete(Name, Options(isDevelopment, expiresAt: null));

    private static CookieOptions Options(bool isDevelopment, DateTimeOffset? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = !isDevelopment,
        SameSite = SameSiteMode.Lax,
        Path = Path,
        Expires = expiresAt,
    };
}
