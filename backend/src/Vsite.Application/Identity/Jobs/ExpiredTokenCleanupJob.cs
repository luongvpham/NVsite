using Microsoft.Extensions.Logging;
using Vsite.Application.Common.Interfaces;

namespace Vsite.Application.Identity.Jobs;

/// <summary>
/// JOBS-001 T5 — xoá <c>RefreshToken</c> và <c>PasswordResetToken</c> có <c>ExpiresAt &lt; now - grace</c>.
/// Cố ý KHÔNG theo <c>RevokedAt</c>/<c>ConsumedAt</c>: token đã revoke nhưng còn hạn vẫn cần để phát hiện reuse.
/// </summary>
public sealed class ExpiredTokenCleanupJob(
    IAppDbContext db, TimeProvider time, ILogger<ExpiredTokenCleanupJob> logger)
{
    public async Task RunAsync(TimeSpan grace, CancellationToken cancellationToken)
    {
        var cutoff = time.GetUtcNow() - grace;

        var refresh = await BatchedDelete.RunAsync(
            db.RefreshTokens, db.RefreshTokens.Where(t => t.ExpiresAt < cutoff), cancellationToken);
        logger.LogInformation("Đã xoá {Count} refresh_token hết hạn trước {Cutoff:O}.", refresh, cutoff);

        var reset = await BatchedDelete.RunAsync(
            db.PasswordResetTokens, db.PasswordResetTokens.Where(t => t.ExpiresAt < cutoff), cancellationToken);
        logger.LogInformation("Đã xoá {Count} password_reset_token hết hạn trước {Cutoff:O}.", reset, cutoff);
    }
}
