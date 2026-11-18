using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Vsite.Api.Shop;
using Vsite.Application.Website.Dtos;
using Vsite.Application.Website.Interfaces;
using Vsite.Domain.Shop.Enums;
using Vsite.Domain.Website.Enums;
using Vsite.Infrastructure.Persistence;
using Vsite.IntegrationTests.Media;

namespace Vsite.IntegrationTests.Website;

/// <summary>
/// T6, WEBSITE-004 — HTTP thật cho <c>GET /api/site</c> (<c>getPublishedSite</c>, #108/#109): Host của shop suy ra tenant,
/// không token, ETag + 304, con trỏ Redis thật (R2, R5, R6, R12). CẦN Docker (Postgres + Redis Testcontainers).
/// </summary>
[Collection(MediaApiCollection.Name)]
public sealed class PublishedSiteEndpointTests
{
    private readonly ShopApiHarness _h;

    public PublishedSiteEndpointTests(MediaApiFactory factory) => _h = new ShopApiHarness(factory);

    private static string Site(Guid shopId) => $"/api/shops/{shopId}/website";

    private static string Publications(Guid shopId) => Site(shopId) + "/publications";

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task<string> ErrorCode(HttpResponseMessage response) =>
        (await JsonOf(response)).GetProperty("error_code").GetString()!;

    private async Task<string> SlugOf(Guid shopId)
    {
        await using var scope = _h.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database.SqlQuery<string>($"SELECT slug AS \"Value\" FROM shop WHERE id = {shopId}").FirstAsync();
    }

    private static string HostOf(string slug) => $"{slug}.vsite.local";

    private async Task<HttpResponseMessage> GetSiteAsync(
        string host, string? ifNoneMatch = null, string? token = null, string path = "/api/site")
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

