using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Vsite.Api.Identity;
using Vsite.Api.Shop;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Identity.Auth.Dtos;
using Vsite.Application.Media.Dtos;
using Vsite.Application.Shop.Dtos;
using Vsite.Domain.Identity.Entities;
using Vsite.Domain.Media.Entities;
using Vsite.Domain.Shop.Enums;
using Vsite.Infrastructure.Persistence;
using ShopEntity = Vsite.Domain.Shop.Entities.Shop;

namespace Vsite.IntegrationTests.Media;

/// <summary>
/// T5, MEDIA-001 — test bắt buộc cho `POST /shops/{shopId}/media/slot-uploads` và
/// `POST /shops/{shopId}/media/library` (brief T5 — bảng endpoint ở `backend/docs/modules/media.md`). Đi qua pipeline HTTP thật
/// (`MediaApiFactory`, Postgres + Redis Testcontainers) — CẦN Docker daemon, xem
/// `Docs/DOCKER-TEST-DEBT.md`. Verify hành vi handler KHÔNG cần Docker ở
/// `UploadHandlerTests` (EF InMemory) cùng thư mục.
/// </summary>
[Collection(MediaApiCollection.Name)]
public sealed class UploadEndpointTests
{
    private const string Password = "Password123!";
    private const string PortalHost = "admin.vsite.local";

    private readonly MediaApiFactory _factory;
    private readonly HttpClient _client;

    public UploadEndpointTests(MediaApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ---- Test bắt buộc 3: không tick -> đúng 1 record ----

    [Fact]
    public async Task SlotUpload_without_SaveToLibrary_creates_one_Direct_record_with_correct_file()
    {
        var (token, shopId) = await CreateOwnerWithShopAsync();

        var response = await PostSlotUploadAsync(token, shopId, preset: "800x600,cover", saveToLibrary: false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SlotUploadResultDto>(JsonOptions);
        Assert.Null(result!.LibraryAsset);
        Assert.Equal("800x600,cover", result.Asset.Preset);
        Assert.Null(result.Asset.SourceAssetId);
        Assert.False(result.Asset.IsInLibrary);
        Assert.Equal(800, result.Asset.Width);
        Assert.Equal(600, result.Asset.Height);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assets = await db.MediaAssets.IgnoreQueryFilters().Where(a => a.ShopId == shopId).ToListAsync();
        var asset = Assert.Single(assets);

        var storagePath = Path.Combine(_factory.StorageRoot, asset.StorageKey.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(storagePath));
        using var image = await Image.LoadAsync(storagePath);
        Assert.Equal(800, image.Width);
        Assert.Equal(600, image.Height);
    }

    // ---- Test bắt buộc 4: có tick -> 2 record; response trả id CLONE ----

    [Fact]
    public async Task SlotUpload_with_SaveToLibrary_creates_library_plus_clone_and_returns_clone_id()
    {
        var (token, shopId) = await CreateOwnerWithShopAsync();

        var response = await PostSlotUploadAsync(token, shopId, preset: "800x600,cover", saveToLibrary: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SlotUploadResultDto>(JsonOptions);
        Assert.NotNull(result!.LibraryAsset);
        Assert.True(result.LibraryAsset!.IsInLibrary);
        Assert.False(result.Asset.IsInLibrary);
        Assert.Equal(result.LibraryAsset.Id, result.Asset.SourceAssetId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assets = await db.MediaAssets.IgnoreQueryFilters().Where(a => a.ShopId == shopId).ToListAsync();
        Assert.Equal(2, assets.Count);
    }

    [Fact]
    public async Task SlotUpload_with_unknown_preset_returns_422_MEDIA_UNKNOWN_PRESET()
    {
        var (token, shopId) = await CreateOwnerWithShopAsync();

        var response = await PostSlotUploadAsync(token, shopId, preset: "does-not-exist", saveToLibrary: false);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("MEDIA_UNKNOWN_PRESET", problem!.Extensions["error_code"]!.ToString());
    }

    [Fact]
    public async Task SlotUpload_with_focal_out_of_range_returns_422()
    {
        var (token, shopId) = await CreateOwnerWithShopAsync();

        var response = await PostSlotUploadAsync(token, shopId, preset: "800x600,cover", saveToLibrary: false, focalX: 1.5f);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ---- Review sau T5, issue #5: request không phải multipart -> 415 ProblemDetails, không 500 ----

    [Fact]
    public async Task SlotUpload_without_multipart_content_type_returns_415_MEDIA_MULTIPART_REQUIRED()
    {
        var (token, shopId) = await CreateOwnerWithShopAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/shops/{shopId}/media/slot-uploads")
        {
            Content = JsonContent.Create(new { preset = "800x600,cover" }),
            Headers = { Host = PortalHost },
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("MEDIA_MULTIPART_REQUIRED", problem!.Extensions["error_code"]!.ToString());
    }

    [Fact]
    public async Task SlotUpload_by_user_of_another_shop_returns_403_SHOP_ACCESS_DENIED()
    {
        var (_, shopAId) = await CreateOwnerWithShopAsync();
        var (tokenB, _) = await CreateOwnerWithShopAsync();

        var response = await PostSlotUploadAsync(tokenB, shopAId, preset: "800x600,cover", saveToLibrary: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("SHOP_ACCESS_DENIED", problem!.Extensions["error_code"]!.ToString());
    }

    // ---- #21.4: field "shopId" trong multipart bị bỏ qua, record vẫn thuộc shop của ROUTE ----

    [Fact]
    public async Task SlotUpload_ignores_shopId_field_in_multipart_body()
    {
        var (token, shopId) = await CreateOwnerWithShopAsync();
        var foreignShopId = Guid.NewGuid();

        using var content = BuildMultipart("800x600,cover", 0.5f, 0.5f, false, extraFields: new Dictionary<string, string> { ["shopId"] = foreignShopId.ToString() });
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/shops/{shopId}/media/slot-uploads") { Content = content, Headers = { Host = PortalHost } };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assets = await db.MediaAssets.IgnoreQueryFilters().Where(a => a.ShopId == shopId).ToListAsync();
        Assert.Single(assets);
        Assert.Empty(await db.MediaAssets.IgnoreQueryFilters().Where(a => a.ShopId == foreignShopId).ToListAsync());
    }

    // ---- R4: SaveChangesAsync ném lỗi -> thư mục storage rỗng sau request ----

    [Fact]
    public async Task SlotUpload_when_SaveChanges_throws_leaves_storage_empty()
    {
        var (token, shopId) = await CreateOwnerWithShopAsync();

        // WithWebHostBuilder trả WebApplicationFactory<Program> (DelegatedWebApplicationFactory nội
        // bộ, KHÔNG phải MediaApiFactory) — dùng đúng type khai báo, không cast. Container
        // Postgres/Redis của _factory được tái sử dụng (connection string đã cấu hình sẵn qua
        // ConfigureAppConfiguration của _factory), storage vẫn dùng chung _factory.StorageRoot.
        await using var throwingFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAppDbContext>();
                services.AddScoped<IAppDbContext>(sp => new ThrowingDbContext(sp.GetRequiredService<AppDbContext>()));
            }));
        using var throwingClient = throwingFactory.CreateClient();

        using var content = BuildMultipart("800x600,cover", 0.5f, 0.5f, false);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/shops/{shopId}/media/slot-uploads") { Content = content, Headers = { Host = PortalHost } };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await throwingClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        // KHÔNG assert cả _factory.StorageRoot rỗng — collection fixture (MediaApiCollection) share
        // MỘT factory/storage cho toàn bộ test trong class này, test khác chạy song song/trước đó có
        // thể để lại file hợp lệ ở shop KHÁC. Chỉ shop vừa tạo (shopId mới, riêng cho test này) mới
        // phải rỗng sau rollback.
        var shopDir = Path.Combine(_factory.StorageRoot, "shops", shopId.ToString());
        var filesUnderShop = Directory.Exists(shopDir)
            ? Directory.GetFiles(shopDir, "*", SearchOption.AllDirectories)
            : [];
        Assert.Empty(filesUnderShop);
    }

    // ---- POST /shops/{shopId}/media/library ----

    [Fact]
    public async Task LibraryUpload_creates_one_Library_record()
    {
        var (token, shopId) = await CreateOwnerWithShopAsync();

        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(EncodeJpeg(1600, 1200)), "file", "photo.jpg" },
        };
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/shops/{shopId}/media/library") { Content = content, Headers = { Host = PortalHost } };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<MediaAssetDto>(JsonOptions);
        Assert.True(dto!.IsInLibrary);
        Assert.Null(dto.Preset);
    }

    // ---- helpers ----

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web)
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        };

