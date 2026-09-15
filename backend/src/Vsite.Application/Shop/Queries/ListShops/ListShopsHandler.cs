using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Shop.Dtos;
using Vsite.Domain.Identity.Enums;

namespace Vsite.Application.Shop.Queries.ListShops;

public sealed class ListShopsHandler(IAppDbContext db, ICurrentUserContext currentUser)
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
            select new ShopSummaryDto(s.Id, s.Name, s.Slug, s.Kind, s.Status, r.Code);

        return await query.ToListAsync(cancellationToken);
    }
}
