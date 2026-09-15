using MediatR;
using Vsite.Application.Shop.Dtos;
using Vsite.Domain.Shop.Enums;

namespace Vsite.Application.Shop.Commands.CreateShop;

/// <summary>
/// SHOP-001 §3/§4.4 — nhánh DUY NHẤT sinh `UserShop(Owner, ShopCreator)` (03 §3.3). Người gọi là
/// user hiện tại (`ICurrentUserContext`, không nhận từ body) — tạo shop xong tự động thành Owner.
/// </summary>
public sealed record CreateShopCommand(string Name, string Slug, ShopKind Kind, string? ExternalUrl) : IRequest<ShopDto>;
