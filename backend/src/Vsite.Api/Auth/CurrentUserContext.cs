using Microsoft.AspNetCore.Http;
using Vsite.Application.Common.Interfaces;

namespace Vsite.Api.Auth;

/// <summary>Đọc `sub`/`aud` từ `HttpContext.User` đã xác thực bởi JWT Bearer (`AuthenticationSetup`).
/// Chỉ dùng được sau `UseAuthentication` — endpoint gọi tới đây phải yêu cầu authenticated
/// (`.RequireAuthorization()`), nếu không `UserId`/`Audience` sẽ throw.</summary>
public sealed class CurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
    public Guid UserId
    {
        get
        {
            var httpContext = httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("Không có HttpContext hiện tại.");
            var claim = httpContext.User.FindFirst("sub")?.Value
                ?? throw new InvalidOperationException("Token không có claim 'sub'.");
            return Guid.TryParse(claim, out var userId)
                ? userId
                : throw new InvalidOperationException("Claim 'sub' không phải GUID hợp lệ.");
        }
    }

    public string Audience
    {
        get
        {
            var httpContext = httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("Không có HttpContext hiện tại.");
            return httpContext.User.FindFirst("aud")?.Value
                ?? throw new InvalidOperationException("Token không có claim 'aud'.");
        }
    }
}
