using Vsite.Domain.Common;
using Vsite.Domain.Shop.Enums;

namespace Vsite.Domain.Shop.Entities;

/// <summary>
/// `04-listing-and-review-design.md` §2.1 — entity đầy đủ (chuyển giao từ Identity ở SHOP-001, xem
/// `backend/docs/modules/identity.md` mục "Nợ kỹ thuật"). Field giống hệt bản trích tối thiểu 03
/// §3.4 — 04 §2.1 không thêm field hồ sơ (địa chỉ, toạ độ, giờ mở cửa...), chỉ chốt ràng buộc
/// `Kind = ExternalOnly ⇒ ExternalUrl NOT NULL` (CHECK constraint, xem `ShopConfiguration`).
///
/// KHÔNG kế thừa `ShopEntity`/`ShopAuditableEntity` — bản thân `Shop` LÀ tenant, không PHỤ THUỘC
/// một tenant khác. `ShopEntity` dành cho các entity THUỘC VỀ một shop (vd. `UserShop`, sau này
/// `Product`, `Service`...).
/// </summary>
public sealed class Shop : BaseAuditableEntity
{
    public Shop()
    {
    }

    public Shop(Guid id) : base(id)
    {
    }

    public required string Name { get; set; }

    /// <summary>Mutable — PATCH /shops/{shopId} cho phép đổi slug. Đổi slug PHẢI gọi
    /// <see cref="Vsite.Application.Shop.Interfaces.IShopLookupService.InvalidateAsync"/> cho CẢ
    /// slug cũ lẫn mới ngay sau khi SaveChangesAsync thành công (xem `UpdateShopHandler`).</summary>
    public required string Slug { get; set; }

    public required ShopKind Kind { get; set; }

    /// <summary>NOT NULL khi <see cref="Kind"/> = ExternalOnly (04 §2.1, CHECK constraint ở DB).</summary>
    public string? ExternalUrl { get; set; }

    public ShopStatus Status { get; set; } = ShopStatus.Draft;
}
