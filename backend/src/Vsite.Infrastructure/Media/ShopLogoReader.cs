using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Media;
using Vsite.Application.Shop.Interfaces;

namespace Vsite.Infrastructure.Media;

/// <summary>
/// MEDIA-001 D4 (#88) — adapter phía Media cho port <see cref="IShopLogoReader"/> của Shop.
/// Cùng khuôn <c>GetDerivativesHandler</c>: <c>IgnoreQueryFilters()</c> rồi <c>ShopId</c> +
/// <c>!IsDeleted</c> viết tay TRONG CÙNG query, không đọc dòng nguồn — bản Library của logo đã soft
/// delete vẫn cho kết quả (A11/#72). `shopId` do handler Shop truyền từ route/entity, không từ body.
/// </summary>
public sealed class ShopLogoReader(IAppDbContext db) : IShopLogoReader
{
    public async Task<string?> GetLogoStorageKeyAsync(Guid shopId, Guid? logoId, CancellationToken ct)
    {
        if (logoId is null)
        {
            return null;
        }

        return await db.MediaAssets
            .IgnoreQueryFilters()
            .Where(a => a.ShopId == shopId
                && !a.IsDeleted
                && a.SourceAssetId == logoId
                && a.Preset == LogoPresets.Header)
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .Select(a => a.StorageKey)
            .FirstOrDefaultAsync(ct);
    }
}
