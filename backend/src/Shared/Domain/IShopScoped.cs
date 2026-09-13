namespace Shared.Domain;

/// <summary>Marker cho phép `Shared.Persistence.TenantQueryFilterExtensions` tìm property `ShopId`
/// bằng reflection type-safe (`nameof`), thay vì string ma thuật.</summary>
public interface IShopScoped
{
    Guid ShopId { get; }
}
