using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Vsite.Api.Identity;
using Vsite.Application.Identity.Auth.Dtos;
using Vsite.Domain.Identity;
using Vsite.Domain.Identity.Entities;
using Vsite.Domain.Identity.Enums;
using Vsite.Domain.Shop.Entities;
using Vsite.Domain.Shop.Enums;
using Vsite.Infrastructure.Persistence;

namespace Vsite.IntegrationTests;

/// <summary>
/// REFACTOR-AUTHZ-001 — membership đã xoá mềm / không còn `Active`, và `User` bị đình chỉ, phải
/// mất quyền ở MỌI luồng auth. Trước task này các handler Identity đọc `UserShops.IgnoreQueryFilters()`
/// không lọc gì, nên tất cả các test dưới đây đều đỏ. Đi qua pipeline HTTP thật (`IdentityApiFactory`).
///
/// Quyết định người duyệt chốt (xem `Docs/tasks/REFACTOR-AUTHZ-001/changelog.md`):
/// đăng ký lại sau khi membership bị xoá mềm → khôi phục; đang Suspended → chặn 409; refresh khi
/// membership/tài khoản hết hiệu lực → 401 + thu hồi token cùng scope.
/// </summary>
[Collection(IdentityApiCollection.Name)]
public sealed class MembershipLifecycleTests(IdentityApiFactory factory)
{
    private const string Password = "Password123!";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Login_at_shop_is_rejected_once_membership_is_soft_deleted_or_suspended()
    {
        var host = await CreateShopAsync();
        var deleted = await RegisterAtShopAsync(host);
        var suspended = await RegisterAtShopAsync(host);

        await MutateMembershipAsync(deleted, us => us.IsDeleted = true);
        await SetMembershipStatusAsync(suspended, UserShopStatus.Suspended);

        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(host, deleted)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(host, suspended)).StatusCode);
    }

    [Fact]
    public async Task Suspended_user_cannot_log_in_or_refresh()
    {
        var email = await RegisterAsync(host: null);
        var tokens = await ReadTokensAsync(await LoginAsync(null, email));

        await SuspendUserAsync(email);

        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(null, email)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(null, tokens.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Refresh_of_shop_token_is_rejected_and_scope_revoked_after_membership_is_suspended()
    {
        var host = await CreateShopAsync();
        var email = await RegisterAtShopAsync(host);
        var first = await ReadTokensAsync(await LoginAsync(host, email));
        var second = await ReadTokensAsync(await LoginAsync(host, email));

        await SetMembershipStatusAsync(email, UserShopStatus.Suspended);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(host, first.RefreshToken)).StatusCode);

        // Cả token còn lại cùng scope cũng bị thu hồi — khôi phục membership cũng không dùng lại được.
        await SetMembershipStatusAsync(email, UserShopStatus.Active);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(host, second.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task OwnerShopIds_claim_excludes_soft_deleted_and_suspended_owner_memberships()
    {
        var email = await RegisterAsync(host: null);
        Guid live, deleted, suspended;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userId = (await db.Users.SingleAsync(u => u.EmailNormalized == email.ToUpperInvariant())).Id;
            (live, deleted, suspended) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            foreach (var shopId in new[] { live, deleted, suspended })
            {
                db.Shops.Add(new Shop(shopId, "Owner", $"owner-{shopId:N}", ShopKind.Hosted));
            }

            db.UserShops.AddRange(
                Owner(userId, live),
                new UserShop { UserId = userId, ShopId = deleted, RoleId = WellKnownRoles.OwnerId, Source = UserShopSource.ShopCreator, IsDeleted = true },
                new UserShop { UserId = userId, ShopId = suspended, RoleId = WellKnownRoles.OwnerId, Source = UserShopSource.ShopCreator, Status = UserShopStatus.Suspended });
            await db.SaveChangesAsync();
        }

        var tokens = await ReadTokensAsync(await LoginAsync(null, email));

        var claims = new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken).Claims
            .Where(c => c.Type == "ownerShopIds").Select(c => Guid.Parse(c.Value)).ToList();
        Assert.Equal([live], claims);
    }

    [Fact]
    public async Task ForgotPassword_at_shop_sends_nothing_for_soft_deleted_membership()
    {
        var host = await CreateShopAsync();
        var email = await RegisterAtShopAsync(host);
        await MutateMembershipAsync(email, us => us.IsDeleted = true);

        var response = await PostAsync(host, "/api/auth/forgot-password", new ForgotPasswordRequest(email));

        Assert.True(response.IsSuccessStatusCode);
        Assert.Throws<InvalidOperationException>(() => ExtractResetToken(email));
    }

    [Fact]
    public async Task ResetPassword_token_stops_working_when_membership_is_suspended_after_it_was_issued()
    {
        var host = await CreateShopAsync();
        var email = await RegisterAtShopAsync(host);
        await PostAsync(host, "/api/auth/forgot-password", new ForgotPasswordRequest(email));
        var resetToken = ExtractResetToken(email);

        await SetMembershipStatusAsync(email, UserShopStatus.Suspended);

        var response = await PostAsync(host, "/api/auth/reset-password", new ResetPasswordRequest(resetToken, "NewPassword123!"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_again_after_membership_soft_deleted_restores_it_as_active_customer_with_new_password()
    {
        var host = await CreateShopAsync();
        var email = await RegisterAtShopAsync(host);
        var oldSession = await ReadTokensAsync(await LoginAsync(host, email));

        // Trước khi bị xoá: Owner + Suspended — khôi phục phải về Customer + Active, không giữ quyền cũ.
        await SetMembershipColumnsAsync(email, WellKnownRoles.OwnerId, UserShopStatus.Suspended);
        Guid membershipId = default;
        await MutateMembershipAsync(email, us =>
        {
            membershipId = us.Id;
            us.IsDeleted = true;
        });

        const string newPassword = "Another123!";
        await PostAsync(host, "/api/auth/register", new RegisterRequest(email, newPassword, "Again"));
        var verify = await GetAsync(host, $"/api/auth/verify-email?token={Uri.EscapeDataString(factory.EmailSpy.ExtractLastTokenFor(email))}");
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(host, email, newPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(host, email, Password)).StatusCode);

        // Refresh token phát TRƯỚC khi bị xoá, chưa từng dùng, không được sống lại sau khi khôi phục.
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(host, oldSession.RefreshToken)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var restored = await db.UserShops.IgnoreQueryFilters().SingleAsync(us => us.Id == membershipId);
        Assert.False(restored.IsDeleted);
        Assert.Null(restored.DeletedAt);
        Assert.Equal(UserShopStatus.Active, restored.Status);
        Assert.Equal(WellKnownRoles.CustomerId, restored.RoleId);
        Assert.Equal(UserShopSource.RegisteredOnShop, restored.Source);
    }

    [Fact]
    public async Task Register_again_while_membership_is_suspended_is_conflict()
    {
        var host = await CreateShopAsync();
        var email = await RegisterAtShopAsync(host);
        await SetMembershipStatusAsync(email, UserShopStatus.Suspended);

        var response = await PostAsync(host, "/api/auth/register", new RegisterRequest(email, "Another123!", "Again"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_again_while_membership_is_invited_is_conflict()
    {
        var host = await CreateShopAsync();
        var email = await RegisterAtShopAsync(host);
        await SetMembershipStatusAsync(email, UserShopStatus.Invited);

        var response = await PostAsync(host, "/api/auth/register", new RegisterRequest(email, "Another123!", "Again"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_token_stops_working_when_membership_is_soft_deleted_after_it_was_issued()
    {
        var host = await CreateShopAsync();
        var email = await RegisterAtShopAsync(host);
        await PostAsync(host, "/api/auth/forgot-password", new ForgotPasswordRequest(email));
        var resetToken = ExtractResetToken(email);

        await MutateMembershipAsync(email, us => us.IsDeleted = true);

        var response = await PostAsync(host, "/api/auth/reset-password", new ResetPasswordRequest(resetToken, "NewPassword123!"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_token_stops_working_when_user_is_suspended_after_it_was_issued()
    {
        var email = await RegisterAsync(host: null);
        await PostAsync(null, "/api/auth/forgot-password", new ForgotPasswordRequest(email));
        var resetToken = ExtractResetToken(email);

        await SuspendUserAsync(email);

        var response = await PostAsync(null, "/api/auth/reset-password", new ResetPasswordRequest(resetToken, "NewPassword123!"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Shop_access_token_of_suspended_user_is_rejected_on_next_request()
    {
        var host = await CreateShopAsync();
        var email = await RegisterAtShopAsync(host);
        var tokens = await ReadTokensAsync(await LoginAsync(host, email));
        Assert.Equal(HttpStatusCode.OK, (await GetMeAsync(host, tokens.AccessToken)).StatusCode);

        await SuspendUserAsync(email);

        // ShopMembershipValidationMiddleware kiểm lại mỗi request — không đợi access token hết hạn.
        Assert.Equal(HttpStatusCode.Forbidden, (await GetMeAsync(host, tokens.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(host, tokens.RefreshToken)).StatusCode);
    }

    // ---- helpers ----

    private async Task SuspendUserAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.EmailNormalized == email.ToUpperInvariant());
        user.Status = UserStatus.Suspended;
        await db.SaveChangesAsync();
    }

    private Task<HttpResponseMessage> GetMeAsync(string host, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Host = host;
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return _client.SendAsync(request);
    }

    private async Task SetMembershipColumnsAsync(string email, Guid roleId, UserShopStatus status)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE user_shop SET role_id = {roleId}, status = {status.ToString()}
            WHERE user_id = (SELECT id FROM app_user WHERE email_normalized = {email.ToUpperInvariant()})
            """);
        Assert.Equal(1, rows);
    }

    private static UserShop Owner(Guid userId, Guid shopId) =>
        new() { UserId = userId, ShopId = shopId, RoleId = WellKnownRoles.OwnerId, Source = UserShopSource.ShopCreator };

    private async Task<string> CreateShopAsync()
    {
        var shopId = Guid.NewGuid();
        var slug = $"lifecycle-{shopId:N}";
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Shops.Add(new Shop(shopId, "Lifecycle Shop", slug, ShopKind.Hosted, status: ShopStatus.Active));
        await db.SaveChangesAsync();
        return $"{slug}.vsite.local";
    }

    private Task<string> RegisterAtShopAsync(string host) => RegisterAsync(host);

    private async Task<string> RegisterAsync(string? host)
    {
        var email = $"lc-{Guid.NewGuid():N}@example.test";
        await PostAsync(host, "/api/auth/register", new RegisterRequest(email, Password, "Lifecycle"));
        var token = factory.EmailSpy.ExtractLastTokenFor(email);
        var verify = await GetAsync(host, $"/api/auth/verify-email?token={Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        return email;
    }

    private string ExtractResetToken(string email)
    {
        // Mail cuối cùng gửi tới email phải là mail reset (không phải mail verify lúc đăng ký).
        var token = factory.EmailSpy.ExtractLastTokenFor(email);
        return factory.EmailSpy.LastBodyFor(email).Contains("reset-password", StringComparison.Ordinal)
            ? token
            : throw new InvalidOperationException("Không có mail reset-password.");
    }

    private async Task MutateMembershipAsync(string email, Action<UserShop> mutate)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var membership = await db.UserShops.IgnoreQueryFilters()
            .SingleAsync(us => db.Users.Any(u => u.Id == us.UserId && u.EmailNormalized == email.ToUpperInvariant()));
        mutate(membership);
        await db.SaveChangesAsync();
    }

    private async Task SetMembershipStatusAsync(string email, UserShopStatus status)
    {
        // Status chỉ đổi qua nghiệp vụ chưa có (đình chỉ thành viên) — test đặt thẳng bằng SQL.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE user_shop SET status = {status.ToString()}
            WHERE user_id = (SELECT id FROM app_user WHERE email_normalized = {email.ToUpperInvariant()})
            """);
        Assert.Equal(1, rows);
    }

    private Task<HttpResponseMessage> LoginAsync(string? host, string email, string password = Password) =>
        PostAsync(host, "/api/auth/login", new LoginRequest(email, password));

    private Task<HttpResponseMessage> RefreshAsync(string? host, string refreshToken) =>
        PostAsync(host, "/api/auth/refresh-token", new RefreshTokenRequest(refreshToken));

    private static async Task<AuthTokenResult> ReadTokensAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthTokenResult>())!;
    }

    private Task<HttpResponseMessage> PostAsync<TBody>(string? host, string path, TBody body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (host is not null)
        {
            request.Headers.Host = host;
        }

        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> GetAsync(string? host, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (host is not null)
        {
            request.Headers.Host = host;
        }

        return _client.SendAsync(request);
    }
}
