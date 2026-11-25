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
        var others = current is null ? keep : keep - 1;

        var candidates = db.SitePublications.AsNoTracking()
            .Where(p => p.ShopId == shopId && p.WebsiteId == website.Id);
        if (current is { } currentId)
        {
            candidates = candidates.Where(p => p.Id != currentId);
        }

        var keptVersions = await candidates
            .OrderByDescending(p => p.Version)
            .Take(others)
            .Select(p => p.Version)
            .ToListAsync(cancellationToken);
        if (keptVersions.Count < others)
        {
            return 0;
        }

        // Xoá theo ngưỡng Version: bản publish mới chen vào giữa hai câu query có Version cao hơn nên không bao giờ khớp.
        // Rollback chen vào khoảng này (trỏ current về bản sắp xoá) kết thúc bằng FK abort + Hangfire retry, không mất dữ liệu.
        var threshold = keptVersions.Count == 0 ? int.MaxValue : keptVersions.Min();
        var doomed = db.SitePublications
            .Where(p => p.ShopId == shopId && p.WebsiteId == website.Id && p.Version < threshold);
        if (current is { } keepId)
        {
            doomed = doomed.Where(p => p.Id != keepId);
        }

        return await doomed.ExecuteDeleteAsync(cancellationToken);
    }
}
