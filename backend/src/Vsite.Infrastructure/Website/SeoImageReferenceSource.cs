using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Media.Dtos;
using Vsite.Application.Media.Interfaces;
using Vsite.Domain.Abstractions;

namespace Vsite.Infrastructure.Website;

/// <summary>
/// T8, WEBSITE-004 (08 §4) — nguồn tham chiếu "ảnh Library dùng làm ảnh OG của trang / OG mặc định của website / favicon". Các cột này
/// lưu thẳng id bản Library (FK ghép #76), nên so <c>== assetId</c> không qua clone. Mỗi trang một mục (<see cref="MediaReferenceKind.PageOgImage"/>
/// kèm <c>pageId</c>/<c>pageTitle</c>); website tối đa một mục mỗi kind (cùng ảnh làm cả OG lẫn favicon thì báo cả hai). Page đã xoá mềm không
/// báo (Global Filter). <c>ShopId</c> đến từ <see cref="ITenantContext"/> và có mặt TƯỜNG MINH trong từng predicate (#21.2).
/// </summary>
public sealed class SeoImageReferenceSource(IAppDbContext db, ITenantContext tenant) : IMediaReferenceSource
{
    public async Task<IReadOnlyList<MediaReferenceDto>> FindAsync(Guid assetId, CancellationToken ct)
    {
        if (tenant.ShopId is not { } shopId)
        {
            return [];
        }

        var references = new List<MediaReferenceDto>();

        var pages = await db.Pages
            .AsNoTracking()
            .Where(p => p.ShopId == shopId && p.OgImageId == assetId)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Title)
            .Select(p => new { p.Id, p.Title })
            .ToListAsync(ct);
        references.AddRange(pages.Select(p => new MediaReferenceDto(MediaReferenceKind.PageOgImage, p.Id, p.Title)));

        var website = await db.Websites
            .AsNoTracking()
            .Where(w => w.ShopId == shopId && (w.DefaultOgImageId == assetId || w.FaviconAssetId == assetId))
            .Select(w => new { IsOg = w.DefaultOgImageId == assetId, IsFavicon = w.FaviconAssetId == assetId })
            .FirstOrDefaultAsync(ct);
        if (website is { IsOg: true })
        {
            references.Add(new MediaReferenceDto(MediaReferenceKind.WebsiteOgImage));
        }

        if (website is { IsFavicon: true })
        {
            references.Add(new MediaReferenceDto(MediaReferenceKind.WebsiteFavicon));
        }

        return references;
    }
}
