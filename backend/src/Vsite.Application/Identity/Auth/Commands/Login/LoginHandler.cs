using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Identity;
using Vsite.Application.Identity.Auth.Dtos;
using Vsite.Application.Identity.Interfaces;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Authorization;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Identity;
using Vsite.Domain.Identity.Entities;

namespace Vsite.Application.Identity.Auth.Commands.Login;

/// <summary>
/// Quyết định #27/#31 (audience theo domain) + #29 (credential riêng theo shop) + #32 (token shop
/// không đọc được credential global — ở ĐÂY là chiều ngược: đăng nhập context shop chỉ được so
/// khớp với `UserShop.PasswordHash`, không bao giờ rơi về `User.PasswordHash`; lockout theo scope,
/// không phải toàn cục — dò password ở Shop C không được khoá tài khoản ở Shop A).
/// </summary>
public sealed class LoginHandler(
    IAppDbContext db,
    ITenantContext tenantContext,
    IPasswordHasher<User> passwordHasher,
    IJwtTokenService tokenService,
    ILoginAttemptThrottle loginAttemptThrottle)
    : IRequestHandler<LoginCommand, AuthTokenResult>
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    // PasswordHasher<User> không đọc field nào của instance — chỉ cần một User hợp lệ để gọi.
    private static readonly User TimingDummy = new() { PrimaryIdentityKind = Vsite.Domain.Identity.Enums.PrimaryIdentityKind.Email, RoleId = Guid.Empty };

    public async Task<AuthTokenResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var normalized = request.Email.Trim().ToUpperInvariant();

        var audience = tenantContext.AudienceKind switch
        {
            TenantAudienceKind.Main => AudienceHelpers.Main,
            TenantAudienceKind.Portal => AudienceHelpers.Portal,
            TenantAudienceKind.Shop => AudienceHelpers.ForShop(tenantContext.ShopId ?? throw new InvalidOperationException("Audience=Shop nhưng ShopId null.")),
            _ => throw new InvalidOperationException("AudienceKind không hợp lệ."),
        };

        // Lockout theo (email, scope) — KHÔNG toàn cục (#32).
        var throttleKey = $"{normalized}:{audience}";
        if (await loginAttemptThrottle.IsLockedOutAsync(throttleKey, cancellationToken))
        {
            throw new TooManyRequestsException("LOGIN_LOCKED_OUT", "Quá nhiều lần đăng nhập sai. Vui lòng thử lại sau ít phút.");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.EmailNormalized == normalized, cancellationToken);

        // Tài khoản bị đình chỉ toàn cục đi CÙNG nhánh 401 với "sai mật khẩu" — không lộ trạng thái
        // tài khoản cho người đoán email (REFACTOR-AUTHZ-001).
        if (user is not null && !user.CanSignIn)
        {
            user = null;
        }

        UserShop? userShop = null;
        string? passwordHashToVerify = null;

        if (user is not null)
        {
            if (tenantContext.AudienceKind == TenantAudienceKind.Shop)
            {
                // Đọc xuyên shop (chưa có tenant context ở bước login) qua helper — chỉ membership
                // CÒN HIỆU LỰC: đã xoá mềm hoặc Suspended/Invited coi như không có, rơi vào 401 chung.
                userShop = await db.UserShops.ActiveAcrossShops()
                    .FirstOrDefaultAsync(us => us.UserId == user.Id && us.ShopId == tenantContext.ShopId, cancellationToken);

                // Chưa có UserShop → chưa từng đặt password riêng ở shop này, không có gì để so
                // khớp (03 §3.3 — phải đăng ký ở shop đó trước, không "login thẳng" bằng credential
                // global).
                passwordHashToVerify = userShop?.PasswordHash;
            }
            else
            {
                passwordHashToVerify = user.PasswordHash;
            }
        }

        if (user is null || passwordHashToVerify is null)
        {
            // Vẫn tốn đúng một lượt PBKDF2 như nhánh sai mật khẩu — không để thời gian phản hồi lộ
            // "email này có tài khoản đang hoạt động / có membership ở shop" (#90).
            passwordHasher.HashPassword(TimingDummy, request.Password);
        }

        if (user is null || passwordHashToVerify is null ||
            passwordHasher.VerifyHashedPassword(user, passwordHashToVerify, request.Password) == PasswordVerificationResult.Failed)
        {
            await loginAttemptThrottle.RecordFailureAsync(throttleKey, cancellationToken);
            throw new UnauthorizedAccessException("Email hoặc mật khẩu không đúng.");
        }

        await loginAttemptThrottle.ResetAsync(throttleKey, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        user.RecordLogin(now);
        userShop?.MarkActive(now);

        // #27: ownerShopIds CHỈ để render UI shop switcher, không bao giờ dùng để authorize.
        var ownerShopIds = await db.UserShops.ActiveAcrossShops()
            .Where(us => us.UserId == user.Id && us.RoleId == WellKnownRoles.OwnerId)
            .Select(us => us.ShopId)
            .ToListAsync(cancellationToken);

        var accessToken = tokenService.CreateAccessToken(new AccessTokenRequest(
            user.Id, audience, user.Email, user.Phone, user.FullName, user.AvatarUrl, ownerShopIds));

        var rawRefreshToken = tokenService.GenerateOpaqueToken();
        var refreshTokenExpiresAt = now.Add(RefreshTokenLifetime);

        db.RefreshTokens.Add(new Vsite.Domain.Identity.Entities.RefreshToken
        {
            UserId = user.Id,
            Audience = audience,
            ShopId = tenantContext.AudienceKind == TenantAudienceKind.Shop ? tenantContext.ShopId : null,
            TokenHash = tokenService.HashToken(rawRefreshToken),
            ExpiresAt = refreshTokenExpiresAt,
        });

        await db.SaveChangesAsync(cancellationToken);

        return new AuthTokenResult(accessToken.Token, accessToken.ExpiresAt, rawRefreshToken, refreshTokenExpiresAt);
    }
}
