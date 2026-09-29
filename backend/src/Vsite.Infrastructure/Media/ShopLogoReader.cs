using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Imaging;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Media;
using Vsite.Application.Shop.Interfaces;

namespace Vsite.Infrastructure.Media;

/// <summary>
/// MEDIA-001 D4 (#88) — adapter phía Media cho port <see cref="IShopLogoReader"/> của Shop.
/// Cùng khuôn <c>GetDerivativesHandler</c>: <c>IgnoreQueryFilters()</c> rồi <c>ShopId</c> +
/// <c>!IsDeleted</c> viết tay TRONG CÙNG query, không đọc dòng nguồn — bản Library của logo đã soft
/// delete vẫn cho kết quả (A11/#72). `shopId` do handler Shop truyền từ route/entity, không từ body.
/// Trả URL <c>/media/{key}</c> qua <see cref="ImagePaths.MediaUrl"/> (nơi DUY NHẤT biết scheme URL).
/// </summary>
public sealed class ShopLogoReader(IAppDbContext db) : IShopLogoReader
{
    public async Task<string?> GetLogoUrlAsync(Guid shopId, Guid? logoId, CancellationToken ct)
    {
        if (logoId is null)
        {
            return null;
        }

        var key = await db.MediaAssets
            .IgnoreQueryFilters()
            .Where(a => a.ShopId == shopId
                && !a.IsDeleted
                && a.SourceAssetId == logoId
                && a.Preset == LogoPresets.Header)
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .Select(a => a.StorageKey)
            .FirstOrDefaultAsync(ct);

        return key is null ? null : ImagePaths.MediaUrl(key);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetLogoUrlsAsync(IReadOnlyCollection<Guid> shopIds, CancellationToken ct)
    {
        if (shopIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        // Một câu SQL: join Shop (Id, LogoId) với phái sinh — cặp (ShopId, SourceAssetId) được ép NGAY trong
        // query, không lọc lại sau. Media dependsOn Shop nên đọc `Shop` ở Infrastructure.Media là hợp lệ.
        var rows = await (
            from s in db.Shops
            where shopIds.Contains(s.Id) && s.LogoId != null
            join a in db.MediaAssets.IgnoreQueryFilters()
                on new { ShopId = s.Id, SourceAssetId = s.LogoId }
                equals new { a.ShopId, a.SourceAssetId }
            where !a.IsDeleted && a.Preset == LogoPresets.Header
            select new { s.Id, a.StorageKey, a.CreatedAt, AssetId = a.Id })
            .ToListAsync(ct);

        // Thường đúng 1 dòng/shop; nếu nhiều thì lấy dòng đầu theo CreatedAt, Id (cùng luật GetLogoUrlAsync).
        return rows
            .GroupBy(r => r.Id)
            .ToDictionary(
                g => g.Key,
                g => ImagePaths.MediaUrl(g.OrderBy(r => r.CreatedAt).ThenBy(r => r.AssetId).First().StorageKey));
    }
}
