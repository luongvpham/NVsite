using Identity.Domain.Enums;
using Shared.Domain;

namespace Identity.Domain.Entities;

/// <summary>
/// ⚠️ BẢN TRÍCH TỐI THIỂU — nguồn sự thật đầy đủ của entity `Shop` là
/// `DesignIdeal/04-listing-and-review-design.md` §2.1 (Quyết định #39.5), thiết kế ở module `Shop`
/// riêng (chưa làm). Ở đây chỉ đủ field để `UserShop` có FK hợp lệ, đúng 03 §3.4. KHÔNG thêm field
/// hồ sơ (địa chỉ, toạ độ, giờ mở cửa...) vào đây — thuộc phạm vi module Shop đầy đủ.
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
    public required string Slug { get; init; }
    public required ShopKind Kind { get; set; }

    /// <summary>NOT NULL khi <see cref="Kind"/> = ExternalOnly (03 §3.4).</summary>
    public string? ExternalUrl { get; set; }

    public ShopStatus Status { get; set; } = ShopStatus.Draft;
}
