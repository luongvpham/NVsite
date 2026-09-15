using MediatR;
using Vsite.Application.Shop.Dtos;

namespace Vsite.Application.Shop.Queries.GetShop;

/// <summary>`ShopId` từ route `/shops/{shopId}` — đã qua `ShopMembershipEndpointFilter` (SHOP-001 §3)
/// trước khi tới đây, handler không cần re-check membership.</summary>
public sealed record GetShopQuery(Guid ShopId) : IRequest<ShopDto>;
