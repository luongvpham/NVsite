using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Media.Dtos;
using Vsite.Domain.Media.Enums;

namespace Vsite.Application.Media.Queries.GetDerivatives;

/// <summary>`IgnoreQueryFilters()` rồi `ShopId == tenant` và `!IsDeleted` viết tay TRONG CÙNG query
/// (cùng ý `GetAssetsByIdsHandler`, #72). Cô lập tenant dựa vào ShopId của CHÍNH các dòng phái sinh —
/// KHÔNG đọc dòng nguồn: bản Library đã soft delete (kể cả là `Shop.LogoId`, A11) hoặc không tồn tại
/// vẫn cho kết quả nhất quán (phái sinh còn sống thì trả, không thì `[]`), không bao giờ 404 nên không
/// lộ sự tồn tại của id thuộc shop khác. Chỉ trả `Kind = Derivative` — clone đặt vào slot (cùng
/// SourceAssetId + preset) KHÔNG phải phái sinh (REFACTOR-DB-001). Dùng index
/// `ux_media_asset_derivative (source_asset_id, preset)`.</summary>
public sealed class GetDerivativesHandler(IAppDbContext db) : IRequestHandler<GetDerivativesQuery, IReadOnlyList<MediaAssetDto>>
{
    public async Task<IReadOnlyList<MediaAssetDto>> Handle(GetDerivativesQuery request, CancellationToken cancellationToken)
    {
        var query = db.MediaAssets
            .IgnoreQueryFilters()
            .Where(a => a.ShopId == request.ShopId
                && !a.IsDeleted
                && a.Kind == MediaAssetKind.Derivative
                && a.SourceAssetId == request.AssetId);

        if (request.Preset is not null)
        {
            query = query.Where(a => a.Preset == request.Preset);
        }

        var entities = await query
            .OrderBy(a => a.Preset)
            .ThenBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(MediaAssetDto.FromEntity).ToList();
    }
}
