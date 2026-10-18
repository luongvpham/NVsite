using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vsite.Api.Identity;
using Vsite.Api.Shop;
using Vsite.Application.Identity.Auth.Dtos;
using Vsite.Application.Media.Dtos;
using Vsite.Application.Shop.Dtos;
using Vsite.Domain.Identity;
using Vsite.Domain.Shop.Enums;
using Vsite.Infrastructure.Persistence;
using Vsite.IntegrationTests.Media;

namespace Vsite.IntegrationTests;

/// <summary>
/// Khung HTTP dùng chung cho test endpoint cần người dùng + shop + membership đủ role (WEBSITE-001 T10, T10b R16):
/// đăng ký/đăng nhập token global, tạo shop (Owner), dựng membership role khác bằng SQL (Phase 1 chưa có "mời thành viên").
/// Chạy trên <see cref="MediaApiFactory"/> (Postgres + Redis Testcontainers — CẦN Docker).
/// </summary>
public sealed class ShopApiHarness(MediaApiFactory factory)
{
    public const string Password = "Password123!";
    public const string PortalHost = "admin.vsite.local";

    public static readonly System.Text.Json.JsonSerializerOptions Json =
        new(System.Text.Json.JsonSerializerDefaults.Web)
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        };

    public MediaApiFactory Factory { get; } = factory;

    public HttpClient Client { get; } = factory.CreateClient();

    public sealed record TestUser(string Email, Guid UserId, string Token);

    public async Task<TestUser> NewUserAsync()
    {
        var email = $"api-{Guid.NewGuid():N}@example.test";
        await PostJsonAsync("/api/auth/register", null, new RegisterRequest(email, Password, "Test User"));
        var verifyToken = Factory.EmailSpy.ExtractLastTokenFor(email);
        var verify = await SendAsync(HttpMethod.Get, $"/api/auth/verify-email?token={Uri.EscapeDataString(verifyToken)}", null);
        Assert.Equal(System.Net.HttpStatusCode.OK, verify.StatusCode);
        var login = await PostJsonAsync("/api/auth/login", null, new LoginRequest(email, Password));
        Assert.Equal(System.Net.HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<AuthTokenResult>())!.AccessToken;

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).FirstAsync();
        return new TestUser(email, userId, token);
    }

    /// <summary>Người dùng mới + shop Hosted do họ tạo (Owner).</summary>
    public async Task<(TestUser Owner, Guid ShopId)> NewOwnerWithShopAsync()
    {
        var owner = await NewUserAsync();
        var response = await PostJsonAsync(
            "/api/shops", owner.Token, new CreateShopRequest("Harness Shop", $"harness-{Guid.NewGuid():N}", ShopKind.Hosted, null));
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var shop = (await response.Content.ReadFromJsonAsync<ShopDto>(Json))!;
        return (owner, shop.Id);
    }

    /// <summary>Người dùng mới có membership Active với <paramref name="roleId"/> vào shop (SQL trực tiếp).</summary>
    public async Task<TestUser> NewMemberAsync(Guid shopId, Guid roleId)
    {
        var user = await NewUserAsync();
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO user_shop (id, user_id, shop_id, role_id, source, status, created_at, is_deleted)
            VALUES ({Guid.NewGuid()}, {user.UserId}, {shopId}, {roleId}, 'InvitedByShop', 'Active', now(), false)
            """);
        return user;
    }

    public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? token, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = content, Headers = { Host = PortalHost } };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return Client.SendAsync(request);
    }

    public Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string? token, object? body) =>
        SendAsync(method, path, token, body is null ? null : JsonContent.Create(body));

    /// <summary>Body JSON thô (văn bản nguyên văn) — để gửi tree/body hỏng hoặc rất lớn.</summary>
    public Task<HttpResponseMessage> SendRawJsonAsync(HttpMethod method, string path, string? token, string json) =>
        SendAsync(method, path, token, new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

    public Task<HttpResponseMessage> PostJsonAsync(string path, string? token, object body) =>
        SendJsonAsync(HttpMethod.Post, path, token, body);

    public async Task<MediaAssetDto> UploadLibraryAsync(string token, Guid shopId)
    {
        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(Imaging.TestImages.Jpeg(1600, 1200)), "file", "photo.jpg" },
        };
        var response = await SendAsync(HttpMethod.Post, $"/api/shops/{shopId}/media/library", token, content);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MediaAssetDto>(Json))!;
    }

    public async Task<MediaAssetDto> CloneAsync(string token, Guid shopId, Guid libraryId)
    {
        var response = await PostJsonAsync(
            $"/api/shops/{shopId}/media/library/{libraryId}/clones", token, new { preset = "800x600,cover", focalX = (float?)null, focalY = (float?)null });
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MediaAssetDto>(Json))!;
    }
}
