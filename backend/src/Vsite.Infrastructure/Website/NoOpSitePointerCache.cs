using Vsite.Application.Website.Interfaces;

namespace Vsite.Infrastructure.Website;

/// <summary>
/// TẠM (WEBSITE-004 T4): chưa có con trỏ Redis nên không có gì để xoá. T6 thay bằng <c>SitePointerCache</c> thật trong
/// <c>DependencyInjection.AddWebsiteModule</c> — xoá file này khi đó.
/// </summary>
public sealed class NoOpSitePointerCache : ISitePointerCache
{
    public Task InvalidateAsync(Guid shopId, CancellationToken cancellationToken) => Task.CompletedTask;
}
