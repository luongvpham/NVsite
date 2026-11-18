using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Website.Dtos;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Exceptions;

namespace Vsite.Application.Website.Queries.ListPublications;

/// <summary>Flow 3 (WEBSITE-004 T5). <c>Take(50)</c> mới nhất; chiếu cột tường minh để KHÔNG kéo <c>Snapshot</c> (vài trăm KB/bản).</summary>
public sealed class ListPublicationsHandler(IAppDbContext db, ITenantContext tenant)
    : IRequestHandler<ListPublicationsQuery, IReadOnlyList<PublicationDto>>
{
    public const int MaxItems = 50;

    public async Task<IReadOnlyList<PublicationDto>> Handle(ListPublicationsQuery request, CancellationToken cancellationToken)
    {
        var shopId = tenant.RequireShopId();

        var website = await db.Websites.AsNoTracking()
            .Where(w => w.ShopId == shopId)
            .Select(w => new { w.Id, w.CurrentPublicationId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Website", shopId);

        var rows = await db.SitePublications.AsNoTracking()
            .Where(p => p.ShopId == shopId && p.WebsiteId == website.Id)
            .OrderByDescending(p => p.Version)
            .Take(MaxItems)
            .Select(p => new { p.Id, p.Version, p.PublishedAt, p.PublishedByUserId, p.Note })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new PublicationDto(r.Id, r.Version, r.PublishedAt, r.PublishedByUserId, r.Note, r.Id == website.CurrentPublicationId))
            .ToList();
    }
}
