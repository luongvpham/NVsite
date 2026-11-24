using Hangfire;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Common.Jobs;

namespace Vsite.Infrastructure.Jobs;

/// <summary>
/// #116 — job cha: liệt kê mọi shop chưa xoá mềm rồi enqueue MỘT job con mỗi shop
/// (<see cref="ShopJobRunner"/> → <typeparamref name="TJob"/> trong scope tenant của shop đó).
/// Không tắt bộ lọc truy vấn toàn cục — `Shop` không phải tenant entity nên bộ lọc xoá mềm là đủ.
/// </summary>
public sealed class ShopFanOut(IAppDbContext db, IBackgroundJobClient jobs)
{
    /// <summary>Mọi shop chưa xoá mềm (mọi `Status`: Draft/Active/Suspended/Closed), chỉ `Id`.</summary>
    public Task<List<Guid>> ListShopIdsAsync(CancellationToken cancellationToken = default) =>
        db.Shops.AsNoTracking().Select(s => s.Id).ToListAsync(cancellationToken);

    /// <summary>Enqueue một job con mỗi shop; trả số job đã enqueue.</summary>
    public async Task<int> EnqueuePerShop<TJob>(CancellationToken cancellationToken = default)
        where TJob : class, IShopJob
    {
        var shopIds = await ListShopIdsAsync(cancellationToken);
        foreach (var shopId in shopIds)
        {
            jobs.Enqueue<ShopJobRunner>(r => r.RunAsync<TJob>(shopId, CancellationToken.None));
        }

        return shopIds.Count;
    }
}
