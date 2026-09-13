using Identity.Application.Common.Interfaces;
using Identity.Application.Common.Options;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Domain.Abstractions;
using Shared.Exceptions;

namespace Identity.Application.Auth.Commands.Register;

/// <summary>
/// 03 §6.1 bước [1]-[2]. Luôn trả kết quả GIỐNG NHAU dù email đã tồn tại hay chưa (nhánh "CÓ +
/// context=shop + chưa có UserShop") — ảo giác tách biệt (03 §3.3) không được lộ qua response.
/// </summary>
public sealed class RegisterHandler(
    IIdentityDbContext db,
    ITenantContext tenantContext,
    IPasswordHasher<User> passwordHasher,
    IJwtTokenService tokenService,
    IEmailSender emailSender,
    IOptions<AuthOptions> authOptions)
    : IRequestHandler<RegisterCommand, RegisterResult>
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(20);

    public async Task<RegisterResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var normalized = request.Email.Trim().ToUpperInvariant();
        var shopId = tenantContext.AudienceKind == TenantAudienceKind.Shop ? tenantContext.ShopId : null;

        var existingUser = await db.Users.FirstOrDefaultAsync(u => u.EmailNormalized == normalized, cancellationToken);

        if (existingUser is not null)
        {
            if (shopId is null)
            {
                // 03 §6.1 nhánh [2] "CÓ, và context = vsite.vn" — DỪNG, báo thẳng.
                throw new ConflictException(
                    "EMAIL_ALREADY_REGISTERED",
                    "Email này đã có tài khoản. Vui lòng đăng nhập hoặc dùng Quên mật khẩu.");
            }

            // IgnoreQueryFilters: đây là kiểm tra CHÉO tenant có chủ đích (user đã có membership ở
            // ĐÚNG shop này chưa), không phải đọc dữ liệu tenant hiện tại — Global Query Filter
            // (fail-closed theo TenantContext) sẽ luôn trả rỗng ở bước đăng ký (chưa có JWT/tenant
            // context nào được resolve), nên phải bypass filter một cách tường minh ở đây.
            var hasMembership = await db.UserShops.IgnoreQueryFilters()
                .AnyAsync(us => us.UserId == existingUser.Id && us.ShopId == shopId, cancellationToken);

            if (hasMembership)
            {
                // 03 §6.1 nhánh [2] "CÓ, và user ĐÃ có UserShop ở shop này" — DỪNG.
                throw new ConflictException(
                    "EMAIL_ALREADY_REGISTERED_AT_SHOP",
                    "Email này đã có tài khoản tại shop này. Vui lòng đăng nhập hoặc dùng Quên mật khẩu.");
            }

            // Nhánh còn lại (CÓ user, CHƯA có UserShop ở shop này) — rơi xuống, vẫn tạo
            // PendingRegistration như thể đăng ký mới, đúng "ảo giác tách biệt" 03 §3.3/§6.1.
        }

        var rawToken = tokenService.GenerateOpaqueToken();

        var pending = new PendingRegistration
        {
            Email = request.Email.Trim(),
            EmailNormalized = normalized,
            // PasswordHasher<User> không đọc field nào của instance truyền vào — chỉ cần thoả
            // required member để compile, giá trị không có ý nghĩa nghiệp vụ ở đây.
            PasswordHash = passwordHasher.HashPassword(
                new User { PrimaryIdentityKind = PrimaryIdentityKind.Email, RoleId = Guid.Empty },
                request.Password),
            FullName = request.FullName,
            ShopId = shopId,
            TokenHash = tokenService.HashToken(rawToken),
            ExpiresAt = DateTimeOffset.UtcNow.Add(TokenLifetime),
        };

        db.PendingRegistrations.Add(pending);
        await db.SaveChangesAsync(cancellationToken);

        var verifyUrl = $"{authOptions.Value.ApiBaseUrl.TrimEnd('/')}/auth/verify-email?token={Uri.EscapeDataString(rawToken)}";
        await emailSender.SendAsync(
            pending.Email,
            "Xác minh email của bạn",
            $"<p>Nhấn vào liên kết sau để xác minh email (hết hạn sau 20 phút):</p><p><a href=\"{verifyUrl}\">{verifyUrl}</a></p>",
            cancellationToken);

        return new RegisterResult(true);
    }
}
