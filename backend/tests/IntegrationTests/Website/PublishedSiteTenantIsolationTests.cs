using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Vsite.Api.Identity;
using Vsite.Application.Identity.Auth.Dtos;
using Vsite.Application.Website.Dtos;
using Vsite.Application.Website.Interfaces;
using Vsite.IntegrationTests.Media;

namespace Vsite.IntegrationTests.Website;

/// <summary>
/// T7, WEBSITE-004 — <c>GET /api/site</c> không đường nào ra bản của shop khác (R1, #21), bản publish bất biến (R3) và
/// sống sót sau xoá mềm ảnh (R11). Dữ liệu dựng bằng API thật; ghi DB trực tiếp thì <c>InvalidatePointerAsync</c>. CẦN Docker.
/// </summary>
[Collection(MediaApiCollection.Name)]
public sealed class PublishedSiteTenantIsolationTests(MediaApiFactory factory) : PublishedSiteTestBase(factory)
{
    private static string Marker(string name) => $"{name}-{Guid.NewGuid():N}";

    private async Task SaveHomeAsync(ShopApiHarness.TestUser owner, Guid shopId, string treeJson)
    {
        var pages = (await _h.SendAsync(HttpMethod.Get, Site(shopId) + "/pages", owner.Token))
            .Content.ReadFromJsonAsync<List<PageDto>>(ShopApiHarness.Json);
        var home = (await pages)!.Single(p => p.Slug == "");
        var draft = await JsonOf(await _h.SendAsync(HttpMethod.Get, $"{Site(shopId)}/pages/{home.Id}/draft", owner.Token));
        var saved = await _h.SendRawJsonAsync(
            HttpMethod.Put, $"{Site(shopId)}/pages/{home.Id}/draft", owner.Token,
            $"{{\"tree\":{treeJson},\"version\":{draft.GetProperty("version").GetInt64()}}}");
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
    }

    /// <summary>Shop mới, trang chủ có đoạn văn mang <paramref name="marker"/>, đã publish.</summary>
    private async Task<(ShopApiHarness.TestUser Owner, Guid ShopId, string Host, PublicationDto Publication)> PublishedWithMarkerAsync(string marker)
    {
        var (owner, shopId, host) = await OwnerWithWebsiteAsync();
        await SaveHomeAsync(owner, shopId, WebsiteHandlerKit.TreeWith(WebsiteHandlerKit.RichText("c_mark1", $"<p>{marker}</p>")));
        return (owner, shopId, host, await PublishAsync(owner, shopId));
    }

    private static async Task<string> BodyOf(HttpResponseMessage response) => await response.Content.ReadAsStringAsync();

    private static async Task<string> SnapshotOf(HttpResponseMessage response) =>
        (await JsonOf(response)).GetProperty("publication").GetProperty("snapshot").GetRawText();

