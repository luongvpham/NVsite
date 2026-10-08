using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vsite.Api.Identity;
using Vsite.Application.Identity.Auth.Dtos;
using Vsite.Application.Identity.Auth.Queries.GetMe;
using Vsite.Domain.Identity.Entities;
using Vsite.Domain.Shop.Entities;
using Vsite.Domain.Shop.Enums;
using Vsite.Infrastructure.Persistence;

namespace Vsite.IntegrationTests;

/// <summary>
/// Phase 3, test bắt buộc theo `03` §7.1 (tài liệu tự nhận "quan trọng nhất") — với MỖI endpoint
/// identity yêu cầu authentication, một test khẳng định token `shop:{shopId}` có bị từ chối đúng
/// như bảng năng lực Quyết định #32 hay không:
///   - `GET /auth/me`               → CHO PHÉP mọi audience.
///   - `POST /auth/me/change-password` → CHO PHÉP mọi audience (đổi đúng password của scope đó).
///
/// `GET /auth/me/shops` đã bị xoá ở SHOP-001 (thay bằng `GET /shops`, xem
/// `Docs/tasks/SHOP-001/contract-diff.md` mục 1) — test `RequireGlobalScope` cho vai trò này giờ
/// nằm ở test của module Shop, không còn ở đây.
///
/// Đi qua toàn bộ pipeline HTTP thật (`IdentityApiFactory`) — không gọi handler trực tiếp — vì
/// đây là test cho chính middleware/policy, không phải cho business logic của handler.
/// </summary>
[Collection(IdentityApiCollection.Name)]
public sealed class TokenScopeTests : IAsyncLifetime
{
    private const string Password = "Password123!";

    private readonly IdentityApiFactory _factory;
    private readonly HttpClient _client;

    private string _mainToken = null!;
    private string _shopToken = null!;
    private string _shopHost = null!;

    public TokenScopeTests(IdentityApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        var shopId = Guid.NewGuid();
        var slug = $"tokenscope-{shopId:N}";
        _shopHost = $"{slug}.vsite.local";

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Shops.Add(new Shop(shopId, "Token Scope Test Shop", slug, ShopKind.Hosted, status: ShopStatus.Active));
            await db.SaveChangesAsync();
        }

        var mainEmail = $"main-{Guid.NewGuid():N}@example.test";
        _mainToken = await RegisterVerifyLoginAsync(host: null, mainEmail, Password);

        var shopEmail = $"shop-{Guid.NewGuid():N}@example.test";
        _shopToken = await RegisterVerifyLoginAsync(host: _shopHost, shopEmail, Password);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetMe_allows_main_token()
    {
        var response = await SendAsync(HttpMethod.Get, "/auth/me", host: null, token: _mainToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MeDto>();
        Assert.Equal("vsite-main", body!.Audience);
    }

    [Fact]
    public async Task GetMe_allows_shop_token()
    {
        var response = await SendAsync(HttpMethod.Get, "/auth/me", host: _shopHost, token: _shopToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MeDto>();
        Assert.StartsWith("shop:", body!.Audience);
    }

    [Fact]
    public async Task ChangePassword_allows_main_token()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/me/change-password")
        {
            Content = JsonContent.Create(new ChangePasswordRequest(Password, "NewPassword123!")),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _mainToken);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_allows_shop_token()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/me/change-password")
        {
            Content = JsonContent.Create(new ChangePasswordRequest(Password, "NewPassword123!")),
            Headers = { Host = _shopHost },
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _shopToken);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // ---- Review sau Gate 2, D1: 415 của route JSON (KHÔNG multipart) không được gắn
    // error_code MEDIA_MULTIPART_REQUIRED — routing tự trả 415 rỗng body cho MỌI endpoint có
    // IAcceptsMetadata không khớp Content-Type (kể cả JSON suy luận từ tham số phức hợp như
    // LoginRequest ở đây), không riêng multipart. UnsupportedMediaTypeStatusCodeHandler phải phân
    // biệt bằng MultipartRouteMatcher, không phải gắn cứng cho mọi 415. ----

    [Fact]
    public async Task Login_with_non_json_content_type_returns_415_without_multipart_error_code()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login")
        {
            Content = new StringContent("not json", System.Text.Encoding.UTF8, "text/plain"),
        };

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("MEDIA_MULTIPART_REQUIRED", body, StringComparison.Ordinal);
        Assert.Empty(body);
    }

    private async Task<string> RegisterVerifyLoginAsync(string? host, string email, string password)
    {
        await PostAsync(host, "/auth/register", new RegisterRequest(email, password, "Test User"));

        var token = _factory.EmailSpy.ExtractLastTokenFor(email);
        var verifyResponse = await SendAsync(HttpMethod.Get, $"/auth/verify-email?token={Uri.EscapeDataString(token)}", host);
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        var loginResponse = await PostAsync(host, "/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthTokenResult>();
        return authResult!.AccessToken;
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
}
