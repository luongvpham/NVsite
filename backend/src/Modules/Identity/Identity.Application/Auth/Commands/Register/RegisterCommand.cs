using MediatR;

namespace Identity.Application.Auth.Commands.Register;

/// <summary>
/// 03 §6.1. Context đăng ký (vsite.vn hay shop nào) đến từ `ITenantContext` (resolve bởi
/// `Api.Tenancy.TenantResolutionMiddleware` theo Host — Quyết định #7 thu hẹp), KHÔNG phải field
/// của command — handler tự đọc `ITenantContext`, endpoint không truyền ShopId từ route/body nữa.
/// </summary>
public sealed record RegisterCommand(string Email, string Password, string? FullName) : IRequest<RegisterResult>;

/// <summary>Không trả biết email đã tồn tại hay chưa — 03 §6.1 "vô hình với người dùng".</summary>
public sealed record RegisterResult(bool VerificationEmailSent);
