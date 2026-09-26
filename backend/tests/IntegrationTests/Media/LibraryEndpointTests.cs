using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
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
using Vsite.Domain.Identity;
using Vsite.Domain.Pagination;
using Vsite.Domain.Shop.Enums;
using Vsite.Infrastructure.Persistence;

namespace Vsite.IntegrationTests.Media;

/// <summary>
/// T6, MEDIA-001 (#55, #71, #72) — test bắt buộc cho nhóm endpoint `library`/`assets`/`usage`
/// (`task-T6-brief.md`). Đi qua pipeline HTTP thật (`MediaApiFactory`, Postgres + Redis
/// Testcontainers) — CẦN Docker daemon, xem `Docs/DOCKER-TEST-DEBT.md`. Verify hành vi handler KHÔNG
/// cần Docker ở `LibraryHandlerTests` (EF InMemory) cùng thư mục.
///
/// Chạy (máy CÓ Docker daemon):
/// <c>dotnet test backend/tests/IntegrationTests --filter "FullyQualifiedName~LibraryEndpointTests"</c>.
/// </summary>
[Collection(MediaApiCollection.Name)]
public sealed class LibraryEndpointTests
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

    public LibraryEndpointTests(MediaApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ---- Test bắt buộc 5: clone -> record mới, StorageKey khác bản Library, SourceAssetId đúng,
    // kích thước đúng preset ----

    [Fact]
    public async Task Clone_creates_new_record_with_different_key_and_correct_preset_size()
    {
        var (token, shopId) = await CreateOwnerWithShopAsync();
        var library = await UploadLibraryAsync(token, shopId);

        var response = await PostCloneAsync(token, shopId, library.Id, "800x600,cover");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var clone = await response.Content.ReadFromJsonAsync<MediaAssetDto>(JsonOptions);
        Assert.NotEqual(library.StorageKey, clone!.StorageKey);
        Assert.Equal(library.Id, clone.SourceAssetId);
        Assert.Equal("800x600,cover", clone.Preset);
        Assert.Equal(800, clone.Width);
        Assert.Equal(600, clone.Height);
    }

    // ---- clone từ id là clone -> 404 (không lộ tồn tại) ----

    [Fact]
    public async Task Clone_of_a_clone_returns_404()
    {
        var (token, shopId) = await CreateOwnerWithShopAsync();
        var library = await UploadLibraryAsync(token, shopId);
        var cloneResponse = await PostCloneAsync(token, shopId, library.Id, "800x600,cover");
        var clone = await cloneResponse.Content.ReadFromJsonAsync<MediaAssetDto>(JsonOptions);

        var response = await PostCloneAsync(token, shopId, clone!.Id, "800x600,cover");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- clone asset của shop B -> 404 ----

    [Fact]
    public async Task Clone_of_another_shops_asset_returns_404()
    {
        var (tokenA, shopAId) = await CreateOwnerWithShopAsync();
        var (tokenB, _) = await CreateOwnerWithShopAsync();
        var libraryA = await UploadLibraryAsync(tokenA, shopAId);

        var response = await PostCloneAsync(tokenB, shopAId, libraryA.Id, "800x600,cover");

        // Membership của tokenB vào shopAId không tồn tại -> 403 SHOP_ACCESS_DENIED trước cả khi
        // handler chạy (RequireShopMembership()) — đây KHÔNG phải case "clone asset shop khác" mà
        // brief mô tả (đó là khi CÙNG shop nhưng asset thuộc shop khác đã lọt qua route). Test dưới
        // (`Clone_of_asset_belonging_to_a_different_shop_but_caller_is_member_returns_404`) mới đúng
        // kịch bản "asset của shop B" khi caller LÀ member của shop đích route.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Kịch bản đúng của brief: caller LÀ member hợp lệ của route `shopId` (A), nhưng
    /// `assetId` truyền vào thuộc shop B — Global Query Filter (ràng theo `TenantContext.ShopId` =
    /// A, set bởi `RequireShopMembership()`) khiến asset B "biến mất" khỏi MỘT query load nguồn, nên
    /// 404 chứ không phải 403.</summary>
    [Fact]
    public async Task Clone_of_asset_belonging_to_a_different_shop_but_caller_is_member_returns_404()
    {
        var (tokenA, shopAId) = await CreateOwnerWithShopAsync();
        var (tokenB, shopBId) = await CreateOwnerWithShopAsync();
        var libraryB = await UploadLibraryAsync(tokenB, shopBId);

        var response = await PostCloneAsync(tokenA, shopAId, libraryB.Id, "800x600,cover");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Test bắt buộc 9: xoá bản Library -> list không còn; GetAssetsByIds([cloneId]) vẫn trả
    // clone, file clone vẫn mở được (qua IObjectStorage — HTTP /media/* là T9, chưa tồn tại) ----

    [Fact]
    public async Task Delete_library_excludes_it_from_list_but_clone_still_resolves_and_file_still_exists()
    {
        var (token, shopId) = await CreateOwnerWithShopAsync();
        var library = await UploadLibraryAsync(token, shopId);
        var cloneResponse = await PostCloneAsync(token, shopId, library.Id, "800x600,cover");
        var clone = (await cloneResponse.Content.ReadFromJsonAsync<MediaAssetDto>(JsonOptions))!;

        var deleteResponse = await DeleteAsync(token, shopId, library.Id);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var listResponse = await GetLibraryAsync(token, shopId);
        var page = await listResponse.Content.ReadFromJsonAsync<PagedResult<MediaAssetDto>>(JsonOptions);
        Assert.DoesNotContain(page!.Items, a => a.Id == library.Id);

        var lookupResponse = await GetAssetsByIdsAsync(token, shopId, [clone.Id]);
        var found = await lookupResponse.Content.ReadFromJsonAsync<List<MediaAssetDto>>(JsonOptions);
        var foundClone = Assert.Single(found!);
        Assert.Equal(clone.Id, foundClone.Id);

        // File clone vẫn mở được qua IObjectStorage — HTTP `/media/{key}` là T9, chưa tồn tại
        // (Ruling task-T6-brief.md: assert bằng OpenReadAsync/tồn tại file trên đĩa, không phải HTTP).
        var storagePath = Path.Combine(_factory.StorageRoot, foundClone.StorageKey.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(storagePath));
    }

    // ---- delete bởi non-Owner -> 403 MEDIA_OWNER_REQUIRED (Phase 1 chỉ có Owner tự nhiên qua
    // CreateShop -> dựng membership Staff bằng SQL trực tiếp trong test) ----

    [Fact]
    public async Task Delete_by_non_owner_returns_403_MEDIA_OWNER_REQUIRED()
    {
        var (ownerToken, shopId) = await CreateOwnerWithShopAsync();
        var library = await UploadLibraryAsync(ownerToken, shopId);

        var staffEmail = NewEmail();
        var staffToken = await RegisterVerifyLoginGlobalAsync(staffEmail);
        var staffUserId = await GetUserIdAsync(staffEmail);
        await InsertStaffMembershipAsync(staffUserId, shopId);

        var response = await DeleteAsync(staffToken, shopId, library.Id);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("MEDIA_OWNER_REQUIRED", problem!.Extensions["error_code"]!.ToString());
    }

    // ---- usage trước/sau clone và trước/sau upload logo (sinh phái sinh) KHÔNG đổi; sau upload
    // thẳng thì tăng đúng SizeBytes ----

    [Fact]
    public async Task Usage_unchanged_after_clone_but_increases_after_direct_upload()
    {
        var (token, shopId) = await CreateOwnerWithShopAsync();

        var before = await GetUsageAsync(token, shopId);
        Assert.Equal(0, before.UsedBytes);

        var library = await UploadLibraryAsync(token, shopId);
        var afterLibrary = await GetUsageAsync(token, shopId);
        Assert.Equal(library.SizeBytes, afterLibrary.UsedBytes);

        await PostCloneAsync(token, shopId, library.Id, "800x600,cover");
        var afterClone = await GetUsageAsync(token, shopId);
        Assert.Equal(afterLibrary.UsedBytes, afterClone.UsedBytes);

        var directResult = await PostSlotUploadAsync(token, shopId, "800x600,cover", saveToLibrary: false);
        var afterDirect = await GetUsageAsync(token, shopId);
        Assert.Equal(afterClone.UsedBytes + directResult.Asset.SizeBytes, afterDirect.UsedBytes);
    }

    // ---- list phân trang đúng shape; không chứa record IsInLibrary=false ----

    [Fact]
    public async Task List_paginates_correctly_and_excludes_non_library_records()
    {
        var (token, shopId) = await CreateOwnerWithShopAsync();
        await UploadLibraryAsync(token, shopId);
        await UploadLibraryAsync(token, shopId);
        await UploadLibraryAsync(token, shopId);
        await PostSlotUploadAsync(token, shopId, "800x600,cover", saveToLibrary: false);

        var response = await GetLibraryAsync(token, shopId, page: 1, pageSize: 2);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<MediaAssetDto>>(JsonOptions);

        Assert.Equal(3, page!.Total);
        Assert.Equal(2, page.Items.Count);
        Assert.All(page.Items, a => Assert.True(a.IsInLibrary));
    }

    // ---- GetAssetsByIds chứa id của shop B -> id đó KHÔNG có trong kết quả ----

    [Fact]
    public async Task GetAssetsByIds_excludes_ids_belonging_to_another_shop()
    {
        var (tokenA, shopAId) = await CreateOwnerWithShopAsync();
        var (tokenB, shopBId) = await CreateOwnerWithShopAsync();
        var libraryA = await UploadLibraryAsync(tokenA, shopAId);
        var libraryB = await UploadLibraryAsync(tokenB, shopBId);

        var response = await GetAssetsByIdsAsync(tokenA, shopAId, [libraryA.Id, libraryB.Id]);
        var found = await response.Content.ReadFromJsonAsync<List<MediaAssetDto>>(JsonOptions);

        var single = Assert.Single(found!);
        Assert.Equal(libraryA.Id, single.Id);
    }

    // ---- references: chưa là logo -> rỗng ----

    [Fact]
    public async Task References_returns_empty_when_asset_is_not_referenced()
    {
        var (token, shopId) = await CreateOwnerWithShopAsync();
        var library = await UploadLibraryAsync(token, shopId);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/shops/{shopId}/media/library/{library.Id}/references")
        {
            Headers = { Host = PortalHost },
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<MediaReferencesDto>(JsonOptions);
        Assert.Empty(dto!.References);
    }

    // ---- helpers ----

    private async Task<HttpResponseMessage> PostCloneAsync(string token, Guid shopId, Guid assetId, string preset)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/shops/{shopId}/media/library/{assetId}/clones")
        {
            Content = JsonContent.Create(new { preset, focalX = (float?)null, focalY = (float?)null }),
            Headers = { Host = PortalHost },
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> DeleteAsync(string token, Guid shopId, Guid assetId)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/shops/{shopId}/media/library/{assetId}")
        {
            Headers = { Host = PortalHost },
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> GetLibraryAsync(string token, Guid shopId, int page = 1, int pageSize = 24)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/shops/{shopId}/media/library?page={page}&pageSize={pageSize}")
        {
            Headers = { Host = PortalHost },
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> GetAssetsByIdsAsync(string token, Guid shopId, IReadOnlyList<Guid> ids)
    {
        var query = string.Join("&", ids.Select(id => $"ids={id}"));
        var request = new HttpRequestMessage(HttpMethod.Get, $"/shops/{shopId}/media/assets?{query}")
        {
            Headers = { Host = PortalHost },
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<MediaUsageDto> GetUsageAsync(string token, Guid shopId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/shops/{shopId}/media/usage")
        {
            Headers = { Host = PortalHost },
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        return (await response.Content.ReadFromJsonAsync<MediaUsageDto>(JsonOptions))!;
    }

    private async Task<MediaAssetDto> UploadLibraryAsync(string token, Guid shopId)
    {
        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(EncodeJpeg(1600, 1200)), "file", "photo.jpg" },
        };
        var request = new HttpRequestMessage(HttpMethod.Post, $"/shops/{shopId}/media/library") { Content = content, Headers = { Host = PortalHost } };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        return (await response.Content.ReadFromJsonAsync<MediaAssetDto>(JsonOptions))!;
    }

    private async Task<SlotUploadResultDto> PostSlotUploadAsync(string token, Guid shopId, string preset, bool saveToLibrary)
    {
        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(EncodeJpeg(1600, 1200)), "file", "photo.jpg" },
            { new StringContent(preset), "preset" },
            { new StringContent("0.5"), "focalX" },
            { new StringContent("0.5"), "focalY" },
            { new StringContent(saveToLibrary.ToString()), "saveToLibrary" },
        };
        var request = new HttpRequestMessage(HttpMethod.Post, $"/shops/{shopId}/media/slot-uploads") { Content = content, Headers = { Host = PortalHost } };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        return (await response.Content.ReadFromJsonAsync<SlotUploadResultDto>(JsonOptions))!;
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

    /// <summary>Ruling task-T6-brief.md: "Phase 1 chỉ có Owner, nên dựng membership role khác bằng
    /// SQL trong test" — không có endpoint "invite member" ở Phase 1, insert thẳng bằng SQL.</summary>
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

    private static string NewEmail() => $"media-lib-{Guid.NewGuid():N}@example.test";

    private static string NewSlug() => $"media-lib-shop-{Guid.NewGuid():N}";
}
