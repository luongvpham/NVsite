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
