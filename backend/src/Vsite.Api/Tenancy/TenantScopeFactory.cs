using Vsite.Application.Common.Interfaces;
using Vsite.Domain.Abstractions;

namespace Vsite.Api.Tenancy;

/// <summary>
/// #116 — scope tenant cho code ngoài request (job nền). Singleton: mỗi lần gọi tạo scope MỚI từ root
/// và đặt `TenantContext` của scope đó đúng một lần, nên hai scope không lẫn `ShopId` và không ghi đè
/// được tenant của scope request nào.
/// </summary>
public sealed class TenantScopeFactory(IServiceScopeFactory scopeFactory) : ITenantScopeFactory
{
    public AsyncServiceScope BeginShopScope(Guid shopId)
    {
        if (shopId == Guid.Empty)
        {
            throw new ArgumentException("shopId không được rỗng.", nameof(shopId));
        }

        var scope = scopeFactory.CreateAsyncScope();
        try
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(TenantAudienceKind.Shop, shopId);
            return scope;
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }
}
