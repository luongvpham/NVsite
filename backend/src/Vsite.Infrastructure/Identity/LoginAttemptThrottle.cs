using Microsoft.Extensions.Caching.Distributed;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Identity.Interfaces;

namespace Vsite.Infrastructure.Identity;

/// <summary>Redis-backed, đếm theo `(emailNormalized, audience)` — xem <see cref="ILoginAttemptThrottle"/>.
/// Không tuyệt đối atomic (get-then-set, không dùng INCR nguyên tử) — chấp nhận được cho ngưỡng
/// lockout thô, không phải bộ đếm chính xác tuyệt đối.</summary>
public sealed class LoginAttemptThrottle(IDistributedCache cache) : ILoginAttemptThrottle
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    public async Task<bool> IsLockedOutAsync(string key, CancellationToken cancellationToken)
    {
        var raw = await cache.GetStringAsync(CacheKey(key), cancellationToken);
        return int.TryParse(raw, out var count) && count >= MaxAttempts;
    }

    public async Task RecordFailureAsync(string key, CancellationToken cancellationToken)
    {
        var cacheKey = CacheKey(key);
        var raw = await cache.GetStringAsync(cacheKey, cancellationToken);
        var count = (int.TryParse(raw, out var existing) ? existing : 0) + 1;

        await cache.SetStringAsync(
            cacheKey,
            count.ToString(),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Window },
            cancellationToken);
    }

    public Task ResetAsync(string key, CancellationToken cancellationToken) =>
        cache.RemoveAsync(CacheKey(key), cancellationToken);

    private static string CacheKey(string key) => $"login-fail:{key}";
}
