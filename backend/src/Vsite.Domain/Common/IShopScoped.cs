namespace Vsite.Domain.Common;

/// <summary>Marker cho phép `Vsite.Infrastructure.Persistence.TenantQueryFilterExtensions` tìm property `ShopId`
/// bằng reflection type-safe (`nameof`), thay vì string ma thuật.</summary>
public interface IShopScoped
{
    Guid ShopId { get; }
}
