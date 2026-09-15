using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Vsite.Application.Shop.Interfaces;
using Vsite.Infrastructure.Persistence;

namespace Vsite.Infrastructure.Shop;

/// <summary>
/// Implementation thật của <see cref="IShopLookupService"/> — cache qua Redis (Quyết định #7),
/// vì middleware gọi hàm này ở MỌI request. Cache CẢ kết quả "không tìm thấy" (negative caching,
/// TTL ngắn hơn) để một domain lạ/không tồn tại không dội thẳng vào DB ở mỗi request.
/// </summary>
public sealed class ShopLookupService(AppDbContext db, IDistributedCache cache) : IShopLookupService
{
    private const string NotFoundSentinel = "";

    private static readonly DistributedCacheEntryOptions FoundOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
    };

    // Ngắn hơn nhiều so với FoundOptions — một slug vừa được đăng ký không nên "biến mất" trong
    // cache negative quá lâu nếu ai đó lỡ tra nó trước khi Shop được tạo.
    private static readonly DistributedCacheEntryOptions NotFoundOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1),
    };

    public async Task<Guid?> FindShopIdBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        var key = CacheKey(slug);
        var cached = await cache.GetStringAsync(key, cancellationToken);

        if (cached is not null)
        {
            return cached == NotFoundSentinel ? null : Guid.Parse(cached);
        }

        var shopId = await db.Shops
            .Where(s => s.Slug == slug)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        await cache.SetStringAsync(
            key,
            shopId?.ToString() ?? NotFoundSentinel,
            shopId is null ? NotFoundOptions : FoundOptions,
            cancellationToken);

        return shopId;
    }

    public Task InvalidateAsync(string slug, CancellationToken cancellationToken) =>
        cache.RemoveAsync(CacheKey(slug), cancellationToken);

    private static string CacheKey(string slug) => $"shop:slug:{slug}";
}
