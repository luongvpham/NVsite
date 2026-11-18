using System.Net;
using System.Net.Http.Json;
using Vsite.Application.Website.Dtos;
using Vsite.IntegrationTests.Media;

namespace Vsite.IntegrationTests.Website;

/// <summary>
/// T8, WEBSITE-004 (R11, vế "references trước khi xoá đã cảnh báo") — đầu-cuối qua <c>GET .../media/library/{assetId}/references</c>:
/// ảnh đã publish vẫn báo <c>SitePublication</c> kể cả sau khi bản Library bị xoá khỏi thư viện (xoá library KHÔNG kéo theo clone, nên
/// ảnh trong snapshot sống tới prune 8d). Dữ liệu dựng bằng API thật. CẦN Docker.
/// </summary>
[Collection(MediaApiCollection.Name)]
public sealed class SiteReferencesEndpointTests(MediaApiFactory factory) : PublishedSiteTestBase(factory)
{
    private string References(Guid shopId, Guid assetId) => $"/api/shops/{shopId}/media/library/{assetId}/references";

    private async Task SaveHomeAsync(ShopApiHarness.TestUser owner, Guid shopId, string treeJson)
    {
        var pages = await (await _h.SendAsync(HttpMethod.Get, Site(shopId) + "/pages", owner.Token))
            .Content.ReadFromJsonAsync<List<PageDto>>(ShopApiHarness.Json);
        var home = pages!.Single(p => p.Slug == "");
        var draft = await JsonOf(await _h.SendAsync(HttpMethod.Get, $"{Site(shopId)}/pages/{home.Id}/draft", owner.Token));
        var saved = await _h.SendRawJsonAsync(
            HttpMethod.Put, $"{Site(shopId)}/pages/{home.Id}/draft", owner.Token,
            $"{{\"tree\":{treeJson},\"version\":{draft.GetProperty("version").GetInt64()}}}");
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
    }

    private async Task<System.Text.Json.JsonElement> ReferencesOf(ShopApiHarness.TestUser owner, Guid shopId, Guid assetId)
    {
        var response = await _h.SendAsync(HttpMethod.Get, References(shopId, assetId), owner.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await JsonOf(response)).GetProperty("references");
    }

    [Fact]
    public async Task Published_image_is_reported_as_SitePublication_and_survives_deleting_the_library_copy_R11()
    {
        var (owner, shopId, _) = await OwnerWithWebsiteAsync();
        var library = await _h.UploadLibraryAsync(owner.Token, shopId);
        var clone = await _h.CloneAsync(owner.Token, shopId, library.Id);
        await SaveHomeAsync(owner, shopId, WebsiteHandlerKit.TreeWith(WebsiteHandlerKit.Hero("c_hero1", clone.Id.ToString())));
        await PublishAsync(owner, shopId);

        var before = await ReferencesOf(owner, shopId, library.Id);
        var publication = Assert.Single(before.EnumerateArray(), r => r.GetProperty("kind").GetString() == "SitePublication");
        Assert.True(publication.GetProperty("isCurrent").GetBoolean());
        Assert.Contains(before.EnumerateArray(), r => r.GetProperty("kind").GetString() == "PageDraft");

        // Xoá bản Library: clone không bị kéo theo, ảnh vẫn nằm trong snapshot đang chạy → cảnh báo phải còn nguyên.
        var delete = await _h.SendAsync(HttpMethod.Delete, $"/api/shops/{shopId}/media/library/{library.Id}", owner.Token);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var after = await ReferencesOf(owner, shopId, library.Id);
        var still = Assert.Single(after.EnumerateArray(), r => r.GetProperty("kind").GetString() == "SitePublication");
        Assert.True(still.GetProperty("isCurrent").GetBoolean());
    }

    [Fact]
    public async Task Another_shops_owner_sees_no_references_to_the_image_of_a_published_site()
    {
        var (owner, shopId, _) = await OwnerWithWebsiteAsync();
        var library = await _h.UploadLibraryAsync(owner.Token, shopId);
        var clone = await _h.CloneAsync(owner.Token, shopId, library.Id);
        await SaveHomeAsync(owner, shopId, WebsiteHandlerKit.TreeWith(WebsiteHandlerKit.Hero("c_hero1", clone.Id.ToString())));
        await PublishAsync(owner, shopId);
        var (other, otherShopId, _) = await OwnerWithWebsiteAsync();

        var references = await ReferencesOf(other, otherShopId, library.Id);

        Assert.Equal(0, references.GetArrayLength());
    }
}
