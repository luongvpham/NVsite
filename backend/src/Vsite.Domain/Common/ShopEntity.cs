namespace Vsite.Domain.Common;

/// <summary>
/// Base cho entity scoped theo shop (tenant). Global Query Filter tự động theo
/// <c>ShopId == ITenantContext.ShopId</c> áp dụng cho MỌI entity kế thừa từ đây, ở MỌI module —
/// xem <c>Vsite.Infrastructure.Persistence.TenantQueryFilterExtensions.ApplyShopTenantFilters</c>. Không cần tự
/// viết `HasQueryFilter` tay cho từng entity nữa — chỉ cần kế thừa đúng base class.
///
/// KHÔNG có navigation property `Shop` ở đây (khác bản tham chiếu ban đầu) — `Shared` không được
/// reference entity `Shop` của module Identity/Shop (Quyết định #1, packages không cross-module).
/// Entity cụ thể trong module muốn có navigation `Shop` thì tự khai thêm property đó ở lớp con
/// (xem `Vsite.Domain.Identity.UserShop.Shop`).
/// </summary>
public abstract class ShopEntity : BaseEntity, IShopScoped
{
    public Guid ShopId { get; set; }

    protected ShopEntity()
    {
    }

    protected ShopEntity(Guid id) : base(id)
    {
    }
}
