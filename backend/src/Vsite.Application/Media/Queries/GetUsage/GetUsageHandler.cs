using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Media.Dtos;
using Vsite.Domain.Media.Enums;

namespace Vsite.Application.Media.Queries.GetUsage;

/// <summary>`SUM(SizeBytes)` của bản gốc (`Kind` Library/Direct) chưa xoá (#55) — Global Query Filter
/// đã lo shop + soft-delete, chỉ cần loại clone/phái sinh. Lọc theo `Kind` thay vì
/// `SourceAssetId IS NULL` (REFACTOR-DB-001): FK `SourceAssetId` là `ON DELETE SET NULL`, một clone
/// mất nguồn sẽ bị tính nhầm vào quota.</summary>
public sealed class GetUsageHandler(IAppDbContext db) : IRequestHandler<GetUsageQuery, MediaUsageDto>
{
    public async Task<MediaUsageDto> Handle(GetUsageQuery request, CancellationToken cancellationToken)
    {
        var usedBytes = await db.MediaAssets
            .Where(a => a.Kind == MediaAssetKind.Library || a.Kind == MediaAssetKind.Direct)
            .SumAsync(a => (long?)a.SizeBytes, cancellationToken) ?? 0L;

        return new MediaUsageDto(usedBytes);
    }
}
