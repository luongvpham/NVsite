using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Vsite.Application.Website.Dtos;
using Vsite.Domain.Identity;
using Vsite.IntegrationTests.Media;

namespace Vsite.IntegrationTests.Website;

/// <summary>
/// T4, WEBSITE-004 — HTTP thật cho <c>POST /api/shops/{shopId}/website/publications</c> (<c>publishWebsite</c>): pipeline đầy đủ
/// (routing, auth, Owner filter #100, validator, exception → ProblemDetails, Postgres). CẦN Docker.
/// </summary>
[Collection(MediaApiCollection.Name)]
public sealed class PublicationEndpointTests
{
    private readonly ShopApiHarness _h;

    public PublicationEndpointTests(MediaApiFactory factory) => _h = new ShopApiHarness(factory);

    private static string Site(Guid shopId) => $"/api/shops/{shopId}/website";

    private static string Publications(Guid shopId) => Site(shopId) + "/publications";

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task<string> ErrorCode(HttpResponseMessage response) =>
        (await JsonOf(response)).GetProperty("error_code").GetString()!;

    private async Task<(ShopApiHarness.TestUser Owner, Guid ShopId, WebsiteDto Website)> OwnerWithWebsiteAsync()
    {
        var (owner, shopId) = await _h.NewOwnerWithShopAsync();
        var create = await _h.SendAsync(HttpMethod.Post, Site(shopId), owner.Token);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        return (owner, shopId, (await create.Content.ReadFromJsonAsync<WebsiteDto>(ShopApiHarness.Json))!);
    }

