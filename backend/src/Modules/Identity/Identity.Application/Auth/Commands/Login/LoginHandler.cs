using Identity.Application.Auth.Dtos;
using Identity.Application.Common.Interfaces;
using Identity.Domain;
using Identity.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Authorization;
using Shared.Domain.Abstractions;
using Shared.Exceptions;

namespace Identity.Application.Auth.Commands.Login;

/// <summary>
/// Quyết định #27/#31 (audience theo domain) + #29 (credential riêng theo shop) + #32 (token shop
/// không đọc được credential global — ở ĐÂY là chiều ngược: đăng nhập context shop chỉ được so
/// khớp với `UserShop.PasswordHash`, không bao giờ rơi về `User.PasswordHash`; lockout theo scope,
/// không phải toàn cục — dò password ở Shop C không được khoá tài khoản ở Shop A).
/// </summary>
public sealed class LoginHandler(
    IIdentityDbContext db,
    ITenantContext tenantContext,
    IPasswordHasher<User> passwordHasher,
    IJwtTokenService tokenService,
    ILoginAttemptThrottle loginAttemptThrottle)
    : IRequestHandler<LoginCommand, AuthTokenResult>
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

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

        UserShop? userShop = null;
        string? passwordHashToVerify = null;

        if (user is not null)
        {
            if (tenantContext.AudienceKind == TenantAudienceKind.Shop)
            {
                // IgnoreQueryFilters — chưa có JWT/tenant context nào resolve ở bước login.
                userShop = await db.UserShops.IgnoreQueryFilters()
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

        if (user is null || passwordHashToVerify is null ||
            passwordHasher.VerifyHashedPassword(user, passwordHashToVerify, request.Password) == PasswordVerificationResult.Failed)
        {
            await loginAttemptThrottle.RecordFailureAsync(throttleKey, cancellationToken);
            throw new UnauthorizedAccessException("Email hoặc mật khẩu không đúng.");
        }

        await loginAttemptThrottle.ResetAsync(throttleKey, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        user.LastLoginAt = now;
        if (userShop is not null)
        {
            userShop.LastActiveAt = now;
        }

        // #27: ownerShopIds CHỈ để render UI shop switcher, không bao giờ dùng để authorize.
        var ownerShopIds = await db.UserShops.IgnoreQueryFilters()
            .Where(us => us.UserId == user.Id && us.RoleId == WellKnownRoles.OwnerId)
            .Select(us => us.ShopId)
            .ToListAsync(cancellationToken);

        var accessToken = tokenService.CreateAccessToken(new AccessTokenRequest(
            user.Id, audience, user.Email, user.Phone, user.FullName, user.AvatarUrl, ownerShopIds));

        var rawRefreshToken = tokenService.GenerateOpaqueToken();
        var refreshTokenExpiresAt = now.Add(RefreshTokenLifetime);

        db.RefreshTokens.Add(new Identity.Domain.Entities.RefreshToken
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
