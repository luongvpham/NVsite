using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Website.Dtos;
using Vsite.Application.Website.Versioning;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Exceptions;

namespace Vsite.Application.Website.Queries.GetPageDraft;

/// <summary>
/// Query theo <c>PageId</c> + <c>ShopId</c> (Global Filter áp thêm) và join Page còn sống — ownership nằm TRONG câu query (#21.3), không load rồi so.
/// Tree trả về đã qua upcaster (#94): bản cũ trong DB được nâng lên hiện hành khi đọc. <c>Version</c> = <c>xmin</c> → <c>long</c> (G5).
/// </summary>
public sealed class GetPageDraftHandler(IAppDbContext db, ITenantContext tenant, DocumentVersioning versioning)
    : IRequestHandler<GetPageDraftQuery, PageDraftDto>
{
    public async Task<PageDraftDto> Handle(GetPageDraftQuery request, CancellationToken cancellationToken)
    {
        var shopId = tenant.RequireShopId();

        var row = await (
                from d in db.PageDrafts.AsNoTracking()
                join p in db.Pages.AsNoTracking() on d.Id equals p.Id
                where d.Id == request.PageId && d.ShopId == shopId && p.ShopId == shopId
                select new
                {
                    d.Tree,
                    d.Version,
                    d.IsDirty,
                    d.UpdatedAt,
                    d.CreatedAt,
                    PageId = p.Id,
                    p.Kind,
                    p.SystemType,
                })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Page", request.PageId);

        return new PageDraftDto(
            StoredDocuments.ReadCurrent(DocumentKind.Tree, row.Tree, versioning).ToJsonElement(),
            row.Version,
            row.IsDirty,
            row.UpdatedAt ?? row.CreatedAt,
            new PageDraftPageDto(row.PageId, row.Kind, row.SystemType));
    }
}
