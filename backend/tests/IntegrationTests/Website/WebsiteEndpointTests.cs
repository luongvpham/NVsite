using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vsite.Api.Shop;
using Vsite.Application.Website.Dtos;
using Vsite.Domain.Identity;
using Vsite.Domain.Shop.Enums;
using Vsite.Infrastructure.Persistence;
using Vsite.IntegrationTests.Media;
using static Vsite.IntegrationTests.Website.WebsiteHandlerKit;

namespace Vsite.IntegrationTests.Website;

/// <summary>
/// T10, WEBSITE-001 — test HTTP thật (pipeline đầy đủ: routing, auth, ShopMembership/Owner filter, validator, Postgres) cho 10 endpoint
/// <c>/api/shops/{shopId}/website…</c>. CẦN Docker (Testcontainers). Khung user/shop/membership ở <see cref="ShopApiHarness"/>.
/// Đóng MEDIA-001 #1 (test request-level `08` §9 test 6/7 — id Library/clone shop khác/đã xoá trong tree → 422).
/// </summary>
[Collection(MediaApiCollection.Name)]
public sealed class WebsiteEndpointTests
{
    private readonly MediaApiFactory _factory;
    private readonly ShopApiHarness _h;

    public WebsiteEndpointTests(MediaApiFactory factory)
    {
        _factory = factory;
        _h = new ShopApiHarness(factory);
    }

    // ---------- helpers ----------

    private static string Site(Guid shopId) => $"/api/shops/{shopId}/website";

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static async Task<string> ErrorCode(HttpResponseMessage response) =>
        (await JsonOf(response)).GetProperty("error_code").GetString()!;

    private async Task<(ShopApiHarness.TestUser Owner, Guid ShopId, WebsiteDto Website)> OwnerWithWebsiteAsync()
    {
        var (owner, shopId) = await _h.NewOwnerWithShopAsync();
        var create = await _h.SendAsync(HttpMethod.Post, Site(shopId), owner.Token);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var website = (await create.Content.ReadFromJsonAsync<WebsiteDto>(ShopApiHarness.Json))!;
        return (owner, shopId, website);
    }

    private async Task<IReadOnlyList<PageDto>> ListPagesAsync(string token, Guid shopId)
    {
        var response = await _h.SendAsync(HttpMethod.Get, Site(shopId) + "/pages", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<PageDto>>(ShopApiHarness.Json))!;
    }