    /// <summary>Token audience <c>shop:{shop}</c> thật: đăng ký + xác minh + đăng nhập TRÊN host của shop (03 §6.1).</summary>
    private async Task<string> ShopAudienceTokenAsync(string shopHost)
    {
        var email = $"shopuser-{Guid.NewGuid():N}@example.test";
        async Task<HttpResponseMessage> Post(string path, object body)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body), Headers = { Host = shopHost } };
            return await _h.Client.SendAsync(request);
        }

        Assert.True((await Post("/api/auth/register", new RegisterRequest(email, ShopApiHarness.Password, "Shop User"))).IsSuccessStatusCode);
        var verifyToken = _h.Factory.EmailSpy.ExtractLastTokenFor(email);
        var verify = await _h.Client.SendAsync(new HttpRequestMessage(
            HttpMethod.Get, $"/api/auth/verify-email?token={Uri.EscapeDataString(verifyToken)}") { Headers = { Host = shopHost } });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var login = await Post("/api/auth/login", new LoginRequest(email, ShopApiHarness.Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (await login.Content.ReadFromJsonAsync<AuthTokenResult>())!.AccessToken;
    }

    // ---------- R1: tenant chỉ đến từ Host ----------

    [Fact]
    public async Task Each_host_serves_its_own_publication_R1()
    {
        var markerA = Marker("alpha");
        var markerB = Marker("beta");
        var (_, _, hostA, pubA) = await PublishedWithMarkerAsync(markerA);
        var (_, _, hostB, pubB) = await PublishedWithMarkerAsync(markerB);

        var a = await GetSiteAsync(hostA);
        var b = await GetSiteAsync(hostB);

        Assert.Equal(HttpStatusCode.OK, a.StatusCode);
        Assert.Equal(HttpStatusCode.OK, b.StatusCode);
        var bodyA = await BodyOf(a);
        var bodyB = await BodyOf(b);
        Assert.Contains(markerA, bodyA);
        Assert.DoesNotContain(markerB, bodyA);
        Assert.Contains(markerB, bodyB);
        Assert.DoesNotContain(markerA, bodyB);
        Assert.NotEqual(pubA.Id, pubB.Id);
    }

    [Fact]
    public async Task Identifier_params_and_forwarded_host_headers_on_host_A_never_return_B_R1()
    {
        var markerA = Marker("alpha");
        var markerB = Marker("beta");
        var (_, _, hostA, _) = await PublishedWithMarkerAsync(markerA);
        var (_, shopB, hostB, _) = await PublishedWithMarkerAsync(markerB);
        var baseline = await BodyOf(await GetSiteAsync(hostA));
        Assert.Contains(markerA, baseline);

        var queries = new[]
        {
            $"/api/site?shopId={shopB}",
            $"/api/site?shopId={shopB:N}",
            $"/api/site?host={Uri.EscapeDataString(hostB)}",
            $"/api/site?host={hostB}&shopId={shopB}&slug={hostB.Split('.')[0]}",
        };
        foreach (var path in queries)
        {
            var response = await GetSiteAsync(hostA, path: path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(baseline, await BodyOf(response));
        }

        var headerSets = new[]
        {
            new Dictionary<string, string> { ["X-Forwarded-Host"] = hostB },
            new Dictionary<string, string> { ["X-Forwarded-Host"] = hostB.Split('.')[0] },
            new Dictionary<string, string> { ["X-Original-Host"] = hostB },
            new Dictionary<string, string> { ["Forwarded"] = $"host={hostB}" },
            new Dictionary<string, string> { ["X-Shop-Id"] = shopB.ToString(), ["X-ShopId"] = shopB.ToString() },
        };
        foreach (var headers in headerSets)
        {
            var response = await GetSiteAsync(hostA, extraHeaders: headers);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(baseline, await BodyOf(response));
        }
    }

    [Fact]
    public async Task Owner_B_tokens_on_host_A_never_return_B_R1()
    {
        var markerA = Marker("alpha");
        var markerB = Marker("beta");
        var (_, _, hostA, _) = await PublishedWithMarkerAsync(markerA);
        var (ownerB, shopB, hostB, _) = await PublishedWithMarkerAsync(markerB);
        var baseline = await BodyOf(await GetSiteAsync(hostA));

        // Token login thường = token global: endpoint công khai bỏ qua token → y hệt không token.
        var withGlobal = await GetSiteAsync(hostA, token: ownerB.Token, path: $"/api/site?shopId={shopB}");
        Assert.Equal(HttpStatusCode.OK, withGlobal.StatusCode);
        Assert.Equal(baseline, await BodyOf(withGlobal));

        // Token audience shop:{B} trên host A: ShopMembershipValidationMiddleware chặn TRƯỚC endpoint (fail-closed).
        var shopBToken = await ShopAudienceTokenAsync(hostB);
        var mismatch = await GetSiteAsync(hostA, token: shopBToken);
        Assert.Equal(HttpStatusCode.Forbidden, mismatch.StatusCode);
        Assert.Equal("TOKEN_SHOP_MISMATCH", await ErrorCode(mismatch));

        // Cùng token trên đúng host B vẫn bình thường (chứng minh token hợp lệ, không phải token hỏng).
        var onB = await GetSiteAsync(hostB, token: shopBToken);
        Assert.Equal(HttpStatusCode.OK, onB.StatusCode);
        Assert.Contains(markerB, await BodyOf(onB));
    }

    [Fact]
    public async Task Only_B_published_host_A_is_404_even_though_B_has_a_publication_R1()
    {
        var markerB = Marker("beta");
        var (_, _, hostA) = await OwnerWithWebsiteAsync(); // A có website Draft, chưa publish
        var (_, shopB, hostB, _) = await PublishedWithMarkerAsync(markerB);
        Assert.Equal(HttpStatusCode.OK, (await GetSiteAsync(hostB)).StatusCode);

        foreach (var path in new[] { "/api/site", $"/api/site?shopId={shopB}", $"/api/site?host={hostB}" })
        {
            var response = await GetSiteAsync(hostA, path: path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("SITE_NOT_FOUND", await ErrorCode(response));
            Assert.DoesNotContain(markerB, await BodyOf(response));
        }

        // Shop A hoàn toàn không có website cũng 404.
        var (_, noWebsiteShop) = await _h.NewOwnerWithShopAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await GetSiteAsync(HostOf(await SlugOf(noWebsiteShop)), path: $"/api/site?shopId={shopB}")).StatusCode);
    }

    [Fact]
    public async Task Pointer_of_A_never_holds_the_publicationId_of_B_R1()
    {
        var (_, shopA, hostA, pubA) = await PublishedWithMarkerAsync(Marker("alpha"));
        Assert.Equal(HttpStatusCode.OK, (await GetSiteAsync(hostA)).StatusCode); // làm nóng con trỏ A

        var (ownerB, shopB, hostB, pubB) = await PublishedWithMarkerAsync(Marker("beta"));
        Assert.Equal(HttpStatusCode.OK, (await GetSiteAsync(hostB)).StatusCode); // làm nóng con trỏ B
        Assert.Equal(HttpStatusCode.OK, (await GetSiteAsync(hostA)).StatusCode);

        await using var scope = _h.Factory.Services.CreateAsyncScope();
        var pointers = scope.ServiceProvider.GetRequiredService<ISitePointerCache>();
        var pointerA = await pointers.GetAsync(shopA, default);
        var pointerB = await pointers.GetAsync(shopB, default);
        Assert.Equal(pubA.Id, pointerA!.PublicationId);
        Assert.Equal(pubB.Id, pointerB!.PublicationId);

        var raw = await scope.ServiceProvider.GetRequiredService<IDistributedCache>().GetStringAsync($"site:pointer:{shopA:N}");
        Assert.NotNull(raw);
        Assert.DoesNotContain(pubB.Id.ToString(), raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(pubB.Id.ToString("N"), raw, StringComparison.OrdinalIgnoreCase);

        // Publish lại B (xoá con trỏ B) rồi đọc A: con trỏ A vẫn của A, A vẫn thấy bản A.
        var pubB2 = await PublishAsync(ownerB, shopB);
        Assert.NotEqual(pubB.Id, pubB2.Id);
        var again = await GetSiteAsync(hostA);
        Assert.Equal(pubA.Id, (await JsonOf(again)).GetProperty("publication").GetProperty("id").GetGuid());
        Assert.Equal(pubA.Id, (await pointers.GetAsync(shopA, default))!.PublicationId);
        Assert.NotEqual(pubB2.Id, (await pointers.GetAsync(shopA, default))!.PublicationId);
    }

    // ---------- R3: bản publish bất biến ----------

    [Fact]
    public async Task Editing_draft_theme_pages_after_publish_leaves_snapshot_and_ETag_untouched_R3()
    {
        var marker = Marker("alpha");
        var (owner, shopId, host) = await OwnerWithWebsiteAsync();
        var created = await _h.SendJsonAsync(HttpMethod.Post, Site(shopId) + "/pages", owner.Token,
            new { slug = "gioi-thieu", title = "Giới thiệu", isEnabled = true });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var extra = (await created.Content.ReadFromJsonAsync<PageDto>(ShopApiHarness.Json))!;
        await SaveHomeAsync(owner, shopId, WebsiteHandlerKit.TreeWith(WebsiteHandlerKit.RichText("c_mark1", $"<p>{marker}</p>")));
        await PublishAsync(owner, shopId);
        var homeId = (await (await _h.SendAsync(HttpMethod.Get, Site(shopId) + "/pages", owner.Token))
            .Content.ReadFromJsonAsync<List<PageDto>>(ShopApiHarness.Json))!.Single(p => p.Slug == "").Id;

        var before = await GetSiteAsync(host);
        var etag = before.Headers.ETag!.Tag;
        var snapshot = await SnapshotOf(before);
        Assert.Contains(marker, snapshot);
        Assert.Contains("gioi-thieu", snapshot);

        async Task AssertUnchanged(string step)
        {
            var now = await GetSiteAsync(host);
            Assert.True(HttpStatusCode.OK == now.StatusCode, step);
            Assert.True(etag == now.Headers.ETag!.Tag, $"{step}: ETag đổi");
            Assert.True(snapshot == await SnapshotOf(now), $"{step}: snapshot đổi");
        }

        // PUT draft
        await SaveHomeAsync(owner, shopId, WebsiteHandlerKit.TreeWith(WebsiteHandlerKit.RichText("c_mark1", "<p>đã sửa sau publish</p>")));
        var liveDraft = await JsonOf(await _h.SendAsync(HttpMethod.Get, $"{Site(shopId)}/pages/{homeId}/draft", owner.Token));
        Assert.Contains("đã sửa sau publish", liveDraft.GetProperty("tree").GetRawText()); // PUT có hiệu lực thật
        await AssertUnchanged("PUT draft");

        // PUT theme
        var theme = await JsonOf(await _h.SendAsync(HttpMethod.Get, Site(shopId) + "/theme", owner.Token));
        var tokens = JsonNode.Parse(theme.GetProperty("tokens").GetRawText())!;
        tokens["colors"]!["primary"] = "#ff0000";
        var putTheme = await _h.SendRawJsonAsync(HttpMethod.Put, Site(shopId) + "/theme", owner.Token,
            $"{{\"presetKey\":null,\"tokens\":{tokens.ToJsonString()}}}");
        Assert.Equal(HttpStatusCode.OK, putTheme.StatusCode);
        var liveTheme = await JsonOf(await _h.SendAsync(HttpMethod.Get, Site(shopId) + "/theme", owner.Token));
        Assert.Equal("#ff0000", liveTheme.GetProperty("tokens").GetProperty("colors").GetProperty("primary").GetString());
        await AssertUnchanged("PUT theme");

        // PUT page: đổi SEO rồi tắt trang
        var seo = await _h.SendJsonAsync(HttpMethod.Put, $"{Site(shopId)}/pages/{extra.Id}", owner.Token,
            new { slug = (string?)null, title = "Tiêu đề mới", seoTitle = "SEO mới", seoDescription = "mô tả mới", noIndex = true, isEnabled = true });
        Assert.Equal(HttpStatusCode.OK, seo.StatusCode);
        var seoLive = await JsonOf(seo);
        Assert.Equal("SEO mới", seoLive.GetProperty("seoTitle").GetString());
        Assert.True(seoLive.GetProperty("noIndex").GetBoolean());
        await AssertUnchanged("PUT page (SEO)");
        var disable = await _h.SendJsonAsync(HttpMethod.Put, $"{Site(shopId)}/pages/{extra.Id}", owner.Token,
            new { slug = (string?)null, title = "Tiêu đề mới", seoTitle = "SEO mới", noIndex = true, isEnabled = false });
        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);
        Assert.False((await JsonOf(disable)).GetProperty("isEnabled").GetBoolean());
        await AssertUnchanged("PUT page (tắt trang)");

        // DELETE page
        Assert.Equal(HttpStatusCode.NoContent, (await _h.SendAsync(HttpMethod.Delete, $"{Site(shopId)}/pages/{extra.Id}", owner.Token)).StatusCode);
        await AssertUnchanged("DELETE page");

        // Con trỏ nguội (TTL hết / Redis mất) cũng đọc ra đúng bản cũ, không phải draft hiện tại.
        await InvalidatePointerAsync(shopId);
        await AssertUnchanged("con trỏ nguội");
    }

    // ---------- R11: xoá mềm ảnh sau publish ----------

    [Fact]
    public async Task Soft_deleting_an_image_after_publish_keeps_it_in_snapshot_media_R11()
    {
        var (owner, shopId, host) = await OwnerWithWebsiteAsync();
        var library = await _h.UploadLibraryAsync(owner.Token, shopId);
        var clone = await _h.CloneAsync(owner.Token, shopId, library.Id);
        await SaveHomeAsync(owner, shopId, WebsiteHandlerKit.TreeWith(WebsiteHandlerKit.Hero("c_hero1", clone.Id.ToString())));
        await PublishAsync(owner, shopId);

        var before = await GetSiteAsync(host);
        var snapshotBefore = await SnapshotOf(before);
        var etagBefore = before.Headers.ETag!.Tag;
        Assert.True(JsonNode.Parse(snapshotBefore)!["media"]!.AsObject().ContainsKey(clone.Id.ToString()));

        // API chỉ xoá được bản Library (không kéo theo clone; clone là Kind khác → 404), nên xoá mềm clone bằng SQL
        // như các test Website khác (cùng kết quả: is_deleted = true, bị Global Query Filter loại).
        var delete = await _h.SendAsync(HttpMethod.Delete, $"/api/shops/{shopId}/media/library/{library.Id}", owner.Token);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        await SqlAsync($"UPDATE media_asset SET is_deleted = true WHERE id = {clone.Id}");
        await using (var scope = _h.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Vsite.Infrastructure.Persistence.AppDbContext>();
            var deleted = await db.Database
                .SqlQuery<bool>($"SELECT is_deleted AS \"Value\" FROM media_asset WHERE id = {clone.Id}").FirstAsync();
            Assert.True(deleted, "ảnh clone phải đã bị xoá mềm");
        }

        await InvalidatePointerAsync(shopId);
        var after = await GetSiteAsync(host);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        var snapshotAfter = await SnapshotOf(after);
        Assert.Equal(snapshotBefore, snapshotAfter);
        Assert.Equal(etagBefore, after.Headers.ETag!.Tag);
        Assert.True(JsonNode.Parse(snapshotAfter)!["media"]!.AsObject().ContainsKey(clone.Id.ToString()));
    }
}
