using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Shop.Dtos;
using Vsite.Application.Shop.Interfaces;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Identity;
using Vsite.Domain.Identity.Enums;

namespace Vsite.Application.Shop.Commands.UpdateShop;

/// <summary>
/// SHOP-001 §4.4 — chỉ role `Owner` được sửa shop (bảng năng lực endpoint). Membership (còn
/// `Active`) đã được `ShopMembershipEndpointFilter` xác nhận trước khi request tới đây; handler tự
/// tra lại `RoleId` vì filter dùng chung không biết business rule "chỉ Owner" của riêng endpoint này.
///
/// ⚠️ 04 §2.2 — đổi `Hosted → ExternalOnly` PHẢI chuyển/gỡ mọi `Listing` đang trỏ `ShopHome`/
/// `ShopPage`, nếu không listing sẽ 404 cho khách từ vsite. `Listing` CHƯA tồn tại (module
/// `Marketplace` chưa làm) nên ràng buộc này để RỖNG ở đây — xem
/// `Docs/tasks/SHOP-001/changelog.md` và test `[Fact(Skip = ...)]` tương ứng.
/// </summary>
public sealed class UpdateShopHandler(IAppDbContext db, ICurrentUserContext currentUser, IShopLookupService shopLookup)
    : IRequestHandler<UpdateShopCommand, ShopDto>
{
    public async Task<ShopDto> Handle(UpdateShopCommand request, CancellationToken cancellationToken)
    {
        var shop = await db.Shops.FirstOrDefaultAsync(s => s.Id == request.ShopId, cancellationToken)
            ?? throw new NotFoundException("Shop", request.ShopId);

        // IgnoreQueryFilters — đây là bước xác lập quyền (giống UserShopMembershipService), không
        // phải đọc dữ liệu trong một tenant đã biết trước.
        var membership = await db.UserShops.IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                us => us.UserId == currentUser.UserId && us.ShopId == request.ShopId && us.Status == UserShopStatus.Active,
                cancellationToken)
            ?? throw new ForbiddenAccessException("SHOP_ACCESS_DENIED", "Không có quyền truy cập shop này.");

        if (membership.RoleId != WellKnownRoles.OwnerId)
        {
            throw new ForbiddenAccessException("SHOP_OWNER_REQUIRED", "Chỉ chủ shop (Owner) mới được sửa thông tin shop.");
        }

        var slugChanged = !string.Equals(shop.Slug, request.Slug, StringComparison.Ordinal);
        if (slugChanged)
        {
            var slugTaken = await db.Shops.AnyAsync(s => s.Slug == request.Slug && s.Id != shop.Id, cancellationToken);
            if (slugTaken)
            {
                throw new ConflictException("SHOP_SLUG_ALREADY_TAKEN", "Slug này đã được dùng bởi shop khác.");
            }
        }

        var oldSlug = shop.Slug;
        shop.Name = request.Name;
        shop.Slug = request.Slug;
        shop.Kind = request.Kind;
        shop.ExternalUrl = request.ExternalUrl;
        shop.Status = request.Status;

        await db.SaveChangesAsync(cancellationToken);

        if (slugChanged)
        {
            // backend/docs/modules/identity.md — quên bước này = middleware resolve sai tenant tới
            // khi cache Redis tự hết hạn (tối đa 30 phút). Cả slug CŨ lẫn MỚI.
            await shopLookup.InvalidateAsync(oldSlug, cancellationToken);
            await shopLookup.InvalidateAsync(request.Slug, cancellationToken);
        }

        return new ShopDto(shop.Id, shop.Name, shop.Slug, shop.Kind, shop.ExternalUrl, shop.Status);
    }
}
