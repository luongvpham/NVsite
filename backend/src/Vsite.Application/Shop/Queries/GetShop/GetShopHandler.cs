using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Shop.Dtos;
using Vsite.Domain.Exceptions;

namespace Vsite.Application.Shop.Queries.GetShop;

public sealed class GetShopHandler(IAppDbContext db) : IRequestHandler<GetShopQuery, ShopDto>
{
    public async Task<ShopDto> Handle(GetShopQuery request, CancellationToken cancellationToken)
    {
        var shop = await db.Shops.FirstOrDefaultAsync(s => s.Id == request.ShopId, cancellationToken)
            ?? throw new NotFoundException("Shop", request.ShopId);

        return new ShopDto(shop.Id, shop.Name, shop.Slug, shop.Kind, shop.ExternalUrl, shop.Status);
    }
}
