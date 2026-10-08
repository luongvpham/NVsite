using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Media.Dtos;
using Vsite.Domain.Pagination;

namespace Vsite.Application.Media.Queries.ListLibrary;

/// <summary>`IsInLibrary = true`, chưa xoá (Global Query Filter), `CreatedAt DESC` — tận dụng thẳng
/// `ix_media_asset_library` (`MediaAssetConfiguration`).</summary>
public sealed class ListLibraryHandler(IAppDbContext db) : IRequestHandler<ListLibraryQuery, PagedResult<MediaAssetDto>>
{
    public async Task<PagedResult<MediaAssetDto>> Handle(ListLibraryQuery request, CancellationToken cancellationToken)
    {
        var query = db.MediaAssets.Where(a => a.IsInLibrary).OrderByDescending(a => a.CreatedAt);

        var total = await query.CountAsync(cancellationToken);
        var entities = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = entities.Select(MediaAssetDto.FromEntity).ToList();

        return new PagedResult<MediaAssetDto>(items, total, request.Page, request.PageSize);
    }
}
