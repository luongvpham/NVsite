using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Identity.Interfaces;
using Vsite.Application.Identity.Options;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Authorization;
using Vsite.Domain.Identity.Entities;

namespace Vsite.Application.Identity.Auth.Commands.ForgotPassword;

/// <summary>
/// 03 §6.4 bước [1]-[2]. Handler KHÔNG BAO GIỜ throw vì "không tìm thấy" — âm thầm bỏ qua để giữ
/// đúng bất biến "luôn trả cùng một thông báo".
/// </summary>
public sealed class ForgotPasswordHandler(IAppDbContext db, ITenantContext tenantContext, IJwtTokenService tokenService, IEmailSender emailSender, IOptions<AuthOptions> authOptions)
    : IRequestHandler<ForgotPasswordCommand>
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

    public async Task Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var normalized = request.Email.Trim().ToUpperInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.EmailNormalized == normalized, cancellationToken);

        if (user is null)
        {
            return;
        }

        var audience = tenantContext.AudienceKind switch
        {
            TenantAudienceKind.Main => AudienceHelpers.Main,
            TenantAudienceKind.Portal => AudienceHelpers.Portal,
            TenantAudienceKind.Shop => AudienceHelpers.ForShop(tenantContext.ShopId ?? throw new InvalidOperationException("Audience=Shop nhưng ShopId null.")),
            _ => throw new InvalidOperationException("AudienceKind không hợp lệ."),
        };

        if (tenantContext.AudienceKind == TenantAudienceKind.Shop)
        {
            var isMember = await db.UserShops.IgnoreQueryFilters()
                .AnyAsync(us => us.UserId == user.Id && us.ShopId == tenantContext.ShopId, cancellationToken);
            if (!isMember)
            {
                return;
            }
        }

        var rawToken = tokenService.GenerateOpaqueToken();
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            Audience = audience,
            ShopId = tenantContext.AudienceKind == TenantAudienceKind.Shop ? tenantContext.ShopId : null,
            TokenHash = tokenService.HashToken(rawToken),
            ExpiresAt = DateTimeOffset.UtcNow.Add(TokenLifetime),
        });

        await db.SaveChangesAsync(cancellationToken);

        var resetUrl = $"{authOptions.Value.ApiBaseUrl.TrimEnd('/')}/auth/reset-password?token={Uri.EscapeDataString(rawToken)}";
        await emailSender.SendAsync(
            user.Email!,
            "Đặt lại mật khẩu",
            $"<p>Nhấn vào liên kết sau để đặt lại mật khẩu (hết hạn sau 30 phút):</p><p><a href=\"{resetUrl}\">{resetUrl}</a></p>",
            cancellationToken);
    }
}
