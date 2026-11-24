using Microsoft.Extensions.DependencyInjection;

namespace Vsite.Application.Common.Interfaces;

/// <summary>
/// #116 — tạo scope DI mang tenant `Shop` cho code chạy NGOÀI request HTTP (job nền). Trong scope, mọi
/// `ITenantContext`/`AppDbContext` thấy đúng `ShopId` nên Global Query Filter hoạt động như request thường.
/// Mỗi scope đặt tenant đúng một lần; hai scope song song độc lập nhau. Caller dispose scope khi xong.
/// </summary>
public interface ITenantScopeFactory
{
    AsyncServiceScope BeginShopScope(Guid shopId);
}
