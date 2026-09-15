using Microsoft.AspNetCore.Builder;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Identity.Interfaces;
using Vsite.Domain.Exceptions;

namespace Vsite.Api.Tenancy;

/// <summary>
/// Gắn CẢ <see cref="ShopMembershipEndpointFilter"/> LẪN một metadata marker discoverable được —
/// dùng đúng MỘT method này cho mọi endpoint có `{shopId}` trong route, KHÔNG gọi thẳng
/// `.AddEndpointFilter&lt;ShopMembershipEndpointFilter&gt;()`. Lý do: filter tự nó biên dịch thẳng
/// vào request pipeline, không để lại dấu vết ở `Endpoint.Metadata` — nếu không có marker này,
/// không cách nào viết test xác nhận "endpoint có filter hay chưa" từ bên ngoài.
/// `ShopScopedRouteFilterTests` (IntegrationTests) duyệt MỌI route có param `shopId`, khẳng định
/// marker này có mặt — quên gọi `RequireShopMembership()` là RED ngay, không cần ai nhớ (#17).
/// </summary>
public static class ShopScopedEndpointExtensions
{
    public static RouteHandlerBuilder RequireShopMembership(this RouteHandlerBuilder builder)
    {
        builder.AddEndpointFilter<ShopMembershipEndpointFilter>();
        builder.WithMetadata(new ShopMembershipRequiredMarker());
        return builder;
    }
}

/// <summary>Marker rỗng — chỉ để `EndpointDataSource`/test bên ngoài phát hiện được endpoint đã gắn
/// <see cref="ShopMembershipEndpointFilter"/> qua <see cref="ShopScopedEndpointExtensions"/>.</summary>
public sealed class ShopMembershipRequiredMarker;

/// <summary>
/// SHOP-001 §3/§7 Quyết định 3 — Quyết định #31 nói Portal lấy `ShopId` từ route
/// `/shops/{shopId}/...`, trường hợp DUY NHẤT `ShopId` không đến từ Host. Nhưng
/// `TenantResolutionMiddleware` set `ShopId = null` cho audience Portal, và Global Query Filter
/// fail-closed (`null` không khớp `Guid` non-null nào) → mọi query shop-scoped trả rỗng nếu không
/// có bước này.
///
/// Gắn vào MỌI endpoint Portal có `{shopId}` trong route (dùng chung, đặt ở `Vsite.Api.Tenancy` —
/// namespace dùng chung mọi module, không phải module cụ thể):
///   1. Đọc `shopId` từ route.
///   2. Query `UserShop(userId, shopId)` còn `Active` TẠI request này — không tin claim JWT
///      (invariant 7, tái dùng <see cref="IUserShopMembershipService"/> đã có sẵn cho
///      `ShopMembershipValidationMiddleware`, không viết lại query).
///   3. Không có record → 403, KHÔNG fallback role mặc định (invariant 8).
///   4. Có → set `TenantContext.ShopId = shopId` để Global Query Filter hoạt động đúng.
///
/// ⚠️ Giả định tôi đã tự đặt (ghi ở `Docs/tasks/SHOP-001/contract-diff.md`): sau bước này,
/// `TenantContext.ShopId` có giá trị dù `AudienceKind` vẫn là `Portal` (không đổi thành `Shop`) —
/// nới rộng invariant cũ "`ShopId` chỉ có giá trị khi `AudienceKind` = Shop" (xem
/// <see cref="Vsite.Domain.Abstractions.ITenantContext.ShopId"/>).
/// </summary>
public sealed class ShopMembershipEndpointFilter(TenantContext tenantContext, ICurrentUserContext currentUser, IUserShopMembershipService membershipService)
    : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var shopId = Guid.Parse((string)context.HttpContext.Request.RouteValues["shopId"]!);

        var isActiveMember = await membershipService.IsActiveMemberAsync(currentUser.UserId, shopId, context.HttpContext.RequestAborted);
        if (!isActiveMember)
        {
            throw new ForbiddenAccessException("SHOP_ACCESS_DENIED", "Không có quyền truy cập shop này.");
        }

        tenantContext.ShopId = shopId;

        return await next(context);
    }
}
