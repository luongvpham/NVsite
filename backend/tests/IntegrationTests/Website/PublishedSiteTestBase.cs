using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vsite.Api.Shop;
using Vsite.Application.Website.Dtos;
using Vsite.Application.Website.Interfaces;
using Vsite.Domain.Shop.Enums;
using Vsite.Infrastructure.Persistence;
using Vsite.IntegrationTests.Media;

namespace Vsite.IntegrationTests.Website;

/// <summary>
/// Helper chung cho test HTTP của <c>GET /api/site</c> (T6 <c>PublishedSiteEndpointTests</c>, T7 <c>PublishedSiteTenantIsolationTests</c>):
/// dựng shop + website + publish bằng API thật, gọi site theo Host, ghi DB trực tiếp rồi xoá con trỏ Redis. CẦN Docker.
/// </summary>
public abstract class PublishedSiteTestBase
{
    protected readonly ShopApiHarness _h;

    protected PublishedSiteTestBase(MediaApiFactory factory) => _h = new ShopApiHarness(factory);

    protected static string Site(Guid shopId) => $"/api/shops/{shopId}/website";

    protected static string Publications(Guid shopId) => Site(shopId) + "/publications";

    protected static async Task<JsonElement> JsonOf(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    protected static async Task<string> ErrorCode(HttpResponseMessage response) =>
        (await JsonOf(response)).GetProperty("error_code").GetString()!;

    protected async Task<string> SlugOf(Guid shopId)
    {
        await using var scope = _h.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database.SqlQuery<string>($"SELECT slug AS \"Value\" FROM shop WHERE id = {shopId}").FirstAsync();
    }

    protected static string HostOf(string slug) => $"{slug}.vsite.local";

    protected async Task<HttpResponseMessage> GetSiteAsync(
        string host, string? ifNoneMatch = null, string? token = null, string path = "/api/site",
        IReadOnlyDictionary<string, string>? extraHeaders = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path) { Headers = { Host = host } };
        if (ifNoneMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);
        }

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        foreach (var (name, value) in extraHeaders ?? new Dictionary<string, string>())
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        return await _h.Client.SendAsync(request);
    }

    protected async Task<(ShopApiHarness.TestUser Owner, Guid ShopId, string Host)> OwnerWithWebsiteAsync()
    {
        var (owner, shopId) = await _h.NewOwnerWithShopAsync();
        var create = await _h.SendAsync(HttpMethod.Post, Site(shopId), owner.Token);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        return (owner, shopId, HostOf(await SlugOf(shopId)));
    }

    protected async Task<PublicationDto> PublishAsync(ShopApiHarness.TestUser owner, Guid shopId, string? note = null)
    {
        var response = await _h.SendJsonAsync(HttpMethod.Post, Publications(shopId), owner.Token, new { note });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PublicationDto>(ShopApiHarness.Json))!;
    }

    protected async Task<(ShopApiHarness.TestUser Owner, Guid ShopId, string Host, PublicationDto Publication)> PublishedShopAsync()
    {
        var (owner, shopId, host) = await OwnerWithWebsiteAsync();
        return (owner, shopId, host, await PublishAsync(owner, shopId));
    }

    protected async Task SqlAsync(FormattableString sql)
    {
        await using var scope = _h.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(sql);
    }

    /// <summary>Ghi thẳng DB (không qua API) nên phải tự xoá con trỏ, như publish/rollback làm.</summary>
    protected async Task InvalidatePointerAsync(Guid shopId)
    {
        await using var scope = _h.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISitePointerCache>().InvalidateAsync(shopId, default);
    }

    protected async Task SetShopStatusAsync(ShopApiHarness.TestUser owner, Guid shopId, ShopStatus status, string? name = null)
    {
        var slug = await SlugOf(shopId);
        var response = await _h.SendJsonAsync(
            HttpMethod.Patch, $"/api/shops/{shopId}", owner.Token,
            new UpdateShopRequest(name ?? "Harness Shop", slug, ShopKind.Hosted, null, status));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
