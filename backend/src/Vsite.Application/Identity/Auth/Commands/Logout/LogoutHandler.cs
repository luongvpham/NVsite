using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Identity.Interfaces;

namespace Vsite.Application.Identity.Auth.Commands.Logout;

/// <summary>
/// #101 — thu hồi ĐÚNG refresh token được gửi lên (một phiên), không thu hồi cả nhóm: khác
/// `RefreshTokenHandler` (token đã revoke bị đem đi refresh = nghi bị đánh cắp), logout lặp lại hay
/// logout bằng token đã revoke/không tồn tại chỉ là no-op, không ném lỗi và không lộ token có tồn
/// tại hay không.
/// </summary>
public sealed class LogoutHandler(IAppDbContext db, IJwtTokenService tokenService) : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            return;
        }

        var tokenHash = tokenService.HashToken(request.RefreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.RevokedAt == null, cancellationToken);
        if (token is null)
        {
            return;
        }

        token.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