    private async Task<(string TreeJson, long Version)> GetDraftAsync(string token, Guid shopId, Guid pageId)
    {
        var response = await _h.SendAsync(HttpMethod.Get, $"{Site(shopId)}/pages/{pageId}/draft", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await JsonOf(response);
        return (json.GetProperty("tree").GetRawText(), json.GetProperty("version").GetInt64());
    }

    private Task<HttpResponseMessage> SaveDraftAsync(string token, Guid shopId, Guid pageId, string treeJson, long version) =>
        _h.SendRawJsonAsync(HttpMethod.Put, $"{Site(shopId)}/pages/{pageId}/draft", token, $"{{\"tree\":{treeJson},\"version\":{version}}}");

    private async Task SoftDeleteAssetAsync(Guid assetId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE media_asset SET is_deleted = true WHERE id = {assetId}"));
    }

    // ---------- đường vui ----------

    [Fact]
    public async Task Owner_full_flow_create_website_pages_draft_theme()
    {
        var (owner, shopId, website) = await OwnerWithWebsiteAsync();
        Assert.NotEqual(Guid.Empty, website.Id);

        var get = await _h.SendAsync(HttpMethod.Get, Site(shopId), owner.Token);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var pages = await ListPagesAsync(owner.Token, shopId);
        Assert.Equal(3, pages.Count);
        Assert.Equal(website.HomePageId, pages.Single(p => p.Slug == "").Id);

        var created = await _h.SendJsonAsync(HttpMethod.Post, Site(shopId) + "/pages", owner.Token,
            new { slug = "gioi-thieu", title = "Giới thiệu" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var page = (await created.Content.ReadFromJsonAsync<PageDto>(ShopApiHarness.Json))!;
        Assert.Equal("gioi-thieu", page.Slug);

        var updated = await _h.SendJsonAsync(HttpMethod.Put, $"{Site(shopId)}/pages/{page.Id}", owner.Token,
            new { slug = (string?)null, title = "Về chúng tôi", seoTitle = "SEO", noIndex = false, isEnabled = true });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var updatedPage = (await updated.Content.ReadFromJsonAsync<PageDto>(ShopApiHarness.Json))!;
        Assert.Equal("Về chúng tôi", updatedPage.Title);
        Assert.Equal("gioi-thieu", updatedPage.Slug);

        var draft = await GetDraftAsync(owner.Token, shopId, page.Id);
        var saved = await SaveDraftAsync(owner.Token, shopId, page.Id, TreeWith(RichText("c_abc12", "<p>Xin chào</p>")), draft.Version);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var savedJson = await JsonOf(saved);
        Assert.NotEqual(draft.Version, savedJson.GetProperty("version").GetInt64());

        var theme = await _h.SendAsync(HttpMethod.Get, Site(shopId) + "/theme", owner.Token);
        Assert.Equal(HttpStatusCode.OK, theme.StatusCode);
        var themeJson = await JsonOf(theme);
        var put = await _h.SendRawJsonAsync(HttpMethod.Put, Site(shopId) + "/theme", owner.Token,
            $"{{\"presetKey\":null,\"tokens\":{themeJson.GetProperty("tokens").GetRawText()}}}");
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var delete = await _h.SendAsync(HttpMethod.Delete, $"{Site(shopId)}/pages/{page.Id}", owner.Token);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _h.SendAsync(HttpMethod.Get, $"{Site(shopId)}/pages/{page.Id}/draft", owner.Token)).StatusCode);
    }

    // ---------- 401 / 403 (R16, #100) trên MỌI endpoint ----------

    private static IEnumerable<(string Name, HttpMethod Method, Func<Guid, Guid, string> Path, Func<string?> Body)> AllEndpoints() =>
    [
        ("createWebsite", HttpMethod.Post, (s, _) => Site(s), () => null),
        ("getWebsite", HttpMethod.Get, (s, _) => Site(s), () => null),
        ("listPages", HttpMethod.Get, (s, _) => Site(s) + "/pages", () => null),
        ("createPage", HttpMethod.Post, (s, _) => Site(s) + "/pages", () => """{"slug":"x-page","title":"X"}"""),
        ("updatePage", HttpMethod.Put, (s, p) => $"{Site(s)}/pages/{p}", () => """{"title":"X","noIndex":false,"isEnabled":true}"""),
        ("deletePage", HttpMethod.Delete, (s, p) => $"{Site(s)}/pages/{p}", () => null),
        ("getPageDraft", HttpMethod.Get, (s, p) => $"{Site(s)}/pages/{p}/draft", () => null),
        ("savePageDraft", HttpMethod.Put, (s, p) => $"{Site(s)}/pages/{p}/draft", () => """{"tree":{},"version":1}"""),
        ("getTheme", HttpMethod.Get, (s, _) => Site(s) + "/theme", () => null),
        ("updateTheme", HttpMethod.Put, (s, _) => Site(s) + "/theme", () => """{"tokens":{}}"""),
    ];

    private Task<HttpResponseMessage> SendEndpointAsync(
        (string Name, HttpMethod Method, Func<Guid, Guid, string> Path, Func<string?> Body) e, Guid shopId, Guid pageId, string? token)
    {
        var body = e.Body();
        return body is null
            ? _h.SendAsync(e.Method, e.Path(shopId, pageId), token)
            : _h.SendRawJsonAsync(e.Method, e.Path(shopId, pageId), token, body);
    }

    [Fact]
    public async Task Every_endpoint_without_token_is_401()
    {
        var (_, shopId, website) = await OwnerWithWebsiteAsync();
        foreach (var e in AllEndpoints())
        {
            var response = await SendEndpointAsync(e, shopId, website.HomePageId, token: null);
            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{e.Name}: {(int)response.StatusCode}");
        }
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("Manager")]
    [InlineData("Staff")]
    [InlineData("Accountant")]
    public async Task Every_endpoint_rejects_non_owner_member_with_global_token_403_WEBSITE_OWNER_REQUIRED(string role)
    {
        var (owner, shopId, website) = await OwnerWithWebsiteAsync();
        var roleId = role switch
        {
            "Customer" => WellKnownRoles.CustomerId,
            "Manager" => WellKnownRoles.ManagerId,
            "Staff" => WellKnownRoles.StaffId,
            _ => WellKnownRoles.AccountantId,
        };
        var member = await _h.NewMemberAsync(shopId, roleId);

        foreach (var e in AllEndpoints())
        {
            var response = await SendEndpointAsync(e, shopId, website.HomePageId, member.Token);
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{role} {e.Name}: {(int)response.StatusCode}");
            Assert.Equal("WEBSITE_OWNER_REQUIRED", await ErrorCode(response));
        }

        // Owner vẫn đọc được — filter không khoá nhầm.
        Assert.Equal(HttpStatusCode.OK, (await _h.SendAsync(HttpMethod.Get, Site(shopId), owner.Token)).StatusCode);
    }

    [Fact]
    public async Task Every_endpoint_rejects_user_without_membership_403_SHOP_ACCESS_DENIED()
    {
        var (_, shopId, website) = await OwnerWithWebsiteAsync();
        var outsider = await _h.NewUserAsync();

        foreach (var e in AllEndpoints())
        {
            var response = await SendEndpointAsync(e, shopId, website.HomePageId, outsider.Token);
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{e.Name}: {(int)response.StatusCode}");
            Assert.Equal("SHOP_ACCESS_DENIED", await ErrorCode(response));
        }
    }

    // ---------- 404 ----------

    [Fact]
    public async Task Reads_without_a_website_are_404_WEBSITE_NOT_FOUND()
    {
        var (owner, shopId) = await _h.NewOwnerWithShopAsync();

        foreach (var path in new[] { Site(shopId), Site(shopId) + "/theme" })
        {
            var response = await _h.SendAsync(HttpMethod.Get, path, owner.Token);
            Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{path}: {(int)response.StatusCode}");
            Assert.Equal("WEBSITE_NOT_FOUND", await ErrorCode(response));
        }

        // listPages không có website thì là danh sách rỗng (hành vi handler T8), không phải 404.
        Assert.Empty(await ListPagesAsync(owner.Token, shopId));
    }

    [Fact]
    public async Task Unknown_page_is_404_PAGE_NOT_FOUND_on_every_page_endpoint()
    {
        var (owner, shopId, _) = await OwnerWithWebsiteAsync();
        var unknown = Guid.NewGuid();

        foreach (var e in AllEndpoints().Where(e => e.Name is "updatePage" or "deletePage" or "getPageDraft" or "savePageDraft"))
        {
            var response = await SendEndpointAsync(e, shopId, unknown, owner.Token);
            Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{e.Name}: {(int)response.StatusCode}");
            Assert.Equal("PAGE_NOT_FOUND", await ErrorCode(response));
        }
    }

    // ---------- R2: tenant isolation qua HTTP ----------

    [Fact]
    public async Task Page_of_shop_B_through_shop_A_route_is_404_and_untouched()
    {
        var (ownerA, shopA, _) = await OwnerWithWebsiteAsync();
        var (ownerB, shopB, websiteB) = await OwnerWithWebsiteAsync();
        var pageB = websiteB.HomePageId;
        var draftB = await GetDraftAsync(ownerB.Token, shopB, pageB);

        var attempts = new[]
        {
            await _h.SendAsync(HttpMethod.Get, $"{Site(shopA)}/pages/{pageB}/draft", ownerA.Token),
            await SaveDraftAsync(ownerA.Token, shopA, pageB, TreeWith(RichText("c_abc12", "<p>hack</p>")), draftB.Version),
            await _h.SendRawJsonAsync(HttpMethod.Put, $"{Site(shopA)}/pages/{pageB}", ownerA.Token,
                """{"title":"hack","noIndex":false,"isEnabled":true}"""),
            await _h.SendAsync(HttpMethod.Delete, $"{Site(shopA)}/pages/{pageB}", ownerA.Token),
        };
        Assert.All(attempts, r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
        foreach (var r in attempts)
        {
            Assert.Equal("PAGE_NOT_FOUND", await ErrorCode(r));
        }

        // Và owner A không dùng được route shop B (không phải thành viên).
        Assert.Equal(HttpStatusCode.Forbidden, (await _h.SendAsync(HttpMethod.Get, $"{Site(shopB)}/pages/{pageB}/draft", ownerA.Token)).StatusCode);

        var after = await GetDraftAsync(ownerB.Token, shopB, pageB);
        Assert.Equal(draftB.Version, after.Version);
        Assert.Contains(await ListPagesAsync(ownerB.Token, shopB), p => p.Id == pageB);
    }

    // ---------- 409 ----------

    [Fact]
    public async Task Conflicts_website_exists_slug_taken_system_page_not_deletable()
    {
        var (owner, shopId, _) = await OwnerWithWebsiteAsync();

        var again = await _h.SendAsync(HttpMethod.Post, Site(shopId), owner.Token);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("WEBSITE_ALREADY_EXISTS", await ErrorCode(again));

        var first = await _h.SendRawJsonAsync(HttpMethod.Post, Site(shopId) + "/pages", owner.Token, """{"slug":"bang-gia","title":"Giá"}""");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var second = await _h.SendRawJsonAsync(HttpMethod.Post, Site(shopId) + "/pages", owner.Token, """{"slug":"bang-gia","title":"Giá 2"}""");
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("PAGE_SLUG_TAKEN", await ErrorCode(second));

        var pages = await ListPagesAsync(owner.Token, shopId);
        var contact = pages.Single(p => p.SystemType == Vsite.Domain.Website.Enums.SystemType.Contact);
        var delete = await _h.SendAsync(HttpMethod.Delete, $"{Site(shopId)}/pages/{contact.Id}", owner.Token);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        Assert.Equal("PAGE_NOT_DELETABLE", await ErrorCode(delete));
    }

    // R7: hai tab qua HTTP.
    [Fact]
    public async Task Two_tabs_second_save_with_stale_version_is_409_DRAFT_CONFLICT_with_currentVersion()
    {
        var (owner, shopId, website) = await OwnerWithWebsiteAsync();
        var home = website.HomePageId;
        var opened = await GetDraftAsync(owner.Token, shopId, home); // cả hai tab cùng mở ở version này

        var tabA = await SaveDraftAsync(owner.Token, shopId, home, TreeWith(RichText("c_abc12", "<p>A</p>")), opened.Version);
        Assert.Equal(HttpStatusCode.OK, tabA.StatusCode);
        var versionA = (await JsonOf(tabA)).GetProperty("version").GetInt64();

        var tabB = await SaveDraftAsync(owner.Token, shopId, home, TreeWith(RichText("c_abc12", "<p>B</p>")), opened.Version);

        Assert.Equal(HttpStatusCode.Conflict, tabB.StatusCode);
        var problem = await JsonOf(tabB);
        Assert.Equal("DRAFT_CONFLICT", problem.GetProperty("error_code").GetString());
        Assert.Equal(versionA, problem.GetProperty("params").GetProperty("currentVersion").GetInt64());
        Assert.Contains("<p>A</p>", (await GetDraftAsync(owner.Token, shopId, home)).TreeJson);
    }

    // ---------- 422 ----------

    [Fact]
    public async Task Validation_errors_are_422_with_problem_codes()
    {
        var (owner, shopId, website) = await OwnerWithWebsiteAsync();

        var reserved = await _h.SendRawJsonAsync(HttpMethod.Post, Site(shopId) + "/pages", owner.Token, """{"slug":"en","title":"EN"}""");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reserved.StatusCode);
        Assert.Equal("PAGE_SLUG_RESERVED", await ErrorCode(reserved));

        var emptyTitle = await _h.SendRawJsonAsync(HttpMethod.Put, $"{Site(shopId)}/pages/{website.HomePageId}", owner.Token,
            """{"title":"","noIndex":false,"isEnabled":true}""");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, emptyTitle.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await ErrorCode(emptyTitle));

        var badTheme = await _h.SendRawJsonAsync(HttpMethod.Put, Site(shopId) + "/theme", owner.Token, """{"tokens":{"schemaVersion":1,"colors":{}}}""");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badTheme.StatusCode);
        Assert.Equal("THEME_INVALID", await ErrorCode(badTheme));

