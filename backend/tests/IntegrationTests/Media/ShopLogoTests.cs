using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using Vsite.Api.Identity;
using Vsite.Api.Shop;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Identity.Auth.Dtos;
using Vsite.Application.Media.Dtos;
using Vsite.Application.Shop.Dtos;
using Vsite.Domain.Identity;
using Vsite.Domain.Identity.Entities;
using Vsite.Domain.Media.Entities;
using Vsite.Domain.Shop.Enums;
using Vsite.Infrastructure.Persistence;
using ShopEntity = Vsite.Domain.Shop.Entities.Shop;

namespace Vsite.IntegrationTests.Media;

/// <summary>
/// T7, MEDIA-001 (#73, #82) — test bắt buộc cho `PUT /shops/{shopId}/logo` (`task-T7-brief.md`). Đi
/// qua pipeline HTTP thật (`MediaApiFactory`, Postgres + Redis Testcontainers) — CẦN Docker daemon,
/// xem `Docs/DOCKER-TEST-DEBT.md`. Verify hành vi handler KHÔNG cần Docker ở
/// `ShopLogoHandlerTests` (EF InMemory) cùng thư mục.
///
/// Chạy (máy CÓ Docker daemon):
/// <c>dotnet test backend/tests/IntegrationTests --filter "FullyQualifiedName~ShopLogoTests"</c>.
/// </summary>
[Collection(MediaApiCollection.Name)]
public sealed class ShopLogoTests
{
    private const string Password = "Password123!";
    private const string PortalHost = "admin.vsite.local";

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web)
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        };

    private readonly MediaApiFactory _factory;
    private readonly HttpClient _client;

    public ShopLogoTests(MediaApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ---- upload -> 1 bản Library + đúng N phái sinh, N = For("Shop").Count, mỗi Preset một lần,
    // cùng SourceAssetId ----

    [Fact]
    public async Task Upload_creates_library_plus_one_derivative_per_shop_preset_with_same_SourceAssetId()
    {
        var (token, shopId) = await CreateOwnerWithShopAsync();

        var response = await PutLogoAsync(token, shopId, "logo.png");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ShopLogoDto>(JsonOptions);

        Assert.True(dto!.LibraryAsset.IsInLibrary);
        Assert.Null(dto.LibraryAsset.SourceAssetId);

        var expectedPresets = new[] { "320x96,inside", "96x96,cover" };
        Assert.Equal(expectedPresets.Length, dto.Derivatives.Count);
        Assert.Equal(expectedPresets.OrderBy(p => p), dto.Derivatives.Select(d => d.Preset).OrderBy(p => p));
        Assert.All(dto.Derivatives, d => Assert.Equal(dto.LibraryAsset.Id, d.SourceAssetId));
        Assert.All(dto.Derivatives, d => Assert.False(d.IsInLibrary));
    }

    // ---- Shop.LogoId = id bản Library; GET /shops/{id} trả logoId ----

    [Fact]
    public async Task Upload_sets_ShopLogoId_and_GetShop_returns_it()
    {
        var (token, shopId) = await CreateOwnerWithShopAsync();

        var response = await PutLogoAsync(token, shopId, "logo.png");
        var dto = await response.Content.ReadFromJsonAsync<ShopLogoDto>(JsonOptions);

        var getResponse = await GetShopAsync(token, shopId);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var shop = await getResponse.Content.ReadFromJsonAsync<ShopDto>(JsonOptions);
        Assert.Equal(dto!.LibraryAsset.Id, shop!.LogoId);
    }

    // ---- logo PNG trong suốt 1000×200 -> phái sinh 320x96,inside có kích thước 320×64, giữ alpha ----

    [Fact]
    public async Task Transparent_1000x200_logo_produces_320x64_inside_derivative_with_alpha()
    {
        var (token, shopId) = await CreateOwnerWithShopAsync();

        var response = await PutLogoAsync(token, shopId, "logo-alpha-1000x200.png");
        var dto = await response.Content.ReadFromJsonAsync<ShopLogoDto>(JsonOptions);

        var insideDerivative = Assert.Single(dto!.Derivatives, d => d.Preset == "320x96,inside");
        Assert.Equal(320, insideDerivative.Width);
        Assert.Equal(64, insideDerivative.Height);

        var storagePath = Path.Combine(_factory.StorageRoot, insideDerivative.StorageKey.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(storagePath));
        using var image = await SixLabors.ImageSharp.Image.LoadAsync<SixLabors.ImageSharp.PixelFormats.Rgba32>(storagePath);
        var transparentPixel = image[image.Width - 1, image.Height / 2];
        Assert.True(transparentPixel.A < 50, $"Kỳ vọng vùng trong suốt còn alpha thấp, thực tế A={transparentPixel.A}.");
    }

    // ---- upload lần hai -> LogoId đổi sang bản mới, bản cũ vẫn còn trong Library ----

    [Fact]
    public async Task Second_upload_switches_LogoId_but_keeps_old_library_record()
    {
        var (token, shopId) = await CreateOwnerWithShopAsync();

        var firstResponse = await PutLogoAsync(token, shopId, "logo1.png");
        var first = await firstResponse.Content.ReadFromJsonAsync<ShopLogoDto>(JsonOptions);

        var secondResponse = await PutLogoAsync(token, shopId, "logo2.png");
        var second = await secondResponse.Content.ReadFromJsonAsync<ShopLogoDto>(JsonOptions);

        Assert.NotEqual(first!.LibraryAsset.Id, second!.LibraryAsset.Id);

        var getResponse = await GetShopAsync(token, shopId);
        var shop = await getResponse.Content.ReadFromJsonAsync<ShopDto>(JsonOptions);
        Assert.Equal(second.LibraryAsset.Id, shop!.LogoId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var oldLibrary = await db.MediaAssets.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == first.LibraryAsset.Id);
        Assert.NotNull(oldLibrary);
        Assert.True(oldLibrary!.IsInLibrary);
        Assert.False(oldLibrary.IsDeleted);
    }

    // ---- non-Owner -> 403 MEDIA_OWNER_REQUIRED (Phase 1 chỉ có Owner tự nhiên qua CreateShop ->
    // dựng membership Staff bằng SQL trực tiếp trong test, cùng khuôn LibraryEndpointTests) ----

    [Fact]
    public async Task Upload_by_non_owner_returns_403_MEDIA_OWNER_REQUIRED()
    {
        var (ownerToken, shopId) = await CreateOwnerWithShopAsync();

        var staffEmail = NewEmail();
        var staffToken = await RegisterVerifyLoginGlobalAsync(staffEmail);
        var staffUserId = await GetUserIdAsync(staffEmail);
        await InsertStaffMembershipAsync(staffUserId, shopId);

        var response = await PutLogoAsync(staffToken, shopId, "logo.png");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("MEDIA_OWNER_REQUIRED", problem!.Extensions["error_code"]!.ToString());
    }

    // ---- caller không phải member -> 403 SHOP_ACCESS_DENIED (trước cả khi handler chạy) ----

    [Fact]
    public async Task Upload_by_non_member_returns_403_SHOP_ACCESS_DENIED()
    {
        var (_, shopAId) = await CreateOwnerWithShopAsync();
        var (tokenB, _) = await CreateOwnerWithShopAsync();

        var response = await PutLogoAsync(tokenB, shopAId, "logo.png");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("SHOP_ACCESS_DENIED", problem!.Extensions["error_code"]!.ToString());
    }

    // ---- request không phải multipart -> 415 MEDIA_MULTIPART_REQUIRED ----

    [Fact]
    public async Task Upload_without_multipart_content_type_returns_415()
    {
        var (token, shopId) = await CreateOwnerWithShopAsync();

        var request = new HttpRequestMessage(HttpMethod.Put, $"/shops/{shopId}/logo")
        {
            Content = JsonContent.Create(new { }),
            Headers = { Host = PortalHost },
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("MEDIA_MULTIPART_REQUIRED", problem!.Extensions["error_code"]!.ToString());
    }

    // ---- R4: SaveChangesAsync ném lỗi -> storage rỗng sau request, Shop.LogoId không đổi ----

    [Fact]
    public async Task Upload_when_SaveChanges_throws_leaves_storage_empty_and_LogoId_unchanged()
    {
        var (token, shopId) = await CreateOwnerWithShopAsync();

        // Decorator trên IAppDbContext (cùng khuôn UploadEndpointTests.ThrowingDbContext, T5) — KHÔNG
        // dùng ISaveChangesInterceptor: (a) chỉ override SavingChanges (sync) không chặn được đường
        // SaveChangesAsync thật sự đi qua (base SavingChangesAsync không gọi lại bản sync), (b)
        // AddDbContext<AppDbContext>(o => o.UseNpgsql(...)) không tự nạp ISaveChangesInterceptor đăng
        // ký rời trong DI container — cần EnableServiceProviderCaching/AddInterceptors tường minh mà
        // production wiring không có (và không nên đổi wiring thật chỉ để phục vụ một test). Decorator
        // ở tầng IAppDbContext chặn được CẢ HAI writer (MediaAssetWriter lẫn ShopLogoWriter) vì cả hai
        // cùng resolve IAppDbContext qua DI Scoped.
        await using var throwingFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAppDbContext>();
                services.AddScoped<IAppDbContext>(sp => new ThrowingDbContext(sp.GetRequiredService<AppDbContext>()));
            }));
        using var throwingClient = throwingFactory.CreateClient();

        using var content = BuildLogoMultipart("logo.png");
        var request = new HttpRequestMessage(HttpMethod.Put, $"/shops/{shopId}/logo") { Content = content, Headers = { Host = PortalHost } };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await throwingClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var shopDir = Path.Combine(_factory.StorageRoot, "shops", shopId.ToString());
        var filesUnderShop = Directory.Exists(shopDir)
            ? Directory.GetFiles(shopDir, "*", SearchOption.AllDirectories)
            : [];
        Assert.Empty(filesUnderShop);

        var getResponse = await GetShopAsync(token, shopId);
        var shop = await getResponse.Content.ReadFromJsonAsync<ShopDto>(JsonOptions);
        Assert.Null(shop!.LogoId);
    }

    // ---- helpers ----

    private async Task<HttpResponseMessage> PutLogoAsync(string token, Guid shopId, string testAssetFileName)
    {
        using var content = BuildLogoMultipart(testAssetFileName);
        var request = new HttpRequestMessage(HttpMethod.Put, $"/shops/{shopId}/logo") { Content = content, Headers = { Host = PortalHost } };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private static MultipartFormDataContent BuildLogoMultipart(string testAssetFileName)
    {
        var bytes = testAssetFileName.StartsWith("logo-alpha", StringComparison.Ordinal)
            ? File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestAssets", "logo-alpha-1000x200.png"))
            : EncodeJpeg(1600, 1200);

        return new MultipartFormDataContent
        {
            { new ByteArrayContent(bytes), "file", testAssetFileName },
        };
    }

    private static byte[] EncodeJpeg(int width, int height)
    {
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, height);
        image.Mutate(x => x.Fill(SixLabors.ImageSharp.Color.SeaGreen));
        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms);
        return ms.ToArray();
    }

    private Task<HttpResponseMessage> GetShopAsync(string token, Guid shopId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/shops/{shopId}") { Headers = { Host = PortalHost } };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private async Task<(string Token, Guid ShopId)> CreateOwnerWithShopAsync()
    {
        var email = NewEmail();
        var token = await RegisterVerifyLoginGlobalAsync(email);
        var createResponse = await CreateShopAsync(token, new CreateShopRequest("Logo Test Shop", NewSlug(), ShopKind.Hosted, null));
        var shop = (await createResponse.Content.ReadFromJsonAsync<ShopDto>(JsonOptions))!;
        return (token, shop.Id);
    }

    private async Task<string> RegisterVerifyLoginGlobalAsync(string email)
    {
        await PostAsync(PortalHost, "/auth/register", new RegisterRequest(email, Password, "Test User"));

        var token = _factory.EmailSpy.ExtractLastTokenFor(email);
        var verifyResponse = await SendAsync(HttpMethod.Get, $"/auth/verify-email?token={Uri.EscapeDataString(token)}", PortalHost);
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        var loginResponse = await PostAsync(PortalHost, "/auth/login", new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthTokenResult>();
        return authResult!.AccessToken;
    }

    private async Task<Guid> GetUserIdAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).FirstAsync();
        return userId;
    }

    private async Task InsertStaffMembershipAsync(Guid userId, Guid shopId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "UserShop" ("Id", "UserId", "ShopId", "RoleId", "Source", "Status", "CreatedAt", "IsDeleted")
            VALUES ({Guid.NewGuid()}, {userId}, {shopId}, {WellKnownRoles.StaffId}, 'InvitedByShop', 'Active', now(), false)
            """);
    }

    private Task<HttpResponseMessage> CreateShopAsync(string token, CreateShopRequest body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/shops")
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

    private static string NewEmail() => $"shop-logo-{Guid.NewGuid():N}@example.test";

    private static string NewSlug() => $"shop-logo-{Guid.NewGuid():N}";

    /// <summary>R4 qua HTTP thật (cùng khuôn `UploadEndpointTests.ThrowingDbContext`, T5): forward
    /// mọi DbSet cho instance thật, <see cref="SaveChangesAsync"/> luôn ném lỗi TRƯỚC khi chạm DB thật
    /// — mô phỏng "ghi file thành công, DB fail". `ShopLogoWriter` VÀ `MediaAssetWriter` đều resolve
    /// `IAppDbContext` qua DI Scoped nên cả hai đường ghi cùng thấy decorator này trong CÙNG request.</summary>
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
            throw new InvalidOperationException("Simulated DB failure (R4 endpoint test, T7).");
    }
}
