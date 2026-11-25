using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Website.Options;
using Vsite.Domain.Abstractions;

namespace Vsite.Application.Website.Jobs;

/// <summary>
/// WEBSITE-008 (#118) — bước prune bản publish của shop hiện tại (<see cref="ITenantContext"/>). Giữ ĐÚNG
/// <see cref="BuilderRetentionOptions.PublicationsToKeep"/> bản, gồm bản current (current + các bản <c>Version</c> lớn nhất khác).
/// Xoá cứng set-based; <c>ShopId</c> tường minh trong predicate (cùng Global Filter). FK <c>Restrict</c> là lưới cuối nếu lỡ chạm bản current.
/// </summary>
public sealed class PublicationPruner(IAppDbContext db, ITenantContext tenant, IOptions<BuilderRetentionOptions> options)
{
    /// <summary>Trả số bản đã xoá (0 nếu shop chưa có website hoặc chưa vượt hạn mức).</summary>
    public async Task<int> PruneAsync(CancellationToken cancellationToken)
    {
        var shopId = tenant.RequireShopId();

        var website = await db.Websites.AsNoTracking()
            .Where(w => w.ShopId == shopId)
            .Select(w => new { w.Id, w.CurrentPublicationId })
            .FirstOrDefaultAsync(cancellationToken);
        if (website is null)
        {
            return 0;
        }

        var keep = options.Value.PublicationsToKeep;
        var current = website.CurrentPublicationId;
        var currentId = current ?? Guid.Empty;
        var others = current is null ? keep : keep - 1;

        var keepIds = await db.SitePublications.AsNoTracking()
            .Where(p => p.ShopId == shopId && p.WebsiteId == website.Id && p.Id != currentId)
            .OrderByDescending(p => p.Version)
            .Take(others)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        return await db.SitePublications
            .Where(p => p.ShopId == shopId && p.WebsiteId == website.Id && p.Id != currentId && !keepIds.Contains(p.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
