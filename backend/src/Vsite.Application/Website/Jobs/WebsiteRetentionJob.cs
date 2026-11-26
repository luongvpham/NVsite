using Microsoft.Extensions.Logging;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Common.Jobs;
using Vsite.Domain.Abstractions;

namespace Vsite.Application.Website.Jobs;

/// <summary>
/// WEBSITE-008 (#117, #118) — job theo shop của Website: <see cref="PublicationPruner"/> → <see cref="DeletedPagePurger"/> →
/// <see cref="NavigationOrphanCleaner"/>. Mỗi bước tự lưu và chạy lại an toàn; thứ tự cố định để bước sau thấy kết quả bước trước.
/// Ảnh mồ côi do <c>MediaOrphanCleanupJob</c> chạy riêng (sau job này) để thấy cả kết quả purge cùng ngày.
/// </summary>
public sealed class WebsiteRetentionJob(
    PublicationPruner pruner,
    DeletedPagePurger purger,
    NavigationOrphanCleaner cleaner,
    ITenantContext tenant,
    ILogger<WebsiteRetentionJob> logger) : IShopJob
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var shopId = tenant.RequireShopId();

        var pruned = await pruner.PruneAsync(cancellationToken);
        logger.LogInformation("website.retention shop {ShopId}: xoá {Count} bản publish cũ.", shopId, pruned);

        var purged = await purger.PurgeAsync(cancellationToken);
        logger.LogInformation("website.retention shop {ShopId}: dọn draft của {Count} trang đã xoá.", shopId, purged);

        var cleaned = await cleaner.CleanAsync(cancellationToken);
        logger.LogInformation("website.retention shop {ShopId}: dọn mục mồ côi ở {Count} menu.", shopId, cleaned);
    }
}
