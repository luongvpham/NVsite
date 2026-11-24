using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Common.Jobs;

namespace Vsite.Infrastructure.Jobs;

/// <summary>
/// #116 — job con do <see cref="ShopFanOut"/> enqueue. Kiểm shop còn tồn tại (chưa xoá mềm) — nếu đã xoá sau khi
/// cha enqueue thì log + return; ngược lại mở scope tenant của shop, resolve <typeparamref name="TJob"/> TRONG
/// scope đó rồi chạy. `shopId` chỉ dùng để dựng scope, không đi vào logic job.
/// </summary>
public sealed class ShopJobRunner(IAppDbContext db, ITenantScopeFactory scopes, ILogger<ShopJobRunner> logger)
{
    public async Task RunAsync<TJob>(Guid shopId, CancellationToken cancellationToken)
        where TJob : class, IShopJob
    {
        // `Shop` không phải tenant entity, bộ lọc xoá mềm tự loại shop đã xoá.
        var exists = await db.Shops.AsNoTracking().AnyAsync(s => s.Id == shopId, cancellationToken);
        if (!exists)
        {
            logger.LogInformation("Bỏ qua job {Job}: shop {ShopId} không còn tồn tại (xoá mềm).", typeof(TJob).Name, shopId);
            return;
        }

        await using var scope = scopes.BeginShopScope(shopId);
        var job = scope.ServiceProvider.GetRequiredService<TJob>();
        await job.ExecuteAsync(cancellationToken);
    }
}
