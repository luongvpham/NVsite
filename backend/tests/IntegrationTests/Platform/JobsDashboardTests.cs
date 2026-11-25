using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Vsite.Api.Platform;

namespace Vsite.IntegrationTests;

/// <summary>
/// JOBS-001 T4 / #114 · #115 · R2, R3, R8 — dashboard <c>/hangfire</c> chỉ mở ở host admin kèm cookie
/// dashboard hợp lệ (<see cref="JobsDashboardCookie"/>). Mọi trường hợp từ chối đều 401, không ném 500.
/// Storage Hangfire chạy trên Postgres thật (Testcontainers); `Jobs:ServerEnabled=false` nên không có
/// worker nền nào chạy trong test (R8).
/// </summary>
[Collection(IdentityApiCollection.Name)]
public sealed class JobsDashboardTests(IdentityApiFactory factory)
{
    private const string PortalHost = "admin.vsite.local";
    private const string DashboardPath = "/hangfire/";

    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        HandleCookies = false,
        AllowAutoRedirect = false,
    });

    [Fact]
    public async Task Admin_host_with_valid_cookie_gets_200()
    {
        var response = await GetAsync(PortalHost, IssueCookie());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task No_cookie_gets_401()
    {
        var response = await GetAsync(PortalHost, null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Expired_cookie_gets_401()
    {
        var past = new FixedTimeProvider(DateTimeOffset.UtcNow - TimeSpan.FromHours(1));
        var response = await GetAsync(PortalHost, IssueCookie(past));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Tampered_cookie_gets_401_not_500()
    {
        var raw = IssueCookie();
        var tampered = raw[..^3] + (raw.EndsWith("AAA", StringComparison.Ordinal) ? "BBB" : "AAA");

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(PortalHost, tampered)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(PortalHost, "not-a-protected-payload")).StatusCode);
    }

    [Fact]
    public async Task Valid_cookie_on_shop_host_gets_401() // R2
    {
        var host = await CreateShopHostAsync();

        var response = await GetAsync(host, IssueCookie());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Valid_cookie_on_api_host_gets_401() // R2
    {
        var response = await GetAsync("api.vsite.local", IssueCookie());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_action_with_valid_cookie_is_allowed_and_without_cookie_gets_401()
    {
        // Thao tác ghi của dashboard (trigger job định kỳ) cũng đi qua filter cookie — không chỉ GET trang.
        var allowed = await PostAsync(PortalHost, IssueCookie(), origin: $"http://{PortalHost}");
        AssertAllowed(allowed);

        var denied = await PostAsync(PortalHost, null, origin: $"http://{PortalHost}");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    // W1 / JOBS-001-D1 — chống CSRF: POST phải cùng origin với host admin.
    [Fact]
    public async Task Post_with_valid_cookie_from_shop_origin_gets_401()
    {
        var response = await PostAsync(PortalHost, IssueCookie(), origin: "http://spa-abc.vsite.local");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_with_valid_cookie_and_no_origin_nor_fetch_site_gets_401()
    {
        var response = await PostAsync(PortalHost, IssueCookie());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_without_origin_but_sec_fetch_site_same_origin_is_allowed()
    {
        var response = await PostAsync(PortalHost, IssueCookie(), secFetchSite: "same-origin");

        AssertAllowed(response);
    }

    [Theory]
    [InlineData("same-site")]
    [InlineData("cross-site")]
    [InlineData("none")]
    public async Task Post_without_origin_and_sec_fetch_site_not_same_origin_gets_401(string site)
    {
        var response = await PostAsync(PortalHost, IssueCookie(), secFetchSite: site);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("garbage")]
    [InlineData("http://")]
    [InlineData("::::")]
    public async Task Post_with_malformed_origin_gets_401_not_500(string origin)
    {
        var response = await PostAsync(PortalHost, IssueCookie(), origin: origin);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_with_valid_cookie_is_allowed_without_origin()
    {
        var response = await GetAsync(PortalHost, IssueCookie());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static void AssertAllowed(HttpResponseMessage response)
    {
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True((int)response.StatusCode < 500, $"POST dashboard trả {(int)response.StatusCode}");
    }

    private async Task<HttpResponseMessage> PostAsync(
        string host, string? cookieValue, string? origin = null, string? secFetchSite = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/hangfire/recurring/trigger")
        {
            Content = new FormUrlEncodedContent([new KeyValuePair<string, string>("jobs[]", "no-such-recurring-job")]),
        };
        request.Headers.Host = host;
        if (cookieValue is not null)
        {
            request.Headers.Add("Cookie", $"{JobsDashboardCookie.Name}={Uri.EscapeDataString(cookieValue)}");
        }

        if (origin is not null)
        {
            request.Headers.TryAddWithoutValidation("Origin", origin);
        }

        if (secFetchSite is not null)
        {
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", secFetchSite);
        }

        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> GetAsync(string host, string? cookieValue)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, DashboardPath);
        request.Headers.Host = host;
        if (cookieValue is not null)
        {
            request.Headers.Add("Cookie", $"{JobsDashboardCookie.Name}={Uri.EscapeDataString(cookieValue)}");
        }

        return await _client.SendAsync(request);
    }

    /// <summary>Phát cookie bằng đúng cơ chế production (cùng DataProtection của app) rồi lấy giá trị đã giải mã URL.</summary>
    private string IssueCookie(TimeProvider? time = null)
    {
        var cookie = time is null
            ? factory.Services.GetRequiredService<JobsDashboardCookie>()
            : new JobsDashboardCookie(factory.Services.GetRequiredService<IDataProtectionProvider>(), time, isDevelopment: true);

        var context = new DefaultHttpContext();
        cookie.Issue(context.Response, Guid.NewGuid());
        var header = context.Response.Headers.SetCookie.ToString();
        return Uri.UnescapeDataString(header[$"{JobsDashboardCookie.Name}=".Length..].Split(';')[0]);
    }

    private async Task<string> CreateShopHostAsync()
    {
        var shopId = Guid.NewGuid();
        var slug = $"jd-{shopId:N}";
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Vsite.Infrastructure.Persistence.AppDbContext>();
        db.Shops.Add(new Vsite.Domain.Shop.Entities.Shop(
            shopId, "Jobs Dashboard Shop", slug, Vsite.Domain.Shop.Enums.ShopKind.Hosted,
            status: Vsite.Domain.Shop.Enums.ShopStatus.Active));
        await db.SaveChangesAsync();
        return $"{slug}.vsite.local";
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
