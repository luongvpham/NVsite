namespace Vsite.Domain.Common;

/// <summary>Kết hợp <see cref="TenantEntity"/> (tenant isolation) + audit trail/soft-delete. Dùng cho
/// phần lớn entity nghiệp vụ thuộc về một shop (product, service, booking...).</summary>
public abstract class TenantAuditableEntity : BaseAuditableEntity, IShopScoped
{
    public Guid ShopId { get; init; }

    protected TenantAuditableEntity()
    {
    }

    protected TenantAuditableEntity(Guid id) : base(id)
    {
    }
}
