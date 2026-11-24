using Microsoft.Extensions.Logging;
using Vsite.Application.Common.Interfaces;

namespace Vsite.Application.Identity.Jobs;

/// <summary>JOBS-001 T5 — xoá <c>PendingRegistration</c> có <c>ExpiresAt &lt; now - grace</c> (không tenant, set-based).</summary>
public sealed class PendingRegistrationCleanupJob(
    IAppDbContext db, TimeProvider time, ILogger<PendingRegistrationCleanupJob> logger)
{
    public async Task RunAsync(TimeSpan grace, CancellationToken cancellationToken)
    {
        var cutoff = time.GetUtcNow() - grace;
        var count = await BatchedDelete.RunAsync(
            db.PendingRegistrations, db.PendingRegistrations.Where(p => p.ExpiresAt < cutoff), cancellationToken);
        logger.LogInformation("Đã xoá {Count} pending_registration hết hạn trước {Cutoff:O}.", count, cutoff);
    }
}
