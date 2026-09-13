using MediatR;

namespace Vsite.Application.Identity.Auth.Queries.ListMyShops;

/// <summary>
/// Quyết định #32 — "Liệt kê shop khác mà user thuộc về ❌ phá ảo giác tách biệt + lộ đời tư" khi
/// gọi từ token `shop:{shopId}`. Endpoint bắt buộc policy `RequireGlobalScope`.
/// </summary>
public sealed record ListMyShopsQuery : IRequest<IReadOnlyList<MyShopDto>>;

public sealed record MyShopDto(Guid ShopId, string RoleCode);
