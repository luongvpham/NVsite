using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vsite.Application.Common.Imaging;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Common.Jobs;
using Vsite.Application.Common.Persistence;
using Vsite.Application.Media.Interfaces;
using Vsite.Application.Media.Options;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Media.Enums;

namespace Vsite.Application.Media.Jobs;

/// <summary>
/// WEBSITE-008 (#117) — dọn ảnh mồ côi của shop hiện tại (<see cref="ITenantContext"/>), hai bước:
/// <list type="number">
/// <item><c>used</c> = hợp mọi <see cref="IMediaUsageSource"/> ∪ <c>Shop.LogoId</c>; phái sinh được coi là dùng khi chính nó, hoặc bản gốc
/// của nó, ∈ <c>used</c>, hoặc bản gốc là <c>Library</c> còn sống.</item>
/// <item>Ứng viên = <c>Clone</c>/<c>Direct</c>/<c>Derivative</c>, hoặc <c>Library</c> đã xoá mềm. <c>Library</c> còn sống KHÔNG BAO GIỜ là ứng viên.</item>
/// <item>Ứng viên không dùng, tạo trước <c>now − grace</c>, chưa đánh dấu ⇒ <c>MarkOrphaned(now)</c>. Đã đánh dấu mà không còn là
/// "ứng viên không dùng" ⇒ <c>ClearOrphaned</c>.</item>
/// <item>Đã đánh dấu trước <c>now − grace</c> và vẫn không dùng ⇒ mỗi ảnh MỘT transaction: xoá record → xoá file → commit. Lỗi (FK còn giữ,
/// storage ném…) ⇒ rollback, record + file + dấu còn nguyên, log Warning, sang ảnh khác (R6).</item>
/// </list>
/// Nguồn "đang dùng" ném ⇒ log Error rồi ném lại TRƯỚC khi ghi bất cứ gì (R5, Hangfire retry).
/// Đọc <c>Library</c> đã xoá mềm cần <c>IgnoreQueryFilters()</c> (tắt cả filter tenant) ⇒ mọi query mang <c>ShopId == shopId</c> tường minh.
/// Xoá cứng bằng <c>ExecuteDelete</c> vì <c>Remove</c> bị interceptor đổi thành xoá mềm.
/// </summary>
public sealed class MediaOrphanCleanupJob(
    IAppDbContext db,
    ITenantContext tenant,
    IEnumerable<IMediaUsageSource> usageSources,
    IObjectStorage storage,
    IOptions<MediaCleanupOptions> options,
    TimeProvider time,
    ILogger<MediaOrphanCleanupJob> logger) : IShopJob
{
    /// <summary>Số record nạp/ghi mỗi lô khi đánh dấu / bỏ đánh dấu.</summary>
    internal const int BatchSize = 500;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var shopId = tenant.ShopId ?? throw new InvalidOperationException("Job dọn ảnh mồ côi chạy ngoài scope tenant (#116).");
        var now = time.GetUtcNow();
        var cutoff = now - options.Value.OrphanGrace;

        var used = await CollectUsedAsync(shopId, cancellationToken);

        var rows = await db.MediaAssets.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.ShopId == shopId)
            .Select(m => new Row(m.Id, m.Kind, m.IsDeleted, m.SourceAssetId, m.CreatedAt, m.OrphanedAt, m.StorageKey))
            .ToListAsync(cancellationToken);

        var liveLibrary = rows.Where(r => r.Kind == MediaAssetKind.Library && !r.IsDeleted).Select(r => r.Id).ToHashSet();

        bool IsUnusedCandidate(Row r, HashSet<Guid> inUse)
        {
            var candidate = r.Kind != MediaAssetKind.Library || r.IsDeleted;
            if (!candidate || inUse.Contains(r.Id))
            {
                return false;
            }

            return !(r.Kind == MediaAssetKind.Derivative && r.SourceAssetId is { } source
                && (inUse.Contains(source) || liveLibrary.Contains(source)));
        }

        var toClear = rows.Where(r => r.OrphanedAt is not null && !IsUnusedCandidate(r, used)).Select(r => r.Id).ToList();
        var toMark = rows.Where(r => r.OrphanedAt is null && r.CreatedAt < cutoff && IsUnusedCandidate(r, used)).Select(r => r.Id).ToList();
        var due = rows.Where(r => r.OrphanedAt < cutoff && IsUnusedCandidate(r, used)).ToList();

        var cleared = await UpdateMarksAsync(shopId, toClear, mark: false, now, cancellationToken);
        var marked = await UpdateMarksAsync(shopId, toMark, mark: true, now, cancellationToken);

        // Thu lại "đang dùng" ngay trước pha xoá: ảnh được đưa lại vào draft/snapshot trong lúc job chạy (R4) thì không xoá. Ảnh bị
        // loại ở đây GIỮ dấu — lần chạy sau thấy nó được dùng và bỏ dấu (bước clear). Nguồn ném ⇒ dừng, không xoá gì (R5).
        // Cửa sổ còn lại: ảnh được đưa lại vào draft SAU lần thu này mà trước khi vòng xoá tới nó — chỉ trong thời gian chạy vòng xoá,
        // và ảnh phải đã không dùng ≥ 2 × grace. Chấp nhận (WEBSITE-008, ghi ở changelog).
        var usedNow = due.Count == 0 ? used : await CollectUsedAsync(shopId, cancellationToken);
        var toDelete = due.Where(r => IsUnusedCandidate(r, usedNow)).ToList();

        var deleted = 0;
        foreach (var row in toDelete)
        {
            if (await TryDeleteAsync(shopId, row, cutoff, cancellationToken))
            {
                deleted++;
            }
        }

        logger.LogInformation(
            "Dọn ảnh mồ côi shop {ShopId}: bỏ đánh dấu {Cleared}, đánh dấu {Marked}, xoá {Deleted}/{Due} (bỏ qua {Reused} vừa dùng lại).",
            shopId, cleared, marked, deleted, toDelete.Count, due.Count - toDelete.Count);
    }

    private async Task<HashSet<Guid>> CollectUsedAsync(Guid shopId, CancellationToken ct)
    {
        var used = new HashSet<Guid>();
        try
        {
            foreach (var source in usageSources)
            {
                used.UnionWith(await source.CollectUsedAssetIdsAsync(ct));
            }

            var logoId = await db.Shops.AsNoTracking()
                .Where(s => s.Id == shopId)
                .Select(s => s.LogoId)
                .SingleOrDefaultAsync(ct);
            if (logoId is { } logo)
            {
                used.Add(logo);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Dọn ảnh mồ côi shop {ShopId} dừng: không đọc được nguồn ảnh đang dùng — không đánh dấu/xoá gì (R5).", shopId);
            throw;
        }

        return used;
    }

    /// <summary>Đánh dấu/bỏ đánh dấu qua method của entity (MarkOrphaned giữ invariant Library sống), theo lô.</summary>
    private async Task<int> UpdateMarksAsync(Guid shopId, List<Guid> ids, bool mark, DateTimeOffset now, CancellationToken ct)
    {
        var changed = 0;
        foreach (var chunk in ids.Chunk(BatchSize))
        {
            var assets = await db.MediaAssets.IgnoreQueryFilters()
                .Where(m => m.ShopId == shopId && chunk.Contains(m.Id))
                .ToListAsync(ct);
            foreach (var asset in assets)
            {
                if (!mark)
                {
                    asset.ClearOrphaned();
                    changed++;
                }
                else if (asset.Kind != MediaAssetKind.Library || asset.IsDeleted)
                {
                    // Re-check trên dòng vừa nạp: dữ liệu có thể đổi sau lần đọc đầu; không bao giờ để Library sống tới MarkOrphaned.
                    asset.MarkOrphaned(now);
                    changed++;
                }
            }

            await db.SaveChangesAsync(ct);
        }

        return changed;
    }

    private async Task<bool> TryDeleteAsync(Guid shopId, Row row, DateTimeOffset cutoff, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Điều kiện xoá viết lại trong SQL: dòng đã bị bỏ đánh dấu / đổi trạng thái giữa chừng thì không khớp ⇒ không xoá.
            var removed = await db.MediaAssets.IgnoreQueryFilters()
                .Where(m => m.ShopId == shopId && m.Id == row.Id && m.OrphanedAt != null && m.OrphanedAt < cutoff
                    && (m.Kind != MediaAssetKind.Library || m.IsDeleted))
                .ExecuteDeleteAsync(ct);
            if (removed == 0)
            {
                await transaction.RollbackAsync(ct);
                return false;
            }

            await storage.DeleteAsync(row.StorageKey, ct);
            await transaction.CommitAsync(ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (Exception rollbackEx)
            {
                logger.LogWarning(rollbackEx, "Rollback xoá ảnh mồ côi {AssetId} (shop {ShopId}) lỗi.", row.Id, shopId);
            }

            logger.LogWarning(
                ex,
                "Không xoá được ảnh mồ côi {AssetId} (shop {ShopId}, {Reason}) — giữ record, file và dấu, lần sau thử lại.",
                row.Id, shopId, UniqueViolation.IsForeignKeyViolation(ex) ? "FK còn giữ" : "lỗi xoá");
            return false;
        }
    }

    private sealed record Row(
        Guid Id, MediaAssetKind Kind, bool IsDeleted, Guid? SourceAssetId, DateTimeOffset CreatedAt, DateTimeOffset? OrphanedAt, string StorageKey);
}
