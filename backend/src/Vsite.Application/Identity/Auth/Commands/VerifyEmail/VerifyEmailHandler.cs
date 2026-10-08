using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Identity;
using Vsite.Application.Identity.Interfaces;
using Vsite.Domain.Authorization;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Identity;
using Vsite.Domain.Identity.Entities;
using Vsite.Domain.Identity.Enums;

namespace Vsite.Application.Identity.Auth.Commands.VerifyEmail;

/// <summary>
/// 03 §6.1 bước [3]-[5]. Một `SaveChangesAsync` DUY NHẤT cho toàn bộ thay đổi (tạo User nếu chưa
/// có + tạo UserShop nếu context shop + đánh dấu ConsumedAt) — EF Core tự bọc trong MỘT transaction,
/// đúng yêu cầu "trong MỘT transaction" của tài liệu.
///
/// Context shop (REFACTOR-AUTHZ-001): chưa có membership → tạo mới; membership đã XOÁ MỀM → khôi
/// phục dòng cũ (unique index <c>(user_id, shop_id)</c> không lọc soft delete — trước đây nhánh này
/// INSERT trùng và ra 500); membership còn sống (kể cả Suspended/Invited) → không đụng tới.
/// </summary>
public sealed class VerifyEmailHandler(IAppDbContext db, IJwtTokenService tokenService)
    : IRequestHandler<VerifyEmailCommand, VerifyEmailResult>
{
    public async Task<VerifyEmailResult> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = tokenService.HashToken(request.Token);
        var now = DateTimeOffset.UtcNow;

        var pending = await db.PendingRegistrations
            .FirstOrDefaultAsync(p => p.TokenHash == tokenHash && p.ConsumedAt == null && p.ExpiresAt > now, cancellationToken);

        if (pending is null)
        {
            throw new DomainException("VERIFY_TOKEN_INVALID", "Liên kết xác minh không hợp lệ hoặc đã hết hạn.");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.EmailNormalized == pending.EmailNormalized, cancellationToken);
        var shopMembershipCreated = false;

        if (user is null)
        {
            user = new User
            {
                Email = pending.Email,
                EmailNormalized = pending.EmailNormalized,
                EmailVerifiedAt = now,
                PrimaryIdentityKind = PrimaryIdentityKind.Email,
                RoleId = WellKnownRoles.PlatformUserId,
                FullName = pending.FullName,

                // ⚠️ CHỈ ghi password vào User khi context là vsite.vn. Context shop KHÔNG BAO GIỜ đụng
                // User.PasswordHash — 03 §6.1 cảnh báo đây là lỗi nguy hiểm nhất của luồng đăng ký.
                PasswordHash = pending.ShopId is null ? pending.PasswordHash : null,
            };

            db.Users.Add(user);
        }

        if (pending.ShopId is not null)
        {
            // Đọc xuyên shop kể cả dòng đã xoá mềm (chưa có tenant context ở luồng verify-email).
            var existing = await db.UserShops.IncludingDeletedAcrossShops()
                .FirstOrDefaultAsync(us => us.UserId == user.Id && us.ShopId == pending.ShopId, cancellationToken);

            if (existing is { IsDeleted: true })
            {
                existing.RestoreAsCustomer(pending.PasswordHash);
                shopMembershipCreated = true;

                // Khôi phục = mật khẩu mới → thu hồi mọi refresh token cũ của scope shop này (cùng
                // nguyên tắc ResetPassword/ChangePassword, #21.5). Xoá mềm membership KHÔNG thu hồi
                // token, nên không có bước này thì một refresh token cũ chưa dùng tới sẽ sống lại.
                var audience = AudienceHelpers.ForShop(pending.ShopId.Value);
                var staleTokens = await db.RefreshTokens
                    .Where(t => t.UserId == user.Id && t.Audience == audience && t.ShopId == pending.ShopId && t.RevokedAt == null)
                    .ToListAsync(cancellationToken);
                foreach (var token in staleTokens)
                {
                    token.RevokedAt = now;
                }
            }
            else if (existing is null)
            {
                db.UserShops.Add(new UserShop
                {
                    UserId = user.Id,
                    ShopId = pending.ShopId.Value,
                    RoleId = WellKnownRoles.CustomerId,
                    PasswordHash = pending.PasswordHash,
                    Source = UserShopSource.RegisteredOnShop,
                });
                shopMembershipCreated = true;
            }
        }

        pending.ConsumedAt = now;

        await db.SaveChangesAsync(cancellationToken);

        return new VerifyEmailResult(user.Id, shopMembershipCreated);
    }
}
