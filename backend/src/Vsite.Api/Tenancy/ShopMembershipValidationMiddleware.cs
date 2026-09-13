using Microsoft.AspNetCore.Mvc;
using Vsite.Application.Identity.Interfaces;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Authorization;

namespace Vsite.Api.Tenancy;

/// <summary>
/// Quyết định #21.5/#31 — JWT hợp lệ về chữ ký/hạn dùng KHÔNG đồng nghĩa quyền còn hiệu lực. Chạy
/// SAU `UseAuthentication` (cần `ClaimsPrincipal` đã populate) — nếu token có audience
/// `shop:{shopId}`, re-check `UserShop(userId, shopId)` còn `Active` thật ở DB tại request này,
/// KHÔNG tin claim trong token. Đồng thời đối chiếu `shopId` trong token với `TenantContext.ShopId`
/// (resolve từ Host bởi `TenantResolutionMiddleware`) — token của shop A dùng trên domain shop B bị
/// chặn ở đây, dù cả hai đều "hợp lệ" xét riêng lẻ.
/// </summary>
public sealed class ShopMembershipValidationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext, IUserShopMembershipService membershipService)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var audience = context.User.FindFirst("aud")?.Value;
            var tokenShopId = AudienceHelpers.TryGetShopId(audience);

            if (tokenShopId is not null)
            {
                // Token issued cho shop khác với domain đang phục vụ request này.
                if (tokenShopId != tenantContext.ShopId)
                {
                    await WriteForbiddenAsync(context, "TOKEN_SHOP_MISMATCH", "Token không thuộc shop này.");
                    return;
                }

                var userIdClaim = context.User.FindFirst("sub")?.Value;
                if (!Guid.TryParse(userIdClaim, out var userId) ||
                    !await membershipService.IsActiveMemberAsync(userId, tokenShopId.Value, context.RequestAborted))
                {
                    await WriteForbiddenAsync(context, "SHOP_MEMBERSHIP_REVOKED", "Quyền truy cập shop này đã bị thu hồi.");
                    return;
                }
            }
        }

        await next(context);
    }

    private static async Task WriteForbiddenAsync(HttpContext context, string errorCode, string detail)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Detail = detail,
            Extensions = { ["error_code"] = errorCode },
        });
    }
}
