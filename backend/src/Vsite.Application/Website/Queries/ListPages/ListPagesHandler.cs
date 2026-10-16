using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Website.Dtos;
using Vsite.Domain.Abstractions;

namespace Vsite.Application.Website.Queries.ListPages;

public sealed class ListPagesHandler(IAppDbContext db, ITenantContext tenant)
    : IRequestHandler<ListPagesQuery, IReadOnlyList<PageDto>>
{
    public async Task<IReadOnlyList<PageDto>> Handle(ListPagesQuery request, CancellationToken cancellationToken)
    {
        var shopId = tenant.RequireShopId();

        // Page và PageDraft cùng khoá (draft.Id = page.Id); IsDirty đọc bằng subquery để không mất trang nếu thiếu draft.
        var rows = await db.Pages
            .AsNoTracking()
            .Where(p => p.ShopId == shopId)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.CreatedAt)
            .Select(p => new
            {
                Page = p,
                IsDirty = db.PageDrafts.Where(d => d.Id == p.Id && d.ShopId == shopId).Select(d => d.IsDirty).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => Mappings.ToDto(r.Page, r.IsDirty)).ToList();
    }
}
