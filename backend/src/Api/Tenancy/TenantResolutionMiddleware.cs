using Shared.Domain.Abstractions;
using Shared.ReservedRoutes;

namespace Api.Tenancy;

/// <summary>
/// Quyết định #7, THU HẸP LẠI cho Bước 3 — chỉ resolve Path/Subdomain qua `Shop.Slug` (không có
/// `ShopDomain`/custom domain thật, deferred tới module Shop đầy đủ). Chạy SỚM trong pipeline,
/// TRƯỚC MediatR/mọi handler — không handler nào tự resolve ShopId, tất cả đọc từ
/// `ITenantContext` đã được middleware này set sẵn.
///
/// Thứ tự resolve theo nhãn đầu tiên của Host (vd. "spa-abc" trong "spa-abc.vsite.local:5270"):
///   1. Nhãn = "admin" (Quyết định #25, đã reserved) → audience `vsite-portal`.
///   2. Nhãn khớp một `Shop.Slug` đang tồn tại → audience `shop:{shopId}`.
///   3. Còn lại (kể cả reserved subdomain khác, hoặc không khớp Shop nào) → audience `vsite-main`.
///
/// ⚠️ Đây KHÔNG phải cách resolve custom domain thật (`spa-abc.com` bất kỳ) — chỉ đúng cho
/// Path/Subdomain dạng `{slug}.{host}` (kể cả dev qua hosts file, vd. `spa-abc.vsite.local`).
/// Custom domain cần bảng `ShopDomain` (module Shop đầy đủ) để match CHÍNH XÁC domain, không phải
/// suy luận từ nhãn đầu — xem CLAUDE.md module Identity.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext, IShopLookupService shopLookup, IReservedRoutesProvider reservedRoutes)
    {
        var hostLabel = context.Request.Host.Host.Split('.')[0].ToLowerInvariant();

        if (string.Equals(hostLabel, "admin", StringComparison.Ordinal))
        {
            tenantContext.AudienceKind = TenantAudienceKind.Portal;
            tenantContext.ShopId = null;
        }
        else if (reservedRoutes.Routes.ReservedSubdomains.Contains(hostLabel))
        {
            // "www", "api", "cdn"... — reserved, không thể là slug shop nào, khỏi query DB.
            tenantContext.AudienceKind = TenantAudienceKind.Main;
            tenantContext.ShopId = null;
        }
        else
        {
            var shopId = await shopLookup.FindShopIdBySlugAsync(hostLabel, context.RequestAborted);
            if (shopId is not null)
            {
                tenantContext.AudienceKind = TenantAudienceKind.Shop;
                tenantContext.ShopId = shopId;
            }
            else
            {
                tenantContext.AudienceKind = TenantAudienceKind.Main;
                tenantContext.ShopId = null;
            }
        }

        await next(context);
    }
}