        var notObject = await _h.SendRawJsonAsync(HttpMethod.Put, $"{Site(shopId)}/pages/{website.HomePageId}/draft", owner.Token, "[1,2]");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, notObject.StatusCode);
        var noVersion = await _h.SendRawJsonAsync(HttpMethod.Put, $"{Site(shopId)}/pages/{website.HomePageId}/draft", owner.Token, """{"tree":{}}""");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noVersion.StatusCode);
    }

    [Fact]
    public async Task CreateWebsite_for_an_ExternalOnly_shop_is_422_WEBSITE_REQUIRES_HOSTED_SHOP()
    {
        var owner = await _h.NewUserAsync();
        var created = await _h.PostJsonAsync("/api/shops", owner.Token,
            new CreateShopRequest("Ext", $"ext-{Guid.NewGuid():N}", ShopKind.ExternalOnly, "https://example.vn"));
        var shop = (await created.Content.ReadFromJsonAsync<Vsite.Application.Shop.Dtos.ShopDto>(ShopApiHarness.Json))!;

        var response = await _h.SendAsync(HttpMethod.Post, Site(shop.Id), owner.Token);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("WEBSITE_REQUIRES_HOSTED_SHOP", await ErrorCode(response));
    }

    [Fact]
    public async Task Unsafe_richtext_is_422_TREE_INVALID_with_RICHTEXT_UNSAFE_in_params_errors()
    {
        var (owner, shopId, website) = await OwnerWithWebsiteAsync();
        var draft = await GetDraftAsync(owner.Token, shopId, website.HomePageId);

        var response = await SaveDraftAsync(owner.Token, shopId, website.HomePageId,
            TreeWith(RichText("c_abc12", "<p>a</p><script>alert(1)</script>")), draft.Version);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await JsonOf(response);
        Assert.Equal("TREE_INVALID", problem.GetProperty("error_code").GetString());
        var errors = problem.GetProperty("params").GetProperty("errors").EnumerateArray().ToList();
        Assert.Contains(errors, e => e.GetProperty("code").GetString() == "RICHTEXT_UNSAFE");
        Assert.Equal(draft.Version, (await GetDraftAsync(owner.Token, shopId, website.HomePageId)).Version);
    }

    [Fact]
    public async Task Binding_to_Review_is_422_BINDING_SOURCE_NOT_ALLOWED()
    {
        var (owner, shopId, website) = await OwnerWithWebsiteAsync();
        var draft = await GetDraftAsync(owner.Token, shopId, website.HomePageId);
        var tree = TreeFixtures.Load("invalid-binding-review.json")["tree"]!.ToJsonString();

        var response = await SaveDraftAsync(owner.Token, shopId, website.HomePageId, tree, draft.Version);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await JsonOf(response);
        Assert.Equal("TREE_INVALID", problem.GetProperty("error_code").GetString());
        Assert.Contains(problem.GetProperty("params").GetProperty("errors").EnumerateArray(),
            e => e.GetProperty("code").GetString() == "BINDING_SOURCE_NOT_ALLOWED");
    }

    [Fact]
    public async Task System_page_structure_change_is_422_SYSTEM_PAGE_STRUCTURE_CHANGED()
    {
        var (owner, shopId, _) = await OwnerWithWebsiteAsync();
        var contact = (await ListPagesAsync(owner.Token, shopId)).Single(p => p.SystemType == Vsite.Domain.Website.Enums.SystemType.Contact);
        var draft = await GetDraftAsync(owner.Token, shopId, contact.Id);
        var tree = JsonNode.Parse(draft.TreeJson)!.AsObject();
        tree["children"]!.AsArray().Add(JsonNode.Parse(RichText("c_extra1", "<p>thêm</p>")));

        var response = await SaveDraftAsync(owner.Token, shopId, contact.Id, tree.ToJsonString(), draft.Version);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains((await JsonOf(response)).GetProperty("params").GetProperty("errors").EnumerateArray(),
            e => e.GetProperty("code").GetString() == "SYSTEM_PAGE_STRUCTURE_CHANGED");
    }

    // ---------- R1: ảnh trong tree (đóng MEDIA-001 #1) ----------

    [Fact]
    public async Task Image_references_in_tree_library_other_shop_deleted_are_422_valid_clone_is_200()
    {
        var (owner, shopId, website) = await OwnerWithWebsiteAsync();
        var (ownerB, shopB) = await _h.NewOwnerWithShopAsync();
        var home = website.HomePageId;

        var library = await _h.UploadLibraryAsync(owner.Token, shopId);
        var clone = await _h.CloneAsync(owner.Token, shopId, library.Id);
        var deletedClone = await _h.CloneAsync(owner.Token, shopId, library.Id);
        await SoftDeleteAssetAsync(deletedClone.Id);
        var libraryB = await _h.UploadLibraryAsync(ownerB.Token, shopB);
        var cloneB = await _h.CloneAsync(ownerB.Token, shopB, libraryB.Id);

        foreach (var (label, id) in new[] { ("library", library.Id), ("other-shop clone", cloneB.Id), ("deleted clone", deletedClone.Id), ("unknown", Guid.NewGuid()) })
        {
            var draft = await GetDraftAsync(owner.Token, shopId, home);
            var response = await SaveDraftAsync(owner.Token, shopId, home, TreeWith(Hero("c_hero1", id.ToString("D"))), draft.Version);
            Assert.True(response.StatusCode == HttpStatusCode.UnprocessableEntity, $"{label}: {(int)response.StatusCode}");
            Assert.Equal("MEDIA_INVALID_IMAGE_REFERENCE", await ErrorCode(response));
        }

        var ok = await GetDraftAsync(owner.Token, shopId, home);
        var saved = await SaveDraftAsync(owner.Token, shopId, home, TreeWith(Hero("c_hero1", clone.Id.ToString("D"))), ok.Version);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
    }

    // ---------- 413 ----------

    [Fact]
    public async Task Body_over_limit_is_413_TREE_BODY_TOO_LARGE_but_media_keeps_its_own_code()
    {
        var (owner, shopId, website) = await OwnerWithWebsiteAsync();
        var draft = await GetDraftAsync(owner.Token, shopId, website.HomePageId);
        var huge = TreeWith(RichText("c_abc12", new string('a', 1_200_000)));

        var response = await SaveDraftAsync(owner.Token, shopId, website.HomePageId, huge, draft.Version);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("TREE_BODY_TOO_LARGE", await ErrorCode(response));
        Assert.Equal(draft.Version, (await GetDraftAsync(owner.Token, shopId, website.HomePageId)).Version);
    }

    [Fact]
    public async Task Body_slightly_over_tree_limit_but_within_envelope_slack_is_422_with_TREE_BODY_TOO_LARGE_in_errors()
    {
        var (owner, shopId, website) = await OwnerWithWebsiteAsync();
        var draft = await GetDraftAsync(owner.Token, shopId, website.HomePageId);
        var big = TreeWith(RichText("c_abc12", new string('a', 1_048_576 + 100)));

        var response = await SaveDraftAsync(owner.Token, shopId, website.HomePageId, big, draft.Version);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains((await JsonOf(response)).GetProperty("params").GetProperty("errors").EnumerateArray(),
            e => e.GetProperty("code").GetString() == "TREE_BODY_TOO_LARGE");
    }
}
