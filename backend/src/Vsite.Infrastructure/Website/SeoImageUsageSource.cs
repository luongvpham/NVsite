using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Media.Interfaces;
using Vsite.Domain.Abstractions;

namespace Vsite.Infrastructure.Website;

/// <summary>
/// WEBSITE-008 T6 (#117) — id ảnh ở các FK nghiệp vụ của Website: <c>Page.OgImageId</c> (chỉ trang còn sống, Global Filter),
/// <c>Website.DefaultOgImageId</c>, <c>Website.FaviconAssetId</c>. (<c>Shop.LogoId</c> do job đọc thẳng.)
/// </summary>
public sealed class SeoImageUsageSource(IAppDbContext db, ITenantContext tenant) : IMediaUsageSource
{
    public async Task<IReadOnlySet<Guid>> CollectUsedAssetIdsAsync(CancellationToken ct)
    {
        var shopId = tenant.ShopId ?? throw new InvalidOperationException("Không có tenant để thu ảnh đang dùng.");

        var used = new HashSet<Guid>();

        var pageOgs = await db.Pages
            .AsNoTracking()
            .Where(p => p.ShopId == shopId && p.OgImageId != null)
            .Select(p => p.OgImageId!.Value)
            .ToListAsync(ct);
        used.UnionWith(pageOgs);

        var websites = await db.Websites
            .AsNoTracking()
            .Where(w => w.ShopId == shopId)
            .Select(w => new { w.DefaultOgImageId, w.FaviconAssetId })
            .ToListAsync(ct);
        foreach (var website in websites)
        {
            if (website.DefaultOgImageId is { } og)
            {
                used.Add(og);
            }

            if (website.FaviconAssetId is { } favicon)
            {
                used.Add(favicon);
            }
        }

        return used;
    }
}
