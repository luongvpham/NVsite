using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Identity;
using Vsite.Domain.Authorization;
using Vsite.Domain.Identity.Entities;
using Vsite.Domain.Identity.Enums;

namespace Vsite.Application.Identity.Auth.Commands.ChangePassword;

public sealed class ChangePasswordHandler(IAppDbContext db, ICurrentUserContext currentUser, IPasswordHasher<User> passwordHasher)
    : IRequestHandler<ChangePasswordCommand>
{
    public async Task Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.UserId && u.Status == UserStatus.Active, cancellationToken)
            ?? throw new UnauthorizedAccessException("Tài khoản không tồn tại hoặc đã bị đình chỉ.");

        var shopId = AudienceHelpers.TryGetShopId(currentUser.Audience);
        UserShop? userShop = null;
        string? currentHash;

        if (shopId is not null)
        {
            // ShopMembershipValidationMiddleware đã re-verify membership; vẫn đọc qua helper chỉ lấy
            // membership còn hiệu lực — không dựa vào việc middleware luôn đứng trước handler.
            userShop = await db.UserShops.ActiveAcrossShops()
                .FirstOrDefaultAsync(us => us.UserId == user.Id && us.ShopId == shopId, cancellationToken)
                ?? throw new UnauthorizedAccessException("Membership không tồn tại.");
            currentHash = userShop.PasswordHash;
        }
        else
        {
            currentHash = user.PasswordHash;
        }

        if (currentHash is null ||
            passwordHasher.VerifyHashedPassword(user, currentHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAccessException("Mật khẩu hiện tại không đúng.");
        }

        var newHash = passwordHasher.HashPassword(user, request.NewPassword);
        if (userShop is not null)
        {
            userShop.SetPassword(newHash);
        }
        else
        {
            user.SetPassword(newHash);
        }

        // Revoke toàn bộ refresh token CÙNG scope — cùng nguyên tắc với ResetPassword (#21.5): kẻ
        // tấn công chiếm session không giữ được quyền sau khi chủ tài khoản đổi mật khẩu.
        var now = DateTimeOffset.UtcNow;
        var tokensToRevoke = await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.Audience == currentUser.Audience && t.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var refreshToken in tokensToRevoke)
        {
            refreshToken.RevokedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
