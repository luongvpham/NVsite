using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vsite.Application.Common.Interfaces;
using Vsite.Domain.Identity.Enums;

namespace Vsite.Application.Identity.Jobs;

/// <summary>
/// JOBS-001 T5 — quét <c>User</c> có <c>PrimaryIdentityKind = Zalo</c> mà không có <c>ExternalLogin</c> Zalo
/// (vi phạm 03 §1 nguyên tắc 4). CHỈ log Warning (đếm + ≤ 20 Id), không sửa dữ liệu.
/// </summary>
public sealed class ZaloIdentityIntegrityJob(IAppDbContext db, ILogger<ZaloIdentityIntegrityJob> logger)
{
    /// <summary>Giá trị <c>ExternalLogin.Provider</c> của Zalo.</summary>
    public const string ZaloProvider = "zalo";

    private const int SampleSize = 20;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var orphans = db.Users.AsNoTracking().Where(u =>
            u.PrimaryIdentityKind == PrimaryIdentityKind.Zalo
            && !db.ExternalLogins.Any(e => e.UserId == u.Id && e.Provider == ZaloProvider));

        var count = await orphans.CountAsync(cancellationToken);
        if (count == 0)
        {
            return;
        }

        var sample = await orphans.OrderBy(u => u.Id).Select(u => u.Id).Take(SampleSize).ToListAsync(cancellationToken);
        logger.LogWarning(
            "{Count} user PrimaryIdentityKind=Zalo thiếu ExternalLogin Zalo. Mẫu Id (≤ {Sample}): {UserIds}",
            count, SampleSize, string.Join(", ", sample));
    }
}
