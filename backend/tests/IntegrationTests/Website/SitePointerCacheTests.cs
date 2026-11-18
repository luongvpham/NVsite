using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Vsite.Application.Website.Interfaces;
using Vsite.Domain.Website.Enums;
using Vsite.Infrastructure.Website;
using Vsite.IntegrationTests.Media;

namespace Vsite.IntegrationTests.Website;

/// <summary>T6, WEBSITE-004 — <see cref="SitePointerCache"/> trên <c>IDistributedCache</c> Redis thật (Testcontainers) + một spy để khoá TTL.</summary>
[Collection(MediaApiCollection.Name)]
public sealed class SitePointerCacheTests(MediaApiFactory factory)
{
    private SitePointerCache Real() => new(factory.Services.GetRequiredService<IDistributedCache>());

    [Fact]
    public async Task Miss_then_set_get_round_trips_and_invalidate_removes()
    {
        var cache = Real();
        var shopId = Guid.NewGuid();
        var publicationId = Guid.NewGuid();

        Assert.Null(await cache.GetAsync(shopId, default));

        await cache.SetAsync(shopId, new SitePointer(WebsiteStatus.Published, publicationId), default);
        var hit = await cache.GetAsync(shopId, default);
        Assert.Equal(new SitePointer(WebsiteStatus.Published, publicationId), hit);

        await cache.InvalidateAsync(shopId, default);
        Assert.Null(await cache.GetAsync(shopId, default));
    }

    [Fact]
    public async Task No_website_pointer_is_cached_and_distinct_from_a_miss()
    {
        var cache = Real();
        var shopId = Guid.NewGuid();

        await cache.SetAsync(shopId, new SitePointer(null, null), default);

        var hit = await cache.GetAsync(shopId, default);
        Assert.NotNull(hit);
        Assert.Null(hit!.WebsiteStatus);
        Assert.Null(hit.PublicationId);
    }

    [Fact]
    public async Task Key_is_per_shop_and_pointers_do_not_mix()
    {
        var cache = Real();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var pubA = Guid.NewGuid();

        await cache.SetAsync(a, new SitePointer(WebsiteStatus.Published, pubA), default);

        Assert.Null(await cache.GetAsync(b, default));
        Assert.Equal(pubA, (await cache.GetAsync(a, default))!.PublicationId);
        await cache.InvalidateAsync(a, default);
    }

    [Fact]
    public async Task Ttl_is_5_minutes_with_a_website_and_1_minute_without()
    {
        var spy = new SpyDistributedCache();
        var cache = new SitePointerCache(spy);
        var shopId = Guid.NewGuid();

        await cache.SetAsync(shopId, new SitePointer(WebsiteStatus.Published, Guid.NewGuid()), default);
        Assert.Equal($"site:pointer:{shopId:N}", spy.LastKey);
        Assert.Equal(TimeSpan.FromMinutes(5), spy.LastOptions!.AbsoluteExpirationRelativeToNow);

        await cache.SetAsync(shopId, new SitePointer(null, null), default);
        Assert.Equal(TimeSpan.FromMinutes(1), spy.LastOptions!.AbsoluteExpirationRelativeToNow);
    }

    private sealed class SpyDistributedCache : IDistributedCache
    {
        public string? LastKey { get; private set; }

        public DistributedCacheEntryOptions? LastOptions { get; private set; }

        public byte[]? Get(string key) => null;

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult<byte[]?>(null);

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Remove(string key)
        {
        }

        public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            LastKey = key;
            LastOptions = options;
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }
    }
}
