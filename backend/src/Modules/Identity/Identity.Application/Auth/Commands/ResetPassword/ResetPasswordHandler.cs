using Identity.Application.Common.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;

namespace Identity.Application.Auth.Commands.ResetPassword;

/// <summary>
/// 03 §6.4 bước [3]-[4]. Đổi đúng scope (User hoặc UserShop tuỳ Audience của token), revoke TOÀN
/// BỘ refresh token cùng scope (Quyết định #21.5 — kẻ tấn công chiếm session không giữ được quyền
/// sau khi nạn nhân đổi mật khẩu), gửi mail thông báo nêu rõ đổi ở đâu.
/// </summary>
public sealed class ResetPasswordHandler(IIdentityDbContext db, IPasswordHasher<Identity.Domain.Entities.User> passwordHasher, IJwtTokenService tokenService, IEmailSender emailSender)
    : IRequestHandler<ResetPasswordCommand>
{
    public async Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = tokenService.HashToken(request.Token);
        var now = DateTimeOffset.UtcNow;

        var resetToken = await db.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.ConsumedAt == null && t.ExpiresAt > now, cancellationToken);

        if (resetToken is null)
        {
            throw new DomainException("RESET_TOKEN_INVALID", "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == resetToken.UserId, cancellationToken)
            ?? throw new DomainException("RESET_TOKEN_INVALID", "Tài khoản không tồn tại.");

        var newHash = passwordHasher.HashPassword(user, request.NewPassword);
        string whereChanged;

        if (resetToken.ShopId is null)
        {
            user.PasswordHash = newHash;
            whereChanged = "tài khoản vsite.vn của bạn";
        }
        else
        {
            // IgnoreQueryFilters — thao tác hệ thống, không phải đọc dữ liệu theo tenant context request hiện tại.
            var userShop = await db.UserShops.IgnoreQueryFilters()
                .FirstOrDefaultAsync(us => us.UserId == user.Id && us.ShopId == resetToken.ShopId, cancellationToken)
                ?? throw new DomainException("RESET_TOKEN_INVALID", "Membership không tồn tại.");

            userShop.PasswordHash = newHash;
            whereChanged = $"tài khoản tại shop {resetToken.ShopId}";
        }

        // Revoke toàn bộ refresh token CÙNG scope — token đã bị chiếm (nếu có) mất quyền ngay.
        var tokensToRevoke = await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.Audience == resetToken.Audience && t.ShopId == resetToken.ShopId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var refreshToken in tokensToRevoke)
        {
            refreshToken.RevokedAt = now;
        }

        resetToken.ConsumedAt = now;

        await db.SaveChangesAsync(cancellationToken);

        await emailSender.SendAsync(
            user.Email!,
            "Mật khẩu của bạn đã được thay đổi",
            $"<p>Mật khẩu của bạn tại {whereChanged} đã được thay đổi. Nếu không phải bạn, hãy liên hệ hỗ trợ ngay.</p>",
            cancellationToken);
    }
}
