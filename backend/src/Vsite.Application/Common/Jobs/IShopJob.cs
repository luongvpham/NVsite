namespace Vsite.Application.Common.Jobs;

/// <summary>
/// #116 — job con chạy cho MỘT shop. Đọc shop qua `ITenantContext` (đã đặt bởi `ITenantScopeFactory`),
/// KHÔNG nhận `shopId` làm tham số logic. Resolve từ DI trong scope của shop đó.
/// </summary>
public interface IShopJob
{
    Task ExecuteAsync(CancellationToken cancellationToken);
}
