using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vsite.Api.Identity;
using Vsite.Api.Shop;
using Vsite.Application.Identity.Auth.Dtos;
using Vsite.Application.Shop.Dtos;
using Vsite.Domain.Identity;
using Vsite.Domain.Identity.Enums;
using Vsite.Domain.Shop.Enums;
using Vsite.Infrastructure.Persistence;

namespace Vsite.IntegrationTests;

/// <summary>
/// SHOP-001 §4.5 — test bắt buộc cho 4 endpoint `/shops`. Đi qua toàn bộ pipeline HTTP thật
/// (`ShopApiFactory`) vì đây là test cho middleware/policy/endpoint filter, không chỉ handler.
/// </summary>
[Collection(ShopApiCollection.Name)]
public sealed class ShopEndpointTests
{
    private const string Password = "Password123!";
    private const string PortalHost = "admin.vsite.local";

    // Server serialize enum dạng string (Quyết định #19, JsonStringEnumConverter đăng ký global ở
    // Program.cs) — HttpClient trong test không tự biết điều này, phải khai lại converter khi đọc
    // response chứa ShopKind/ShopStatus.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ShopApiFactory _factory;
    private readonly HttpClient _client;

    public ShopEndpointTests(ShopApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateShop_creates_exactly_one_UserShop_with_Owner_role_and_ShopCreator_source()
    {
        var token = await RegisterVerifyLoginGlobalAsync(NewEmail());
        var slug = NewSlug();

        var response = await CreateShopAsync(token, new CreateShopRequest("Spa ABC", slug, ShopKind.Hosted, null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var shop = await response.Content.ReadFromJsonAsync<ShopDto>(JsonOptions);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var memberships = await db.UserShops.IgnoreQueryFilters()
            .Where(us => us.ShopId == shop!.Id)
            .ToListAsync();

        var membership = Assert.Single(memberships);
        Assert.Equal(WellKnownRoles.OwnerId, membership.RoleId);
        Assert.Equal(UserShopSource.ShopCreator, membership.Source);
    }

    [Fact]
    public async Task CreateShop_rejects_duplicate_slug_with_409()
    {
        var token = await RegisterVerifyLoginGlobalAsync(NewEmail());
        var slug = NewSlug();

        var first = await CreateShopAsync(token, new CreateShopRequest("Spa A", slug, ShopKind.Hosted, null));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await CreateShopAsync(token, new CreateShopRequest("Spa B", slug, ShopKind.Hosted, null));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var problem = await second.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("SHOP_SLUG_ALREADY_TAKEN", problem!.Extensions["error_code"]!.ToString());
    }

    [Fact]
    public async Task CreateShop_rejects_reserved_slug_with_422()
    {
        var token = await RegisterVerifyLoginGlobalAsync(NewEmail());

        // "admin" nằm trong reservedSubdomains (config/reserved-routes.json) — Quyết định #8/#24.
        var response = await CreateShopAsync(token, new CreateShopRequest("Spa Reserved", "admin", ShopKind.Hosted, null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateShop_rejects_invalid_slug_format_with_422()
    {
        var token = await RegisterVerifyLoginGlobalAsync(NewEmail());

        var response = await CreateShopAsync(token, new CreateShopRequest("Spa Bad Slug", "-Not Valid_Slug-", ShopKind.Hosted, null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateShop_rejects_ExternalOnly_without_ExternalUrl_with_422()
    {
        var token = await RegisterVerifyLoginGlobalAsync(NewEmail());

        var response = await CreateShopAsync(token, new CreateShopRequest("Spa External", NewSlug(), ShopKind.ExternalOnly, null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateShop_rejects_shop_scoped_token_with_insufficient_scope()
    {
        var (shopToken, _, shopHost) = await CreateShopWithOwnerShopTokenAsync();

        // Gọi ĐÚNG host của chính shop đó — nếu không, ShopMembershipValidationMiddleware chặn
        // trước với TOKEN_SHOP_MISMATCH (token dùng sai domain), che mất INSUFFICIENT_SCOPE mà
        // test này thật sự muốn khẳng định (Quyết định #32).
        var response = await CreateShopAsync(shopToken, new CreateShopRequest("Spa Nested", NewSlug(), ShopKind.Hosted, null), shopHost);

        await AssertInsufficientScopeAsync(response);
    }

    [Fact]
    public async Task ListShops_rejects_shop_scoped_token_with_insufficient_scope()
    {
        var (shopToken, _, shopHost) = await CreateShopWithOwnerShopTokenAsync();

        var response = await SendAsync(HttpMethod.Get, "/shops", shopHost, shopToken);

        await AssertInsufficientScopeAsync(response);
    }

    [Fact]
    public async Task GetShop_rejects_shop_scoped_token_with_insufficient_scope()
    {
        var (shopToken, shopId, shopHost) = await CreateShopWithOwnerShopTokenAsync();

        var response = await SendAsync(HttpMethod.Get, $"/shops/{shopId}", shopHost, shopToken);

        await AssertInsufficientScopeAsync(response);
    }

    [Fact]
    public async Task UpdateShop_rejects_shop_scoped_token_with_insufficient_scope()
    {
        var (shopToken, shopId, shopHost) = await CreateShopWithOwnerShopTokenAsync();

        var response = await PatchAsync(shopHost, $"/shops/{shopId}", shopToken,
            new UpdateShopRequest("New Name", NewSlug(), ShopKind.Hosted, null, ShopStatus.Active));

        await AssertInsufficientScopeAsync(response);
    }

    [Fact]
    public async Task GetShop_of_another_shop_returns_403_forbidden_not_empty_404()
    {
        // SHOP-001 §3 — test tenant isolation quan trọng nhất: user chỉ là Owner của shop A gọi
        // /shops/{B} phải bị 403 (ShopMembershipEndpointFilter), không phải "trông như" 404 rỗng.
        var ownerAToken = await RegisterVerifyLoginGlobalAsync(NewEmail());
        var shopA = await CreateShopAsync(ownerAToken, new CreateShopRequest("Shop A", NewSlug(), ShopKind.Hosted, null));
        var shopAId = (await shopA.Content.ReadFromJsonAsync<ShopDto>(JsonOptions))!.Id;

        var ownerBToken = await RegisterVerifyLoginGlobalAsync(NewEmail());
        var shopB = await CreateShopAsync(ownerBToken, new CreateShopRequest("Shop B", NewSlug(), ShopKind.Hosted, null));
        var shopBId = (await shopB.Content.ReadFromJsonAsync<ShopDto>(JsonOptions))!.Id;

        var response = await SendAsync(HttpMethod.Get, $"/shops/{shopBId}", PortalHost, ownerAToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("SHOP_ACCESS_DENIED", problem!.Extensions["error_code"]!.ToString());

        // Sanity: chủ shop B tự đọc được shop của mình bình thường.
        var ownResponse = await SendAsync(HttpMethod.Get, $"/shops/{shopBId}", PortalHost, ownerBToken);
        Assert.Equal(HttpStatusCode.OK, ownResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateShop_by_non_owner_member_returns_403_SHOP_OWNER_REQUIRED()
    {
        var ownerToken = await RegisterVerifyLoginGlobalAsync(NewEmail());
        var shop = await CreateShopAsync(ownerToken, new CreateShopRequest("Shop Owner Only", NewSlug(), ShopKind.Hosted, null));
        var shopDto = (await shop.Content.ReadFromJsonAsync<ShopDto>(JsonOptions))!;

        // Thêm một membership KHÔNG phải Owner cho user khác (giả lập nhân viên, chèn thẳng DB vì
        // API mời nhân viên chưa tồn tại — ngoài phạm vi SHOP-001 §2).
        var staffEmail = NewEmail();
        var staffUserId = await RegisterVerifyGlobalAsync(staffEmail);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserShops.Add(new Vsite.Domain.Identity.Entities.UserShop
            {
                UserId = staffUserId,
                ShopId = shopDto.Id,
                RoleId = WellKnownRoles.StaffId,
                Source = UserShopSource.InvitedByShop,
            });
            await db.SaveChangesAsync();
        }

        var staffToken = await LoginGlobalAsync(staffEmail);

        var response = await PatchAsync(PortalHost, $"/shops/{shopDto.Id}", staffToken,
            new UpdateShopRequest("Renamed", shopDto.Slug, ShopKind.Hosted, null, ShopStatus.Active));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("SHOP_OWNER_REQUIRED", problem!.Extensions["error_code"]!.ToString());
    }

    [Fact]
    public async Task UpdateShop_changes_slug_and_invalidates_both_old_and_new_slug_cache()
    {
        var ownerToken = await RegisterVerifyLoginGlobalAsync(NewEmail());
        var oldSlug = NewSlug();
        var shop = await CreateShopAsync(ownerToken, new CreateShopRequest("Slug Change Shop", oldSlug, ShopKind.Hosted, null));
        var shopDto = (await shop.Content.ReadFromJsonAsync<ShopDto>(JsonOptions))!;
        var newSlug = NewSlug();

        var response = await PatchAsync(PortalHost, $"/shops/{shopDto.Id}", ownerToken,
            new UpdateShopRequest(shopDto.Name, newSlug, shopDto.Kind, shopDto.ExternalUrl, ShopStatus.Active));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invalidated = _factory.ShopLookupSpy.InvalidatedSlugs;
        Assert.Contains(oldSlug, invalidated);
        Assert.Contains(newSlug, invalidated);
    }

    [Fact(Skip = "Listing chưa tồn tại — mở lại ở module Marketplace (04 §2.2)")]
    public Task UpdateShop_Hosted_to_ExternalOnly_must_reassign_or_unpublish_listings_targeting_website()
    {
        // 04 §2.2 — đổi Kind Hosted → ExternalOnly PHẢI chuyển/gỡ mọi Listing đang trỏ
        // ShopHome/ShopPage, nếu không sẽ 404 cho khách đến từ vsite. Listing thuộc module
        // Marketplace, chưa tồn tại ở SHOP-001 — ràng buộc này để RỖNG có chủ đích, xem
        // Docs/tasks/SHOP-001/changelog.md.
        throw new NotImplementedException();
    }

    private static async Task AssertInsufficientScopeAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("INSUFFICIENT_SCOPE", problem!.Extensions["error_code"]!.ToString());
    }

    private async Task<(string ShopToken, Guid ShopId, string ShopHost)> CreateShopWithOwnerShopTokenAsync()
    {
        var ownerEmail = NewEmail();
        var globalToken = await RegisterVerifyLoginGlobalAsync(ownerEmail);
        var createResponse = await CreateShopAsync(globalToken, new CreateShopRequest("Nested Shop", NewSlug(), ShopKind.Hosted, null));
        var shop = (await createResponse.Content.ReadFromJsonAsync<ShopDto>(JsonOptions))!;
        var shopHost = $"{shop.Slug}.vsite.local";

        // 03 §6.1 — đăng ký một membership MỚI trên chính host của shop này để có token audience
        // `shop:{shopId}` thật (không tái dùng token global — hai audience khác nhau).
        var shopEmail = NewEmail();
        var shopToken = await RegisterVerifyLoginGlobalAsync(shopEmail, shopHost);

        return (shopToken, shop.Id, shopHost);
    }

    private async Task<Guid> RegisterVerifyGlobalAsync(string email)
    {
        await PostAsync(PortalHost, "/auth/register", new RegisterRequest(email, Password, "Staff User"));
        var token = _factory.EmailSpy.ExtractLastTokenFor(email);
        var verifyResponse = await SendAsync(HttpMethod.Get, $"/auth/verify-email?token={Uri.EscapeDataString(token)}", PortalHost);
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.Where(u => u.EmailNormalized == email.Trim().ToUpperInvariant()).Select(u => u.Id).SingleAsync();
    }

    private async Task<string> LoginGlobalAsync(string email)
    {
        var loginResponse = await PostAsync(PortalHost, "/auth/login", new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthTokenResult>();
        return authResult!.AccessToken;
    }

    private async Task<string> RegisterVerifyLoginGlobalAsync(string email, string? host = null)
    {
        var effectiveHost = host ?? PortalHost;
        await PostAsync(effectiveHost, "/auth/register", new RegisterRequest(email, Password, "Test User"));

        var token = _factory.EmailSpy.ExtractLastTokenFor(email);
        var verifyResponse = await SendAsync(HttpMethod.Get, $"/auth/verify-email?token={Uri.EscapeDataString(token)}", effectiveHost);
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        var loginResponse = await PostAsync(effectiveHost, "/auth/login", new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthTokenResult>();
        return authResult!.AccessToken;
    }

    private Task<HttpResponseMessage> CreateShopAsync(string token, CreateShopRequest body, string? host = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/shops")
        {
            Content = JsonContent.Create(body),
            Headers = { Host = host ?? PortalHost },
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PatchAsync<TBody>(string host, string path, string token, TBody body)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, path)
        {
            Content = JsonContent.Create(body),
            Headers = { Host = host },
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PostAsync<TBody>(string? host, string path, TBody body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (host is not null)
        {
            request.Headers.Host = host;
        }

        return await _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? host, string? token = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (host is not null)
        {
            request.Headers.Host = host;
        }

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return _client.SendAsync(request);
    }

    private static string NewEmail() => $"shop-{Guid.NewGuid():N}@example.test";

    private static string NewSlug() => $"shop-{Guid.NewGuid():N}";
}
