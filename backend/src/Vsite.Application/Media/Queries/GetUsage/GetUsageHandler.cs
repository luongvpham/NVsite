using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Media.Dtos;

namespace Vsite.Application.Media.Queries.GetUsage;

/// <summary>`SUM(SizeBytes) WHERE SourceAssetId IS NULL AND NOT IsDeleted` (#55) — Global Query
/// Filter đã lo shop + soft-delete, chỉ cần loại clone/phái sinh (`SourceAssetId != null`).</summary>
public sealed class GetUsageHandler(IAppDbContext db) : IRequestHandler<GetUsageQuery, MediaUsageDto>
{
    public async Task<MediaUsageDto> Handle(GetUsageQuery request, CancellationToken cancellationToken)
    {
        var usedBytes = await db.MediaAssets
            .Where(a => a.SourceAssetId == null)
            .SumAsync(a => (long?)a.SizeBytes, cancellationToken) ?? 0L;

        return new MediaUsageDto(usedBytes);
    }
}
