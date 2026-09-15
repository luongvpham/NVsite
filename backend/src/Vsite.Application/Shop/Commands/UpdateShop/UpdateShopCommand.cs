using MediatR;
using Vsite.Application.Shop.Dtos;
using Vsite.Domain.Shop.Enums;

namespace Vsite.Application.Shop.Commands.UpdateShop;

/// <summary>
/// SHOP-001 §4.4 — `ShopId` đến từ route `/shops/{shopId}` (không phải body, Quyết định #21.4),
/// đã qua `Vsite.Api.Tenancy.ShopMembershipEndpointFilter` xác nhận membership trước khi tới đây.
/// Sửa toàn bộ field editable của Shop (không phải partial patch từng field) — giả định ghi ở
/// `Docs/tasks/SHOP-001/contract-diff.md`.
/// </summary>
public sealed record UpdateShopCommand(Guid ShopId, string Name, string Slug, ShopKind Kind, string? ExternalUrl, ShopStatus Status)
    : IRequest<ShopDto>;