        return await _h.Client.SendAsync(request);
    }

    private async Task<(ShopApiHarness.TestUser Owner, Guid ShopId, string Host)> OwnerWithWebsiteAsync()
    {
        var (owner, shopId) = await _h.NewOwnerWithShopAsync();
        var create = await _h.SendAsync(HttpMethod.Post, Site(shopId), owner.Token);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        return (owner, shopId, HostOf(await SlugOf(shopId)));
    }

    private async Task<PublicationDto> PublishAsync(ShopApiHarness.TestUser owner, Guid shopId, string? note = null)
    {
        var response = await _h.SendJsonAsync(HttpMethod.Post, Publications(shopId), owner.Token, new { note });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PublicationDto>(ShopApiHarness.Json))!;
    }

    private async Task<(ShopApiHarness.TestUser Owner, Guid ShopId, string Host, PublicationDto Publication)> PublishedShopAsync()
    {
        var (owner, shopId, host) = await OwnerWithWebsiteAsync();
        return (owner, shopId, host, await PublishAsync(owner, shopId));
    }

    private async Task SqlAsync(FormattableString sql)
    {
        await using var scope = _h.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(sql);
    }

    /// <summary>Ghi thẳng DB (không qua API) nên phải tự xoá con trỏ, như publish/rollback làm.</summary>
    private async Task InvalidatePointerAsync(Guid shopId)
    {
        await using var scope = _h.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISitePointerCache>().InvalidateAsync(shopId, default);
    }

    private async Task SetShopStatusAsync(ShopApiHarness.TestUser owner, Guid shopId, ShopStatus status, string? name = null)
    {
        var slug = await SlugOf(shopId);
        var response = await _h.SendJsonAsync(
            HttpMethod.Patch, $"/api/shops/{shopId}", owner.Token,
            new UpdateShopRequest(name ?? "Harness Shop", slug, ShopKind.Hosted, null, status));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Published_site_is_200_with_ETag_no_cache_and_anonymous_T6()
    {
        var (_, shopId, host, publication) = await PublishedShopAsync();

        var response = await GetSiteAsync(host);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
        Assert.NotNull(response.Headers.ETag);
        Assert.StartsWith($"\"{publication.Id:N}-", response.Headers.ETag!.Tag);
        var json = await JsonOf(response);
        Assert.Equal("Published", json.GetProperty("status").GetString());
        Assert.Equal("Harness Shop", json.GetProperty("shop").GetProperty("name").GetString());
        var pub = json.GetProperty("publication");
        Assert.Equal(publication.Id, pub.GetProperty("id").GetGuid());
        Assert.Equal(1, pub.GetProperty("version").GetInt32());
        Assert.Equal(1, pub.GetProperty("snapshot").GetProperty("schemaVersion").GetInt32());
        Assert.Equal(3, pub.GetProperty("snapshot").GetProperty("pages").GetArrayLength());
        Assert.NotEqual(Guid.Empty, shopId);
    }

    [Fact]
    public async Task Shop_in_Draft_status_with_published_website_is_still_200_T6()
    {
        var (_, shopId, host, _) = await PublishedShopAsync();
        await using var scope = _h.Factory.Services.CreateAsyncScope();
        var status = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database
            .SqlQuery<string>($"SELECT status AS \"Value\" FROM shop WHERE id = {shopId}").FirstAsync();
        Assert.Equal("Draft", status);

        Assert.Equal(HttpStatusCode.OK, (await GetSiteAsync(host)).StatusCode);
    }

    [Fact]
    public async Task If_None_Match_equal_is_304_without_body_and_other_etag_is_200_T6()
    {
        var (_, _, host, _) = await PublishedShopAsync();
        var etag = (await GetSiteAsync(host)).Headers.ETag!.Tag;

        var notModified = await GetSiteAsync(host, etag);
        Assert.Equal(HttpStatusCode.NotModified, notModified.StatusCode);
        Assert.Equal(string.Empty, await notModified.Content.ReadAsStringAsync());
        Assert.Equal(etag, notModified.Headers.ETag?.Tag);

        Assert.Equal(HttpStatusCode.NotModified, (await GetSiteAsync(host, "W/" + etag)).StatusCode);
        Assert.Equal(HttpStatusCode.NotModified, (await GetSiteAsync(host, $"\"zzz\", {etag}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotModified, (await GetSiteAsync(host, "*")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await GetSiteAsync(host, "\"other\"")).StatusCode);
    }

    [Fact]
    public async Task Non_shop_hosts_and_unknown_slug_are_404_SITE_NOT_FOUND_R2()
    {
        foreach (var host in new[] { "admin.vsite.local", "vsite.local", "www.vsite.local", $"nope-{Guid.NewGuid():N}.vsite.local" })
        {
            var response = await GetSiteAsync(host);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("SITE_NOT_FOUND", await ErrorCode(response));
        }
    }

    [Fact]
    public async Task Shop_without_website_or_with_draft_website_is_404_T6()
    {
        var (_, _, bareHost) = await OwnerWithWebsiteAsync();
        // website Draft chưa publish
        var draft = await GetSiteAsync(bareHost);
        Assert.Equal(HttpStatusCode.NotFound, draft.StatusCode);
        Assert.Equal("SITE_NOT_FOUND", await ErrorCode(draft));

        // shop chưa có website
        var (_, noWebsiteShop) = await _h.NewOwnerWithShopAsync();
        var none = await GetSiteAsync(HostOf(await SlugOf(noWebsiteShop)));
        Assert.Equal(HttpStatusCode.NotFound, none.StatusCode);
        Assert.Equal("SITE_NOT_FOUND", await ErrorCode(none));
    }

    [Fact]
    public async Task Draft_website_with_a_manually_inserted_publication_is_404_R6()
    {
        var (_, shopId, host) = await OwnerWithWebsiteAsync();
        var publicationId = Guid.NewGuid();
        const string snapshot = """{"schemaVersion":1}""";
        await SqlAsync($"""
            INSERT INTO site_publication (id, website_id, shop_id, version, snapshot, note, published_at)
            SELECT {publicationId}, w.id, w.shop_id, 1, {snapshot}::jsonb, NULL, now()
            FROM website w WHERE w.shop_id = {shopId}
            """);
        await SqlAsync($"UPDATE website SET current_publication_id = {publicationId} WHERE shop_id = {shopId}");
        await InvalidatePointerAsync(shopId);

        var response = await GetSiteAsync(host);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("SITE_NOT_FOUND", await ErrorCode(response));
    }

    [Fact]
    public async Task Suspended_website_is_200_with_null_publication_and_no_ETag_T6()
    {
        var (_, shopId, host, _) = await PublishedShopAsync();
        await SqlAsync($"UPDATE website SET status = 'Suspended' WHERE shop_id = {shopId}");
        await InvalidatePointerAsync(shopId);

        var response = await GetSiteAsync(host);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.ETag);
        var json = await JsonOf(response);
        Assert.Equal("Suspended", json.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("publication").ValueKind);
        Assert.Equal("Harness Shop", json.GetProperty("shop").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Suspended_shop_is_200_Suspended_and_Closed_shop_is_404_taking_effect_immediately_T6()
    {
        var (owner, shopId, host, _) = await PublishedShopAsync();
        Assert.Equal(HttpStatusCode.OK, (await GetSiteAsync(host)).StatusCode); // làm nóng con trỏ cache

        await SetShopStatusAsync(owner, shopId, ShopStatus.Suspended);
        var suspended = await GetSiteAsync(host);
        Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);
        Assert.Null(suspended.Headers.ETag);
        var json = await JsonOf(suspended);
        Assert.Equal("Suspended", json.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("publication").ValueKind);

        await SetShopStatusAsync(owner, shopId, ShopStatus.Closed);
        var closed = await GetSiteAsync(host);
        Assert.Equal(HttpStatusCode.NotFound, closed.StatusCode);
        Assert.Equal("SITE_NOT_FOUND", await ErrorCode(closed));

        await SetShopStatusAsync(owner, shopId, ShopStatus.Active);
        Assert.Equal(HttpStatusCode.OK, (await GetSiteAsync(host)).StatusCode);
    }

    [Fact]
    public async Task Second_publish_and_rollback_are_visible_immediately_R5()
    {
        var (owner, shopId, host, first) = await PublishedShopAsync();
        var v1 = await JsonOf(await GetSiteAsync(host)); // làm nóng con trỏ
        Assert.Equal(1, v1.GetProperty("publication").GetProperty("version").GetInt32());

        var second = await PublishAsync(owner, shopId);
        var v2 = await JsonOf(await GetSiteAsync(host));
        Assert.Equal(2, v2.GetProperty("publication").GetProperty("version").GetInt32());
        Assert.Equal(second.Id, v2.GetProperty("publication").GetProperty("id").GetGuid());

        var rollback = await _h.SendAsync(HttpMethod.Post, $"{Publications(shopId)}/{first.Id}/rollback", owner.Token);
        Assert.Equal(HttpStatusCode.OK, rollback.StatusCode);
        var back = await JsonOf(await GetSiteAsync(host));
        Assert.Equal(1, back.GetProperty("publication").GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task Renaming_the_shop_changes_ETag_and_name_but_not_the_snapshot_R12()
    {
        var (owner, shopId, host, _) = await PublishedShopAsync();
        var before = await GetSiteAsync(host);
        var beforeEtag = before.Headers.ETag!.Tag;
        var beforeSnapshot = (await JsonOf(before)).GetProperty("publication").GetProperty("snapshot").GetRawText();

        await SetShopStatusAsync(owner, shopId, ShopStatus.Draft, name: "Tên mới");

        var after = await GetSiteAsync(host, beforeEtag);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode); // ETag cũ không còn khớp
        Assert.NotEqual(beforeEtag, after.Headers.ETag!.Tag);
        var json = await JsonOf(after);
        Assert.Equal("Tên mới", json.GetProperty("shop").GetProperty("name").GetString());
        Assert.Equal(beforeSnapshot, json.GetProperty("publication").GetProperty("snapshot").GetRawText());
    }

    [Fact]
    public async Task Token_does_not_change_the_result_and_identifier_query_params_are_ignored_T6()
    {
        var (owner, _, host, _) = await PublishedShopAsync();
        var (_, _, otherHost, _) = await PublishedShopAsync();

        var anonymous = await (await GetSiteAsync(host)).Content.ReadAsStringAsync();
        var withToken = await (await GetSiteAsync(host, token: owner.Token)).Content.ReadAsStringAsync();
        var withQuery = await (await GetSiteAsync(
            host, path: $"/api/site?host={Uri.EscapeDataString(otherHost)}&shopId={Guid.NewGuid()}")).Content.ReadAsStringAsync();

        Assert.Equal(anonymous, withToken);
        Assert.Equal(anonymous, withQuery);
    }

    [Fact]
    public async Task Stale_pointer_to_a_missing_publication_self_heals_from_the_database_T6()
    {
        var (_, shopId, host, publication) = await PublishedShopAsync();
        await using (var scope = _h.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISitePointerCache>()
                .SetAsync(shopId, new SitePointer(WebsiteStatus.Published, Guid.NewGuid()), default);
        }

        var response = await GetSiteAsync(host);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(publication.Id, (await JsonOf(response)).GetProperty("publication").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Corrupt_pointer_value_in_Redis_is_treated_as_a_miss_and_endpoint_still_200_I3()
    {
        var (_, shopId, host, publication) = await PublishedShopAsync();
        var cache = _h.Factory.Services.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
        await cache.SetStringAsync($"site:pointer:{shopId:N}", "{not json");

        var response = await GetSiteAsync(host);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(publication.Id, (await JsonOf(response)).GetProperty("publication").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Stale_pointer_to_an_existing_older_version_is_served_until_invalidated_known_limit_I1()
    {
        // Khoá hành vi đã chấp nhận (changelog T6 "Theo dõi"): con trỏ cũ trỏ bản còn tồn tại sống tới TTL; Invalidate sửa ngay.
        var (owner, shopId, host, first) = await PublishedShopAsync();
        await GetSiteAsync(host);
        var second = await PublishAsync(owner, shopId);
        await using var scope = _h.Factory.Services.CreateAsyncScope();
        var cache = scope.ServiceProvider.GetRequiredService<ISitePointerCache>();
        await cache.SetAsync(shopId, new SitePointer(WebsiteStatus.Published, first.Id), default);

        var stale = await JsonOf(await GetSiteAsync(host));
        Assert.Equal(first.Id, stale.GetProperty("publication").GetProperty("id").GetGuid());

        await cache.InvalidateAsync(shopId, default);
        var fresh = await JsonOf(await GetSiteAsync(host));
        Assert.Equal(second.Id, fresh.GetProperty("publication").GetProperty("id").GetGuid());
    }
}
