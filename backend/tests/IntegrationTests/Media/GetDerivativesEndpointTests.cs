using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
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

namespace Vsite.IntegrationTests.Media;

/// <summary>
/// MEDIA-001 D3 (#73) — `GET /shops/{shopId}/media/library/{assetId}/derivatives?preset=`. Đi qua
/// pipeline HTTP thật (`MediaApiFactory`, Postgres + Redis Testcontainers) — CẦN Docker daemon.
/// Handler-level (không Docker) ở `GetDerivativesHandlerTests`.
///
/// Chạy: <c>dotnet test backend/tests/IntegrationTests --filter "FullyQualifiedName~GetDerivativesEndpointTests"</c>.
/// </summary>
[Collection(MediaApiCollection.Name)]
public sealed class GetDerivativesEndpointTests
{
    private const string Password = "Password123!";
    private const string PortalHost = "admin.vsite.local";
    private const string MainHost = "vsite.local";

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web)
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        };

    private readonly MediaApiFactory _factory;
    private readonly HttpClient _client;

    public GetDerivativesEndpointTests(MediaApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Real_logo_flow_returns_the_uploaded_derivative_readable_at_media_url()
    {
        var (token, shopId) = await CreateOwnerWithShopAsync();
        var logo = await PutLogoAsync(token, shopId);
        var uploaded = Assert.Single(logo.Derivatives, d => d.Preset == "320x96,inside");

        var response = await GetDerivativesAsync(token, shopId, logo.LibraryAsset.Id, Uri.EscapeDataString("320x96,inside"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = (await response.Content.ReadFromJsonAsync<List<MediaAssetDto>>(JsonOptions))!;
        var found = Assert.Single(list);
        Assert.Equal(uploaded.StorageKey, found.StorageKey);
        Assert.Equal(uploaded.Id, found.Id);
        Assert.Equal("320x96,inside", found.Preset);
        Assert.Equal(logo.LibraryAsset.Id, found.SourceAssetId);
        Assert.False(found.IsInLibrary);

        var fileRequest = new HttpRequestMessage(HttpMethod.Get, "/media/" + found.StorageKey) { Headers = { Host = MainHost } };
        var fileResponse = await _client.SendAsync(fileRequest);
        Assert.Equal(HttpStatusCode.OK, fileResponse.StatusCode);
        Assert.NotEmpty(await fileResponse.Content.ReadAsByteArrayAsync());

        // Bỏ preset -> trả mọi phái sinh của logo.
        var allResponse = await GetDerivativesAsync(token, shopId, logo.LibraryAsset.Id, null);
        var all = (await allResponse.Content.ReadFromJsonAsync<List<MediaAssetDto>>(JsonOptions))!;
        Assert.Equal(logo.Derivatives.Count, all.Count);
    }

    [Fact]
    public async Task Unknown_or_derivativeless_id_returns_200_empty_list()
    {
        var (token, shopId) = await CreateOwnerWithShopAsync();

        var response = await GetDerivativesAsync(token, shopId, Guid.NewGuid(), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<List<MediaAssetDto>>(JsonOptions))!);
    }

    [Fact]
    public async Task Other_shops_library_id_returns_empty_list_not_leak()
    {
        var (tokenA, shopA) = await CreateOwnerWithShopAsync();
        var (tokenB, shopB) = await CreateOwnerWithShopAsync();
        var logo = await PutLogoAsync(tokenA, shopA);

        var response = await GetDerivativesAsync(tokenB, shopB, logo.LibraryAsset.Id, null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<List<MediaAssetDto>>(JsonOptions))!);
    }

    [Fact]
    public async Task Non_member_of_route_shop_gets_403_SHOP_ACCESS_DENIED()
    {
        var (_, shopA) = await CreateOwnerWithShopAsync();
        var (tokenB, _) = await CreateOwnerWithShopAsync();

        var response = await GetDerivativesAsync(tokenB, shopA, Guid.NewGuid(), null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("SHOP_ACCESS_DENIED", problem!.Extensions["error_code"]!.ToString());
    }

    [Fact]
    public async Task Preset_longer_than_40_chars_returns_422()
    {
        var (token, shopId) = await CreateOwnerWithShopAsync();

        var response = await GetDerivativesAsync(token, shopId, Guid.NewGuid(), new string('x', 41));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ---- helpers ----

    private async Task<ShopLogoDto> PutLogoAsync(string token, Guid shopId)
    {
        using var content = new MultipartFormDataContent { { new ByteArrayContent(EncodeJpeg(1600, 1200)), "file", "logo.jpg" } };
        var request = new HttpRequestMessage(HttpMethod.Put, $"/shops/{shopId}/logo") { Content = content, Headers = { Host = PortalHost } };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ShopLogoDto>(JsonOptions))!;
    }

    private async Task<HttpResponseMessage> GetDerivativesAsync(string token, Guid shopId, Guid assetId, string? encodedPreset)
    {
        var query = encodedPreset is null ? string.Empty : "?preset=" + encodedPreset;
        var request = new HttpRequestMessage(HttpMethod.Get, $"/shops/{shopId}/media/library/{assetId}/derivatives{query}")
        {
            Headers = { Host = PortalHost },
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private static byte[] EncodeJpeg(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        image.Mutate(x => x.Fill(Color.SeaGreen));
        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms);
        return ms.ToArray();
    }

    private async Task<(string Token, Guid ShopId)> CreateOwnerWithShopAsync()
    {
        var email = $"derivatives-{Guid.NewGuid():N}@example.test";
        await PostAsync("/auth/register", new RegisterRequest(email, Password, "Test User"));

        var verifyToken = _factory.EmailSpy.ExtractLastTokenFor(email);
        var verify = new HttpRequestMessage(HttpMethod.Get, $"/auth/verify-email?token={Uri.EscapeDataString(verifyToken)}") { Headers = { Host = PortalHost } };
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(verify)).StatusCode);

        var loginResponse = await PostAsync("/auth/login", new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var token = (await loginResponse.Content.ReadFromJsonAsync<AuthTokenResult>())!.AccessToken;

        var create = new HttpRequestMessage(HttpMethod.Post, "/shops")
        {
            Content = JsonContent.Create(new CreateShopRequest("Derivatives Test Shop", $"deriv-{Guid.NewGuid():N}", ShopKind.Hosted, null)),
            Headers = { Host = PortalHost },
        };
        create.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var shop = (await (await _client.SendAsync(create)).Content.ReadFromJsonAsync<ShopDto>(JsonOptions))!;
        return (token, shop.Id);
    }

    private async Task<HttpResponseMessage> PostAsync<TBody>(string path, TBody body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body), Headers = { Host = PortalHost } };
        return await _client.SendAsync(request);
    }
}
