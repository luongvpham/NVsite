using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Shop.Dtos;
using Vsite.Application.Shop.Interfaces;
using Vsite.Domain.Identity.Enums;

namespace Vsite.Application.Shop.Queries.ListShops;

public sealed class ListShopsHandler(IAppDbContext db, ICurrentUserContext currentUser, IShopLogoReader logoReader)
    : IRequestHandler<ListShopsQuery, IReadOnlyList<ShopSummaryDto>>
{
    public async Task<IReadOnlyList<ShopSummaryDto>> Handle(ListShopsQuery request, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters trên UserShop — đúng ý nghĩa endpoint là đọc XUYÊN shop (chỉ cho phép
        // ở RequireGlobalScope, enforce ở tầng endpoint/policy, không phải ở đây).
        var query =
            from us in db.UserShops.IgnoreQueryFilters()
            join s in db.Shops on us.ShopId equals s.Id
            join r in db.Roles on us.RoleId equals r.Id
            where us.UserId == currentUser.UserId && us.Status == UserShopStatus.Active
            select new { s.Id, s.Name, s.Slug, s.Kind, s.Status, RoleCode = r.Code };

        var shops = await query.ToListAsync(cancellationToken);

        // D4 (#88): logo tra THEO LÔ qua port (một câu SQL cho cả danh sách, không N+1).
        var logos = await logoReader.GetLogoUrlsAsync(shops.Select(s => s.Id).ToList(), cancellationToken);

        return shops
            .Select(s => new ShopSummaryDto(s.Id, s.Name, s.Slug, s.Kind, s.Status, s.RoleCode, logos.GetValueOrDefault(s.Id)))
            .ToList();
    }
}
