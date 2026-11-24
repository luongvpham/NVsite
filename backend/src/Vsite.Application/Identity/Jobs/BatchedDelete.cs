using Microsoft.EntityFrameworkCore;
using Vsite.Domain.Common;

namespace Vsite.Application.Identity.Jobs;

/// <summary>
/// #116 ngoại lệ (a) — xoá set-based theo lô. `ExecuteDeleteAsync` không có LIMIT nên chọn id của lô bằng
/// subquery (`DELETE … WHERE id IN (SELECT id … ORDER BY id LIMIT 5000)`), lặp tới khi một lô xoá &lt; kích
/// thước lô. Không nạp dòng nào vào bộ nhớ.
/// </summary>
internal static class BatchedDelete
{
    public const int BatchSize = 5000;

    public static async Task<int> RunAsync<T>(DbSet<T> set, IQueryable<T> expired, CancellationToken cancellationToken)
        where T : BaseEntity
    {
        var total = 0;
        int deleted;
        do
        {
            var batchIds = expired.OrderBy(e => e.Id).Select(e => e.Id).Take(BatchSize);
            deleted = await set.Where(e => batchIds.Contains(e.Id)).ExecuteDeleteAsync(cancellationToken);
            total += deleted;
        }
        while (deleted >= BatchSize);

        return total;
    }
}
