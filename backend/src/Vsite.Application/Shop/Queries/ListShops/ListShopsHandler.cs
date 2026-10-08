using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Identity;
using Vsite.Application.Shop.Dtos;
using Vsite.Application.Shop.Interfaces;

namespace Vsite.Application.Shop.Queries.ListShops;

public sealed class ListShopsHandler(IAppDbContext db, ICurrentUserContext currentUser, IShopLogoReader logoReader)
    : IRequestHandler<ListShopsQuery, IReadOnlyList<ShopSummaryDto>>
{
    public async Task<IReadOnlyList<ShopSummaryDto>> Handle(ListShopsQuery request, CancellationToken cancellationToken)
    {
        // Đọc UserShop XUYÊN shop qua helper (đúng ý nghĩa endpoint, chỉ cho phép ở RequireGlobalScope).
        // Helper bỏ query filter cho CẢ câu query (kể cả Shops/Roles được join) nên `!IsDeleted` của
        // Shop và Role vẫn phải viết tay (#21); điều kiện của UserShop đã nằm trong helper.
        var query =
            from us in db.UserShops.ActiveAcrossShops()
            join s in db.Shops on us.ShopId equals s.Id
            join r in db.Roles on us.RoleId equals r.Id
            where us.UserId == currentUser.UserId && !s.IsDeleted && !r.IsDeleted
            select new { s.Id, s.Name, s.Slug, s.Kind, s.Status, RoleCode = r.Code };

        var shops = await query.ToListAsync(cancellationToken);

        // D4 (#88): logo tra THEO LÔ qua port (một câu SQL cho cả danh sách, không N+1).
        var logos = await logoReader.GetLogoUrlsAsync(shops.Select(s => s.Id).ToList(), cancellationToken);

        return shops
            .Select(s => new ShopSummaryDto(s.Id, s.Name, s.Slug, s.Kind, s.Status, s.RoleCode, logos.GetValueOrDefault(s.Id)))
            .ToList();
    }
}
