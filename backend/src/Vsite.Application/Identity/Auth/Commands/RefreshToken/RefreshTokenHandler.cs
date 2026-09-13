using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Identity.Auth.Dtos;
using Vsite.Application.Identity.Interfaces;
using Vsite.Domain.Identity;

namespace Vsite.Application.Identity.Auth.Commands.RefreshToken;

/// <summary>
/// Quyết định #3 — rotation: mỗi lần refresh, token cũ bị revoke NGAY và một token mới được phát.
/// Nếu một token ĐÃ revoke bị đem đi refresh lần nữa (reuse) → coi là dấu hiệu bị đánh cắp, revoke
/// TOÀN BỘ token cùng (UserId, Audience, ShopId) — không chỉ token đang dùng.
/// </summary>
public sealed class RefreshTokenHandler(IAppDbContext db, IJwtTokenService tokenService)
    : IRequestHandler<RefreshTokenCommand, AuthTokenResult>
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public async Task<AuthTokenResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = tokenService.HashToken(request.RefreshToken);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (existing is null)
        {
            throw new UnauthorizedAccessException("Refresh token không hợp lệ.");
        }

        if (existing.RevokedAt is not null)
        {
            await RevokeAllAsync(existing.UserId, existing.Audience, existing.ShopId, cancellationToken);
            throw new UnauthorizedAccessException("Refresh token đã bị thu hồi.");
        }

        if (existing.ExpiresAt < DateTimeOffset.UtcNow)
        {
            throw new UnauthorizedAccessException("Refresh token đã hết hạn.");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == existing.UserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Tài khoản không tồn tại.");

        var ownerShopIds = await db.UserShops.IgnoreQueryFilters()
            .Where(us => us.UserId == user.Id && us.RoleId == WellKnownRoles.OwnerId)
            .Select(us => us.ShopId)
            .ToListAsync(cancellationToken);

        var accessToken = tokenService.CreateAccessToken(new AccessTokenRequest(
            user.Id, existing.Audience, user.Email, user.Phone, user.FullName, user.AvatarUrl, ownerShopIds));

        var rawNewRefreshToken = tokenService.GenerateOpaqueToken();
        var now = DateTimeOffset.UtcNow;
        var newRefreshTokenExpiresAt = now.Add(RefreshTokenLifetime);

        // Phải fully-qualify: namespace chứa file này (…Commands.RefreshToken) che mất tên type
        // `RefreshToken` của Domain — CS0118 nếu viết tắt.
        var newToken = new Vsite.Domain.Identity.Entities.RefreshToken
        {
            UserId = existing.UserId,
            Audience = existing.Audience,
            ShopId = existing.ShopId,
            TokenHash = tokenService.HashToken(rawNewRefreshToken),
            ExpiresAt = newRefreshTokenExpiresAt,
        };

        db.RefreshTokens.Add(newToken);
        existing.RevokedAt = now;
        existing.ReplacedByTokenId = newToken.Id;

        await db.SaveChangesAsync(cancellationToken);

        return new AuthTokenResult(accessToken.Token, accessToken.ExpiresAt, rawNewRefreshToken, newRefreshTokenExpiresAt);
    }

    private async Task RevokeAllAsync(Guid userId, string audience, Guid? shopId, CancellationToken cancellationToken)
    {
        var tokens = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.Audience == audience && t.ShopId == shopId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var token in tokens)
        {
            token.RevokedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
