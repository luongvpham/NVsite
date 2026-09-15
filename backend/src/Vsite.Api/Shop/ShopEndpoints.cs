using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vsite.Api.Tenancy;
using Vsite.Application.Shop.Commands.CreateShop;
using Vsite.Application.Shop.Commands.UpdateShop;
using Vsite.Application.Shop.Dtos;
using Vsite.Application.Shop.Queries.GetShop;
using Vsite.Application.Shop.Queries.ListShops;
using Vsite.Domain.Authorization;
using Vsite.Domain.Shop.Enums;

namespace Vsite.Api.Shop;

/// <summary>
/// SHOP-001 §4.4 — 4 endpoint, tất cả `RequireGlobalScope` (Quyết định #32: token `shop:{shopId}`
/// không được tạo/sửa shop, đó là thao tác quản trị Portal/Main). `GET`/`PATCH /shops/{shopId}`
/// gọi <see cref="ShopScopedEndpointExtensions.RequireShopMembership"/> (SHOP-001 §3) để
/// resolve/validate `ShopId` từ route — trường hợp DUY NHẤT `ShopId` không đến từ Host (Quyết định
/// #31). Mọi endpoint có `{shopId}` trong route BẮT BUỘC gọi method này (không gọi thẳng
/// `.AddEndpointFilter&lt;ShopMembershipEndpointFilter&gt;()`) — `ShopScopedRouteFilterTests` sẽ đỏ
/// nếu quên.
/// </summary>
public static class ShopEndpoints
{
    public static IEndpointRouteBuilder MapShopEndpoints(this IEndpointRouteBuilder app)
    {
        var shops = app.MapGroup("/shops").WithTags("Shop").WithGroupName("shop");

        shops.MapPost("", async (CreateShopRequest body, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new CreateShopCommand(body.Name, body.Slug, body.Kind, body.ExternalUrl), ct)))
            .RequireAuthorization(AuthPolicies.RequireGlobalScope)
            .Produces<ShopDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        shops.MapGet("", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListShopsQuery(), ct)))
            .RequireAuthorization(AuthPolicies.RequireGlobalScope)
            .Produces<IReadOnlyList<ShopSummaryDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        shops.MapGet("/{shopId:guid}", async (Guid shopId, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetShopQuery(shopId), ct)))
            .RequireAuthorization(AuthPolicies.RequireGlobalScope)
            .RequireShopMembership()
            .Produces<ShopDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        shops.MapPatch("/{shopId:guid}", async (Guid shopId, UpdateShopRequest body, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new UpdateShopCommand(shopId, body.Name, body.Slug, body.Kind, body.ExternalUrl, body.Status), ct)))
            .RequireAuthorization(AuthPolicies.RequireGlobalScope)
            .RequireShopMembership()
            .Produces<ShopDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}

public sealed record CreateShopRequest(string Name, string Slug, ShopKind Kind, string? ExternalUrl);
public sealed record UpdateShopRequest(string Name, string Slug, ShopKind Kind, string? ExternalUrl, ShopStatus Status);
