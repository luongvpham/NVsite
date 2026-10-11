using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Vsite.Api.Identity;
using Vsite.Api.Shop;
using Vsite.Application.Identity.Auth.Dtos;
using Vsite.Application.Media.Dtos;
using Vsite.Application.Shop.Dtos;
using Vsite.Domain.Shop.Enums;
using Vsite.Infrastructure.Persistence;

namespace Vsite.IntegrationTests.Media;

/// <summary>
/// T9, MEDIA-001 (#53, #81, #83) — test bắt buộc cho <c>/media/{key}</c> qua pipeline HTTP thật
/// (`MediaApiFactory`, Postgres + Redis Testcontainers) — CẦN Docker daemon, xem
/// `Docs/DOCKER-TEST-DEBT.md`. Test bắt buộc 12: cùng một ảnh phải trả 200 + bytes giống nhau trên
/// CẢ BA host (main/shop-subdomain/portal) vì `/media` branch off pipeline TRƯỚC
/// `TenantResolutionMiddleware` — không tenant nào chặn được.
///
/// R1 (path traversal) verify KHÔNG cần Docker ở <c>MediaFileMiddlewareTests</c> cùng thư mục —
/// class này chỉ thêm lại vài biến thể traversal đi qua HTTP thật để chắc chắn `Program.cs` wiring
/// (Map("/media", ...) TRƯỚC TenantResolutionMiddleware) không đổi hành vi.
///
/// Chạy (máy CÓ Docker daemon):
/// <c>dotnet test backend/tests/IntegrationTests --filter "FullyQualifiedName~MediaFileServingTests"</c>.
/// </summary>
[Collection(MediaApiCollection.Name)]
public sealed class MediaFileServingTests
{
    private const string Password = "Password123!";
    private const string PortalHost = "admin.vsite.local";
    private const string MainHost = "vsite.local";
    private const string ShopSlug = "spa-abc";

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web)
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        };

    private readonly MediaApiFactory _factory;
    private readonly HttpClient _client;

    public MediaFileServingTests(MediaApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ---- Test bắt buộc 12: 3 host, cùng bytes ----

    [Fact]
    public async Task GET_media_key_returns_200_with_identical_bytes_on_every_host()
    {
        var (token, shopId) = await CreateOwnerWithShopAsync(ShopSlug);
        var asset = await UploadLibraryAsync(token, shopId);

        var mainResponse = await GetMediaAsync(asset.StorageKey, MainHost);
        var shopResponse = await GetMediaAsync(asset.StorageKey, $"{ShopSlug}.vsite.local");
        var portalResponse = await GetMediaAsync(asset.StorageKey, PortalHost);

        Assert.Equal(HttpStatusCode.OK, mainResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, shopResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, portalResponse.StatusCode);

        var mainBytes = await mainResponse.Content.ReadAsByteArrayAsync();
        var shopBytes = await shopResponse.Content.ReadAsByteArrayAsync();
        var portalBytes = await portalResponse.Content.ReadAsByteArrayAsync();

        Assert.Equal(mainBytes, shopBytes);
        Assert.Equal(mainBytes, portalBytes);
        Assert.NotEmpty(mainBytes);

        Assert.Equal("image/webp", mainResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal("public, max-age=3600", mainResponse.Headers.CacheControl?.ToString());
        Assert.Equal("nosniff", mainResponse.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task GET_media_key_requires_no_auth_token()
    {
        var (token, shopId) = await CreateOwnerWithShopAsync("spa-noauth");
        var asset = await UploadLibraryAsync(token, shopId);

        var request = new HttpRequestMessage(HttpMethod.Get, "/media/" + asset.StorageKey) { Headers = { Host = MainHost } };
        // Cố tình KHÔNG set Authorization header.
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HEAD_media_key_returns_headers_without_body()
    {
        var (token, shopId) = await CreateOwnerWithShopAsync("spa-head");
        var asset = await UploadLibraryAsync(token, shopId);

        var request = new HttpRequestMessage(HttpMethod.Head, "/media/" + asset.StorageKey) { Headers = { Host = MainHost } };
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsByteArrayAsync();
        Assert.Empty(body);
    }

    // ---- R1: qua HTTP thật ----

    [Theory]
    [InlineData("/media/../appsettings.json")]
    [InlineData("/media/shops/%2e%2e/%2e%2e/appsettings.json")]
    [InlineData("/media/shops%5c..%5cx")]
    [InlineData("/media//etc/passwd")]
    public async Task Traversal_paths_return_404_over_real_HTTP_pipeline(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path) { Headers = { Host = MainHost } };
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task POST_media_key_returns_405()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/media/shops/whatever/x.webp") { Headers = { Host = MainHost } };
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task GET_unknown_key_returns_404()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/media/shops/does-not-exist/x.webp") { Headers = { Host = MainHost } };
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- helpers ----

    private Task<HttpResponseMessage> GetMediaAsync(string storageKey, string host)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/media/" + storageKey) { Headers = { Host = host } };
        return _client.SendAsync(request);
    }

    private async Task<MediaAssetDto> UploadLibraryAsync(string token, Guid shopId)
    {
        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(EncodeJpeg(1600, 1200)), "file", "photo.jpg" },
        };
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/shops/{shopId}/media/library") { Content = content, Headers = { Host = PortalHost } };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<MediaAssetDto>(JsonOptions))!;
    }

    private static byte[] EncodeJpeg(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        image.Mutate(x => x.Fill(Color.SeaGreen));
        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms);
        return ms.ToArray();
    }

    private async Task<(string Token, Guid ShopId)> CreateOwnerWithShopAsync(string slug)
    {
        var email = NewEmail();
        var token = await RegisterVerifyLoginGlobalAsync(email);
        var createResponse = await CreateShopAsync(token, new CreateShopRequest("Media Serving Test Shop", slug, ShopKind.Hosted, null));
        var shop = (await createResponse.Content.ReadFromJsonAsync<ShopDto>(JsonOptions))!;
        return (token, shop.Id);
    }

    private async Task<string> RegisterVerifyLoginGlobalAsync(string email)
    {
        await PostAsync(PortalHost, "/api/auth/register", new RegisterRequest(email, Password, "Test User"));

        var token = _factory.EmailSpy.ExtractLastTokenFor(email);
        var verifyResponse = await SendAsync(HttpMethod.Get, $"/api/auth/verify-email?token={Uri.EscapeDataString(token)}", PortalHost);
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        var loginResponse = await PostAsync(PortalHost, "/api/auth/login", new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthTokenResult>();
        return authResult!.AccessToken;
    }

    private Task<HttpResponseMessage> CreateShopAsync(string token, CreateShopRequest body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/shops")
        {
            Content = JsonContent.Create(body),
            Headers = { Host = PortalHost },
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PostAsync<TBody>(string host, string path, TBody body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body), Headers = { Host = host } };
        return await _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string host)
    {
        var request = new HttpRequestMessage(method, path) { Headers = { Host = host } };
        return _client.SendAsync(request);
    }

    private static string NewEmail() => $"media-serve-{Guid.NewGuid():N}@example.test";
}
