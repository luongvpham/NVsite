using MediatR;
using Vsite.Application.Shop.Dtos;

namespace Vsite.Application.Shop.Queries.ListShops;

/// <summary>
/// SHOP-001 §7 Quyết định 1 — thay thế hoàn toàn `GET /auth/me/shops` (Identity). Vừa là mục tiêu
/// test `RequireGlobalScope` (Quyết định #32 — token `shop:{shopId}` không được liệt kê MỌI shop
/// user sở hữu), vừa đủ dữ liệu cho shop switcher ở Portal.
/// </summary>
public sealed record ListShopsQuery : IRequest<IReadOnlyList<ShopSummaryDto>>;
