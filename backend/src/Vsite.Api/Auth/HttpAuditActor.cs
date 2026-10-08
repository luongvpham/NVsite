using Microsoft.AspNetCore.Http;
using Vsite.Application.Common.Interfaces;

namespace Vsite.Api.Auth;

/// <summary>Đọc claim `sub` nếu request đã xác thực; còn lại trả `null` (không throw như
/// <see cref="CurrentUserContext"/>) — xem <see cref="IAuditActor"/>.</summary>
public sealed class HttpAuditActor(IHttpContextAccessor httpContextAccessor) : IAuditActor
{
    public Guid? UserId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value, out var userId)
            ? userId
            : null;
}
