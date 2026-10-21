using System.Net.Http.Json;
using System.Text.Json;

namespace Vsite.IntegrationTests;

/// <summary>Cặp token sau login/refresh: access từ body JSON, refresh từ header Set-Cookie (#101 —
/// refresh token KHÔNG nằm trong body).</summary>
public sealed record TokenPair(string AccessToken, string RefreshToken);

internal static class RefreshCookieTestHelpers
{
    public const string CookieName = "vsite_rt";

    /// <summary>Dòng Set-Cookie của refresh token, hoặc null nếu response không set/xoá nó.</summary>
    public static string? FindRefreshSetCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith($"{CookieName}=", StringComparison.Ordinal))
            : null;

    /// <summary>Giá trị cookie (phần trước dấu `;` đầu tiên), rỗng nếu cookie bị xoá.</summary>
    public static string CookieValue(string setCookie) =>
        setCookie[(CookieName.Length + 1)..].Split(';')[0];

    public static async Task<TokenPair> ReadTokenPairAsync(HttpResponseMessage response)
    {
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var setCookie = FindRefreshSetCookie(response) ?? throw new InvalidOperationException("Response không set cookie refresh.");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return new TokenPair(doc.RootElement.GetProperty("accessToken").GetString()!, CookieValue(setCookie));
    }

    public static HttpRequestMessage WithRefreshCookie(this HttpRequestMessage request, string? token)
    {
        if (token is not null)
        {
            request.Headers.Add("Cookie", $"{CookieName}={token}");
        }

        return request;
    }

    public static HttpRequestMessage Post(string path, string? host, string? refreshCookie = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (host is not null)
        {
            request.Headers.Host = host;
        }

        return request.WithRefreshCookie(refreshCookie);
    }

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>());
}