    [Fact]
    public async Task Owner_publishes_201_with_Location_then_website_and_pages_reflect_it()
    {
        var (owner, shopId, website) = await OwnerWithWebsiteAsync();
        Assert.Null(website.CurrentPublicationId);

        var response = await _h.SendJsonAsync(HttpMethod.Post, Publications(shopId), owner.Token, new { note = "Bản đầu" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = (await response.Content.ReadFromJsonAsync<PublicationDto>(ShopApiHarness.Json))!;
        Assert.Equal(1, dto.Version);
        Assert.True(dto.IsCurrent);
        Assert.Equal("Bản đầu", dto.Note);
        Assert.Equal(owner.UserId, dto.PublishedByUserId);
        Assert.Equal($"{Publications(shopId)}/{dto.Id}", response.Headers.Location?.OriginalString);

        var get = await _h.SendAsync(HttpMethod.Get, Site(shopId), owner.Token);
        var after = (await get.Content.ReadFromJsonAsync<WebsiteDto>(ShopApiHarness.Json))!;
        Assert.Equal(dto.Id, after.CurrentPublicationId);
        Assert.Equal(dto.Id, (await JsonOf(await _h.SendAsync(HttpMethod.Get, Site(shopId), owner.Token)))
            .GetProperty("currentPublicationId").GetGuid());

        var pages = await _h.SendAsync(HttpMethod.Get, Site(shopId) + "/pages", owner.Token);
        var list = (await pages.Content.ReadFromJsonAsync<List<PageDto>>(ShopApiHarness.Json))!;
        Assert.Equal(3, list.Count);
        Assert.All(list, p => Assert.False(p.IsDirty));
    }

    // ---- T5: list + rollback ----

    private async Task<PublicationDto> PublishAsync(ShopApiHarness.TestUser owner, Guid shopId, string? note = null)
    {
        var response = await _h.SendJsonAsync(HttpMethod.Post, Publications(shopId), owner.Token, new { note });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PublicationDto>(ShopApiHarness.Json))!;
    }

    private static string Rollback(Guid shopId, Guid publicationId) => $"{Publications(shopId)}/{publicationId}/rollback";

    [Fact]
    public async Task List_returns_versions_descending_with_one_current_and_no_snapshot_T5()
    {
        var (owner, shopId, _) = await OwnerWithWebsiteAsync();
        var first = await PublishAsync(owner, shopId, "một");
        await PublishAsync(owner, shopId, "hai");
        var third = await PublishAsync(owner, shopId, "ba");

        var response = await _h.SendAsync(HttpMethod.Get, Publications(shopId), owner.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await JsonOf(response);
        Assert.Equal([3, 2, 1], json.EnumerateArray().Select(e => e.GetProperty("version").GetInt32()));
        Assert.Equal(third.Id, Assert.Single(json.EnumerateArray(), e => e.GetProperty("isCurrent").GetBoolean()).GetProperty("id").GetGuid());
        Assert.All(json.EnumerateArray(), e => Assert.False(e.TryGetProperty("snapshot", out _)));
        Assert.Equal(first.Id, json[2].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task List_and_rollback_without_token_are_401_customer_403_no_website_404_T5()
    {
        var (owner, shopId, _) = await OwnerWithWebsiteAsync();
        var published = await PublishAsync(owner, shopId);
        var customer = await _h.NewMemberAsync(shopId, WellKnownRoles.CustomerId);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _h.SendAsync(HttpMethod.Get, Publications(shopId), token: null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _h.SendAsync(HttpMethod.Post, Rollback(shopId, published.Id), token: null)).StatusCode);

        var listAsCustomer = await _h.SendAsync(HttpMethod.Get, Publications(shopId), customer.Token);
        Assert.Equal(HttpStatusCode.Forbidden, listAsCustomer.StatusCode);
        Assert.Equal("WEBSITE_OWNER_REQUIRED", await ErrorCode(listAsCustomer));
        var rollbackAsCustomer = await _h.SendAsync(HttpMethod.Post, Rollback(shopId, published.Id), customer.Token);
        Assert.Equal(HttpStatusCode.Forbidden, rollbackAsCustomer.StatusCode);

        var (bare, bareShop) = await _h.NewOwnerWithShopAsync();
        var noList = await _h.SendAsync(HttpMethod.Get, Publications(bareShop), bare.Token);
        Assert.Equal(HttpStatusCode.NotFound, noList.StatusCode);
        Assert.Equal("WEBSITE_NOT_FOUND", await ErrorCode(noList));
        var noRollback = await _h.SendAsync(HttpMethod.Post, Rollback(bareShop, Guid.NewGuid()), bare.Token);
        Assert.Equal(HttpStatusCode.NotFound, noRollback.StatusCode);
        Assert.Equal("WEBSITE_NOT_FOUND", await ErrorCode(noRollback));
    }

    [Fact]
    public async Task Rollback_200_repoints_and_marks_pages_dirty_T5()
    {
        var (owner, shopId, _) = await OwnerWithWebsiteAsync();
        var first = await PublishAsync(owner, shopId);
        var second = await PublishAsync(owner, shopId);
        Assert.NotEqual(first.Id, second.Id);

        var response = await _h.SendAsync(HttpMethod.Post, Rollback(shopId, first.Id), owner.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = (await response.Content.ReadFromJsonAsync<PublicationDto>(ShopApiHarness.Json))!;
        Assert.Equal(first.Id, dto.Id);
        Assert.True(dto.IsCurrent);
        var website = (await (await _h.SendAsync(HttpMethod.Get, Site(shopId), owner.Token))
            .Content.ReadFromJsonAsync<WebsiteDto>(ShopApiHarness.Json))!;
        Assert.Equal(first.Id, website.CurrentPublicationId);
        var pages = (await (await _h.SendAsync(HttpMethod.Get, Site(shopId) + "/pages", owner.Token))
            .Content.ReadFromJsonAsync<List<PageDto>>(ShopApiHarness.Json))!;
        Assert.All(pages, p => Assert.True(p.IsDirty));
    }

    [Fact]
    public async Task Rollback_with_publication_of_another_shop_or_random_id_is_404_PUBLICATION_NOT_FOUND_R4_T5()
    {
        var (ownerA, shopA, _) = await OwnerWithWebsiteAsync();
        var (ownerB, shopB, _) = await OwnerWithWebsiteAsync();
        var current = await PublishAsync(ownerA, shopA);
        var foreign = await PublishAsync(ownerB, shopB);

        var cross = await _h.SendAsync(HttpMethod.Post, Rollback(shopA, foreign.Id), ownerA.Token);
        var random = await _h.SendAsync(HttpMethod.Post, Rollback(shopA, Guid.NewGuid()), ownerA.Token);

        Assert.Equal(HttpStatusCode.NotFound, cross.StatusCode);
        Assert.Equal("PUBLICATION_NOT_FOUND", await ErrorCode(cross));
        Assert.Equal(HttpStatusCode.NotFound, random.StatusCode);
        Assert.Equal("PUBLICATION_NOT_FOUND", await ErrorCode(random));
        var website = (await (await _h.SendAsync(HttpMethod.Get, Site(shopA), ownerA.Token))
            .Content.ReadFromJsonAsync<WebsiteDto>(ShopApiHarness.Json))!;
        Assert.Equal(current.Id, website.CurrentPublicationId);
        // B không bị ảnh hưởng.
        var websiteB = (await (await _h.SendAsync(HttpMethod.Get, Site(shopB), ownerB.Token))
            .Content.ReadFromJsonAsync<WebsiteDto>(ShopApiHarness.Json))!;
        Assert.Equal(foreign.Id, websiteB.CurrentPublicationId);
    }

    [Fact]
    public async Task Body_is_optional()
    {
        var (owner, shopId, _) = await OwnerWithWebsiteAsync();

        var response = await _h.SendAsync(HttpMethod.Post, Publications(shopId), owner.Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = (await response.Content.ReadFromJsonAsync<PublicationDto>(ShopApiHarness.Json))!;
        Assert.Null(dto.Note);
    }

    [Fact]
    public async Task Without_token_is_401()
    {
        var (_, shopId, _) = await OwnerWithWebsiteAsync();

        var response = await _h.SendAsync(HttpMethod.Post, Publications(shopId), token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Customer_of_the_shop_is_403_WEBSITE_OWNER_REQUIRED()
    {
        var (_, shopId, _) = await OwnerWithWebsiteAsync();
        var customer = await _h.NewMemberAsync(shopId, WellKnownRoles.CustomerId);

        var response = await _h.SendAsync(HttpMethod.Post, Publications(shopId), customer.Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("WEBSITE_OWNER_REQUIRED", await ErrorCode(response));
    }

    [Fact]
    public async Task Owner_of_another_shop_is_403_and_nothing_is_published()
    {
        var (ownerA, shopA, _) = await OwnerWithWebsiteAsync();
        var (ownerB, _, _) = await OwnerWithWebsiteAsync();

        var response = await _h.SendAsync(HttpMethod.Post, Publications(shopA), ownerB.Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("SHOP_ACCESS_DENIED", await ErrorCode(response));
        var get = await _h.SendAsync(HttpMethod.Get, Site(shopA), ownerA.Token);
        Assert.Null((await get.Content.ReadFromJsonAsync<WebsiteDto>(ShopApiHarness.Json))!.CurrentPublicationId);
    }

    [Fact]
    public async Task Note_of_201_chars_is_422()
    {
        var (owner, shopId, _) = await OwnerWithWebsiteAsync();

        var response = await _h.SendJsonAsync(HttpMethod.Post, Publications(shopId), owner.Token, new { note = new string('a', 201) });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var atLimit = await _h.SendJsonAsync(HttpMethod.Post, Publications(shopId), owner.Token, new { note = new string('a', 200) });
        Assert.Equal(HttpStatusCode.Created, atLimit.StatusCode);
    }

    [Fact]
    public async Task No_website_is_404_WEBSITE_NOT_FOUND()
    {
        var (owner, shopId) = await _h.NewOwnerWithShopAsync();

        var response = await _h.SendAsync(HttpMethod.Post, Publications(shopId), owner.Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("WEBSITE_NOT_FOUND", await ErrorCode(response));
    }

    [Fact]
    public async Task Blocked_publish_is_422_PUBLISH_BLOCKED_with_params_errors_per_page()
    {
        var (owner, shopId, website) = await OwnerWithWebsiteAsync();
        var draft = await JsonOf(await _h.SendAsync(HttpMethod.Get, $"{Site(shopId)}/pages/{website.HomePageId}/draft", owner.Token));
        var tree = WebsiteHandlerKit.TreeWith(
            "{\"id\":\"c_hero1\",\"type\":\"Hero\",\"variant\":\"Hero01\",\"props\":{\"title\":\"Chào\"}}");
        var saved = await _h.SendRawJsonAsync(HttpMethod.Put, $"{Site(shopId)}/pages/{website.HomePageId}/draft", owner.Token,
            $"{{\"tree\":{tree},\"version\":{draft.GetProperty("version").GetInt64()}}}");
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var response = await _h.SendAsync(HttpMethod.Post, Publications(shopId), owner.Token);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var json = await JsonOf(response);
        Assert.Equal("PUBLISH_BLOCKED", json.GetProperty("error_code").GetString());
        var parameters = json.GetProperty("params");
        Assert.False(parameters.GetProperty("truncated").GetBoolean());
        var error = Assert.Single(parameters.GetProperty("errors").EnumerateArray());
        Assert.Equal(website.HomePageId, error.GetProperty("pageId").GetGuid());
        Assert.False(string.IsNullOrEmpty(error.GetProperty("pageTitle").GetString()));
        Assert.Equal("/children/0/props/image", error.GetProperty("path").GetString());
        Assert.Equal("REQUIRED_PROP_MISSING", error.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(error.GetProperty("message").GetString()));
    }
}
