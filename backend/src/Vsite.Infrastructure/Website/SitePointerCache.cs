using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Distributed;
using Vsite.Application.Website.Interfaces;

namespace Vsite.Infrastructure.Website;

/// <summary>
/// <see cref="ISitePointerCache"/> trên <c>IDistributedCache</c> (Redis) — cùng khuôn <c>ShopLookupService</c>. Giá trị là JSON
/// <c>{ websiteStatus, publicationId }</c>; "chưa có website" là <c>{null,null}</c> (khác hụt cache = không có key). Key theo shop,
/// không có tham số nào khác → không thể trộn con trỏ giữa hai shop.
/// </summary>
public sealed class SitePointerCache(IDistributedCache cache) : ISitePointerCache
{
    // Quyết định #109 + review T4: giữ 5' — lỗi xoá cache sau commit chỉ log, TTL là lưới an toàn duy nhất.
    private static readonly DistributedCacheEntryOptions WithWebsite = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };

    private static readonly DistributedCacheEntryOptions WithoutWebsite = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<SitePointer?> GetAsync(Guid shopId, CancellationToken cancellationToken)
    {
        var raw = await cache.GetStringAsync(Key(shopId), cancellationToken);
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SitePointer>(raw, Json);
        }
        catch (JsonException)
        {
            // Giá trị hỏng = coi như hụt; handler đọc DB rồi ghi đè.
            return null;
        }
    }

    public Task SetAsync(Guid shopId, SitePointer pointer, CancellationToken cancellationToken) =>
        cache.SetStringAsync(
            Key(shopId),
            JsonSerializer.Serialize(pointer, Json),
            pointer.WebsiteStatus is null ? WithoutWebsite : WithWebsite,
            cancellationToken);

    public Task InvalidateAsync(Guid shopId, CancellationToken cancellationToken) => cache.RemoveAsync(Key(shopId), cancellationToken);

    private static string Key(Guid shopId) => $"site:pointer:{shopId:N}";
}
