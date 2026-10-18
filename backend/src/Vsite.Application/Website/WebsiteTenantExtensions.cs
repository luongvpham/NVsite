using System.Text.Json;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Exceptions;

namespace Vsite.Application.Website;

/// <summary>Tiện ích dùng chung cho handler Website (#99).</summary>
internal static class WebsiteTenantExtensions
{
    /// <summary>
    /// #99 — handler tenant-scoped lấy <c>ShopId</c> từ <see cref="ITenantContext"/> (không từ request). Null = fail-closed:
    /// endpoint quên <c>RequireShopOwner</c> (filter không set tenant) thì request bị từ chối chứ không chạy với filter rỗng.
    /// </summary>
    public static Guid RequireShopId(this ITenantContext tenant) =>
        tenant.ShopId ?? throw new ForbiddenAccessException(
            "TENANT_REQUIRED", "Không xác định được shop hiện tại cho yêu cầu này.");

    /// <summary>Chuỗi JSON (jsonb) → <see cref="JsonElement"/> độc lập với <c>JsonDocument</c> (không cần dispose).</summary>
    public static JsonElement ToJsonElement(this string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
