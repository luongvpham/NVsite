using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Website.Navigation;
using Vsite.Application.Website.Options;
using Vsite.Application.Website.Versioning;
using Vsite.Domain.Abstractions;

namespace Vsite.Application.Website.Jobs;

/// <summary>
/// WEBSITE-008 (#118) — bỏ mục menu trỏ trang không còn (cùng cả con) của shop hiện tại (<see cref="ITenantContext"/>), CHỈ khi menu
/// yên quá <see cref="BuilderRetentionOptions.NavigationQuietPeriod"/> (<c>UpdatedAt ?? CreatedAt</c>) để không giành xmin với chủ shop (R3).
/// Không đổi <c>IsDirty</c>; không có mục mồ côi ⇒ không ghi. Gặp concurrency ⇒ bỏ lượt, không ném.
/// </summary>
public sealed class NavigationOrphanCleaner(
    IAppDbContext db,
    ITenantContext tenant,
    IOptions<BuilderRetentionOptions> options,
    TimeProvider time,
    DocumentVersioning versioning,
    ILogger<NavigationOrphanCleaner> logger)
{
    /// <summary>Trả số menu đã đổi (0 hoặc 1).</summary>
    public async Task<int> CleanAsync(CancellationToken cancellationToken)
    {
        var shopId = tenant.RequireShopId();
        var cutoff = time.GetUtcNow() - options.Value.NavigationQuietPeriod;

        var nav = await db.NavigationConfigs
            .Where(n => n.ShopId == shopId && (n.UpdatedAt ?? n.CreatedAt) < cutoff)
            .FirstOrDefaultAsync(cancellationToken);
        if (nav is null)
        {
            return 0;
        }

        var existing = await db.Pages
            .Where(p => p.ShopId == shopId && p.WebsiteId == nav.Id)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var items = NavigationItems.Parse(nav.Items, versioning);
        var kept = NavigationItems.DropOrphans(items, existing.ToHashSet());
        if (NavigationItems.CountAll(kept) == NavigationItems.CountAll(items))
        {
            return 0;
        }

        nav.PruneOrphans(NavigationItems.Serialize(kept, versioning));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogInformation(ex, "Menu của website {WebsiteId} vừa được sửa, bỏ lượt dọn mục mồ côi.", nav.Id);
            return 0;
        }

        return 1;
    }
}
