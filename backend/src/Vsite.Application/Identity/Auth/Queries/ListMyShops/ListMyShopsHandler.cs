using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;

namespace Vsite.Application.Identity.Auth.Queries.ListMyShops;

public sealed class ListMyShopsHandler(IAppDbContext db, ICurrentUserContext currentUser)
    : IRequestHandler<ListMyShopsQuery, IReadOnlyList<MyShopDto>>
{
    public async Task<IReadOnlyList<MyShopDto>> Handle(ListMyShopsQuery request, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters — đúng ý nghĩa endpoint là đọc XUYÊN shop (chỉ cho phép ở
        // RequireGlobalScope, enforce ở tầng endpoint/policy, không phải ở đây).
        var query =
            from us in db.UserShops.IgnoreQueryFilters()
            join r in db.Roles on us.RoleId equals r.Id
            where us.UserId == currentUser.UserId
            select new MyShopDto(us.ShopId, r.Code);

        return await query.ToListAsync(cancellationToken);
    }
}
