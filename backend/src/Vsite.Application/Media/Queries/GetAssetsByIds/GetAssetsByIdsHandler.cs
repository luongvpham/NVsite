using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Media.Dtos;

namespace Vsite.Application.Media.Queries.GetAssetsByIds;

/// <summary>`IgnoreQueryFilters()` rồi `Where(ShopId == tenant)` viết tay TRONG CÙNG query (#72) —
/// khác `ListLibraryHandler` (dùng Global Query Filter mặc định, ẩn record đã xoá): ở đây phải LỘ
/// LẠI record đã soft delete của CÙNG shop (clone của một bản Library đã xoá vẫn phải render), nhưng
/// tuyệt đối không lộ record của shop khác dù nó không bị xoá.</summary>
public sealed class GetAssetsByIdsHandler(IAppDbContext db) : IRequestHandler<GetAssetsByIdsQuery, IReadOnlyList<MediaAssetDto>>
{
    public async Task<IReadOnlyList<MediaAssetDto>> Handle(GetAssetsByIdsQuery request, CancellationToken cancellationToken)
    {
        if (request.Ids.Count == 0)
        {
            return [];
        }

        var entities = await db.MediaAssets
            .IgnoreQueryFilters()
            .Where(a => a.ShopId == request.ShopId && request.Ids.Contains(a.Id))
            .ToListAsync(cancellationToken);

        return entities.Select(MediaAssetDto.FromEntity).ToList();
    }
}
