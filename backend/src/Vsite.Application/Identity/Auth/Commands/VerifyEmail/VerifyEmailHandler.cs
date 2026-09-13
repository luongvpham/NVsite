using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Identity.Interfaces;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Identity;
using Vsite.Domain.Identity.Entities;
using Vsite.Domain.Identity.Enums;

namespace Vsite.Application.Identity.Auth.Commands.VerifyEmail;

/// <summary>
/// 03 §6.1 bước [3]-[5]. Một `SaveChangesAsync` DUY NHẤT cho toàn bộ thay đổi (tạo User nếu chưa
/// có + tạo UserShop nếu context shop + đánh dấu ConsumedAt) — EF Core tự bọc trong MỘT transaction,
/// đúng yêu cầu "trong MỘT transaction" của tài liệu.
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
            };

            // ⚠️ CHỈ ghi password vào User khi context là vsite.vn. Context shop KHÔNG BAO GIỜ đụng
            // User.PasswordHash — 03 §6.1 cảnh báo đây là lỗi nguy hiểm nhất của luồng đăng ký.
            if (pending.ShopId is null)
            {
                user.PasswordHash = pending.PasswordHash;
            }

            db.Users.Add(user);
        }

        if (pending.ShopId is not null)
        {
            // IgnoreQueryFilters — kiểm tra membership là thao tác hệ thống, chưa có tenant context
            // nào được resolve ở luồng verify-email (chưa có JWT).
            var alreadyMember = await db.UserShops.IgnoreQueryFilters()
                .AnyAsync(us => us.UserId == user.Id && us.ShopId == pending.ShopId, cancellationToken);

            if (!alreadyMember)
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
