namespace Shared.Domain;

/// <summary>Kết hợp <see cref="ShopEntity"/> (tenant isolation) + audit trail/soft-delete. Dùng cho
/// phần lớn entity nghiệp vụ thuộc về một shop (product, service, booking...).</summary>
public abstract class ShopAuditableEntity : BaseAuditableEntity, IShopScoped
{
    public Guid ShopId { get; set; }

    protected ShopAuditableEntity()
    {
    }

    protected ShopAuditableEntity(Guid id) : base(id)
    {
    }
}