    private async Task<HttpResponseMessage> PostSlotUploadAsync(
        string token, Guid shopId, string preset, bool saveToLibrary, float focalX = 0.5f, float focalY = 0.5f)
    {
        using var content = BuildMultipart(preset, focalX, focalY, saveToLibrary);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/shops/{shopId}/media/slot-uploads")
        {
            Content = content,
            Headers = { Host = PortalHost },
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private static MultipartFormDataContent BuildMultipart(
        string preset, float focalX, float focalY, bool saveToLibrary, IDictionary<string, string>? extraFields = null)
    {
        var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(EncodeJpeg(1600, 1200)), "file", "photo.jpg" },
            { new StringContent(preset), "preset" },
            { new StringContent(focalX.ToString(System.Globalization.CultureInfo.InvariantCulture)), "focalX" },
            { new StringContent(focalY.ToString(System.Globalization.CultureInfo.InvariantCulture)), "focalY" },
            { new StringContent(saveToLibrary.ToString()), "saveToLibrary" },
        };

        if (extraFields is not null)
        {
            foreach (var (key, value) in extraFields)
            {
                content.Add(new StringContent(value), key);
            }
        }

        return content;
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
        var email = NewEmail();
        var token = await RegisterVerifyLoginGlobalAsync(email);
        var createResponse = await CreateShopAsync(token, new CreateShopRequest("Media Test Shop", NewSlug(), ShopKind.Hosted, null));
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

    private static string NewEmail() => $"media-{Guid.NewGuid():N}@example.test";

    private static string NewSlug() => $"media-shop-{Guid.NewGuid():N}";

    /// <summary>Decorator R4 (đi qua HTTP thật): forward mọi DbSet, <c>SaveChangesAsync</c> luôn
    /// ném lỗi — mô phỏng "ghi file thành công, DB fail" ở tầng handler thật, qua pipeline đầy đủ.</summary>
    private sealed class ThrowingDbContext(IAppDbContext inner) : IAppDbContext
    {
        public DbSet<User> Users => inner.Users;
        public DbSet<ExternalLogin> ExternalLogins => inner.ExternalLogins;
        public DbSet<Role> Roles => inner.Roles;
        public DbSet<UserShop> UserShops => inner.UserShops;
        public DbSet<PendingRegistration> PendingRegistrations => inner.PendingRegistrations;
        public DbSet<RefreshToken> RefreshTokens => inner.RefreshTokens;
        public DbSet<PasswordResetToken> PasswordResetTokens => inner.PasswordResetTokens;
        public DbSet<ShopEntity> Shops => inner.Shops;
        public DbSet<MediaAsset> MediaAssets => inner.MediaAssets;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated DB failure (R4 endpoint test).");
    }
}
