using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Shop.Dtos;
using Vsite.Application.Shop.Interfaces;
using Vsite.Domain.Exceptions;

namespace Vsite.Application.Shop.Queries.GetShop;

public sealed class GetShopHandler(IAppDbContext db, IShopLogoReader logoReader) : IRequestHandler<GetShopQuery, ShopDto>
{
    public async Task<ShopDto> Handle(GetShopQuery request, CancellationToken cancellationToken)
    {
        var shop = await db.Shops.FirstOrDefaultAsync(s => s.Id == request.ShopId, cancellationToken)
            ?? throw new NotFoundException("Shop", request.ShopId);

        var logoStorageKey = await logoReader.GetLogoStorageKeyAsync(shop.Id, shop.LogoId, cancellationToken);

        return new ShopDto(shop.Id, shop.Name, shop.Slug, shop.Kind, shop.ExternalUrl, shop.Status, shop.LogoId, logoStorageKey);
    }
}
