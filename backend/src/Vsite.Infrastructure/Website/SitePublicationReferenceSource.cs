using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Media.Dtos;
using Vsite.Application.Media.Interfaces;
using Vsite.Application.Website.Versioning;
using Vsite.Domain.Abstractions;

namespace Vsite.Infrastructure.Website;

/// <summary>
/// T8, WEBSITE-004 (08 §4, R11) — nguồn tham chiếu "ảnh còn nằm trong <c>snapshot.media</c> của bản publish nào". Ảnh trong snapshot là
/// bản CLONE (tree) hoặc chính bản Library (ảnh OG/favicon, preset null), nên tập id cần tìm = clone có <c>SourceAssetId == assetId</c> CỘNG
/// <c>assetId</c>. Xoá bản Library không kéo theo clone, nên ảnh vẫn sống trong site tới khi prune (8d) — đây là chỗ cảnh báo điều đó.
/// Trả MỘT mục nếu có ít nhất một bản chứa ảnh; <c>IsCurrent</c> = bản đang chạy (<c>Website.CurrentPublicationId</c>) có chứa hay không.
/// Snapshot đọc qua <see cref="StoredDocuments.ReadCurrent"/> (cùng đường với <c>GetPublishedSite</c>), key của <c>media</c> so không phân biệt
/// hoa/thường (R14). <c>ShopId</c> đến từ <see cref="ITenantContext"/> và có mặt TƯỜNG MINH trong từng predicate (#21.2); không IgnoreQueryFilters.
/// </summary>
public sealed class SitePublicationReferenceSource(IAppDbContext db, ITenantContext tenant, DocumentVersioning versioning) : IMediaReferenceSource
{
    public async Task<IReadOnlyList<MediaReferenceDto>> FindAsync(Guid assetId, CancellationToken ct)
    {
        if (tenant.ShopId is not { } shopId)
        {
            return [];
        }

        var wanted = (await db.MediaAssets
                .AsNoTracking()
                .Where(a => a.ShopId == shopId && a.SourceAssetId == assetId)
                .Select(a => a.Id)
                .ToListAsync(ct))
            .ToHashSet();
        wanted.Add(assetId);

        // Tải snapshot của mọi bản của shop (tối đa 20 bản nhờ job prune 8d, #118) rồi đọc key `media` trong memory —
        // đổi lấy việc đi qua ReadCurrent/versioning thay vì SQL jsonb thô. Nếu bản lớn gây chậm: prefilter `snapshot->'media' ?| keys`.
        var currentId = await db.Websites
            .AsNoTracking()
            .Where(w => w.ShopId == shopId)
            .Select(w => w.CurrentPublicationId)
            .FirstOrDefaultAsync(ct);
        var publications = await db.SitePublications
            .AsNoTracking()
            .Where(p => p.ShopId == shopId)
            .Select(p => new { p.Id, p.Snapshot })
            .ToListAsync(ct);

        var found = false;
        var current = false;
        foreach (var publication in publications)
        {
            if (!SnapshotMediaKeys.Read(publication.Snapshot, versioning).Any(wanted.Contains))
            {
                continue;
            }

            found = true;
            if (publication.Id == currentId)
            {
                current = true;
            }
        }

        return found ? [new MediaReferenceDto(MediaReferenceKind.SitePublication, IsCurrent: current)] : [];
    }
}
