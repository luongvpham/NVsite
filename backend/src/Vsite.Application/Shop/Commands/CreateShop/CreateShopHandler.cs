using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Shop.Dtos;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Identity;
using Vsite.Domain.Identity.Entities;
using Vsite.Domain.Identity.Enums;
using ShopEntity = Vsite.Domain.Shop.Entities.Shop;

namespace Vsite.Application.Shop.Commands.CreateShop;

public sealed class CreateShopHandler(IAppDbContext db, ICurrentUserContext currentUser)
    : IRequestHandler<CreateShopCommand, ShopDto>
{
    public async Task<ShopDto> Handle(CreateShopCommand request, CancellationToken cancellationToken)
    {
        var slugTaken = await db.Shops.AnyAsync(s => s.Slug == request.Slug, cancellationToken);
        if (slugTaken)
        {
            throw new ConflictException("SHOP_SLUG_ALREADY_TAKEN", "Slug này đã được dùng bởi shop khác.");
        }

        var shop = new ShopEntity
        {
            Name = request.Name,
            Slug = request.Slug,
            Kind = request.Kind,
            ExternalUrl = request.ExternalUrl,
        };
        db.Shops.Add(shop);

        // 03 §3.3 — nhánh ShopCreator, nhánh DUY NHẤT sinh role Owner. MỘT SaveChangesAsync cho cả
        // Shop lẫn UserShop (SHOP-001 §4.4) — không có Shop nào tồn tại mà thiếu Owner.
        db.UserShops.Add(new UserShop
        {
            UserId = currentUser.UserId,
            ShopId = shop.Id,
            RoleId = WellKnownRoles.OwnerId,
            Source = UserShopSource.ShopCreator,
        });

        await db.SaveChangesAsync(cancellationToken);

        return new ShopDto(shop.Id, shop.Name, shop.Slug, shop.Kind, shop.ExternalUrl, shop.Status);
    }
}
