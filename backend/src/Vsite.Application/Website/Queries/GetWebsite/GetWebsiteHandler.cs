using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Website.Dtos;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Website.Enums;

namespace Vsite.Application.Website.Queries.GetWebsite;

public sealed class GetWebsiteHandler(IAppDbContext db, ITenantContext tenant) : IRequestHandler<GetWebsiteQuery, WebsiteDto>
{
    public async Task<WebsiteDto> Handle(GetWebsiteQuery request, CancellationToken cancellationToken)
    {
        var shopId = tenant.RequireShopId();

        var website = await db.Websites.AsNoTracking().FirstOrDefaultAsync(w => w.ShopId == shopId, cancellationToken)
            ?? throw new NotFoundException("Website", shopId);

        // Trang chủ = Composable có Slug "" (không có cột IsHomePage, `05` §4).
        var homePageId = await db.Pages
            .Where(p => p.ShopId == shopId && p.WebsiteId == website.Id && p.Kind == PageKind.Composable && p.Slug == "")
            .Select(p => p.Id)
            .FirstAsync(cancellationToken);

        return Mappings.ToDto(website, homePageId);
    }
}
