using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Media.Interfaces;
using Vsite.Application.Website.Versioning;
using Vsite.Domain.Abstractions;

namespace Vsite.Infrastructure.Website;

/// <summary>
/// WEBSITE-008 T6 (#117) — id ảnh trong <c>snapshot.media</c> của MỌI bản publish còn lại của shop. Đọc key bằng
/// <see cref="SnapshotMediaKeys"/> (dùng chung với <see cref="SitePublicationReferenceSource"/>); snapshot không đọc được thì NÉM (R5).
/// </summary>
public sealed class SitePublicationUsageSource(IAppDbContext db, ITenantContext tenant, DocumentVersioning versioning) : IMediaUsageSource
{
    public async Task<IReadOnlySet<Guid>> CollectUsedAssetIdsAsync(CancellationToken ct)
    {
        var shopId = tenant.ShopId ?? throw new InvalidOperationException("Không có tenant để thu ảnh đang dùng.");

        var snapshots = await db.SitePublications
            .AsNoTracking()
            .Where(p => p.ShopId == shopId)
            .Select(p => p.Snapshot)
            .ToListAsync(ct);

        var used = new HashSet<Guid>();
        foreach (var snapshot in snapshots)
        {
            used.UnionWith(SnapshotMediaKeys.Read(snapshot, versioning));
        }

        return used;
    }
}
