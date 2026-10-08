using Vsite.Domain.Common;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Shop.Enums;

namespace Vsite.Domain.Shop.Entities;

/// <summary>
/// `04-listing-and-review-design.md` §2.1 — entity đầy đủ (chuyển giao từ Identity ở SHOP-001, xem
/// `backend/docs/modules/identity.md` mục "Nợ kỹ thuật"). Field giống hệt bản trích tối thiểu 03
/// §3.4 — 04 §2.1 không thêm field hồ sơ (địa chỉ, toạ độ, giờ mở cửa...), chỉ chốt ràng buộc
/// `Kind = ExternalOnly ⇒ ExternalUrl NOT NULL` (CHECK constraint, xem `ShopConfiguration`).
///
/// KHÔNG kế thừa `TenantEntity`/`TenantAuditableEntity` — bản thân `Shop` LÀ tenant, không PHỤ
/// THUỘC một tenant khác. `TenantEntity` dành cho các entity THUỘC VỀ một shop (vd. `UserShop`,
/// sau này `Product`, `Service`...).
///
/// Setter đều `private` (REFACTOR-BE-001): mọi thay đổi đi qua <see cref="Update"/>/
/// <see cref="SetLogo"/> để invariant `ExternalOnly ⇒ ExternalUrl` nằm ở entity, không chỉ ở
/// validator + CHECK constraint.
/// </summary>
public sealed class Shop : BaseAuditableEntity
{
    /// <summary>Cho EF Core materialize — không dùng trong code nghiệp vụ.</summary>
    private Shop()
    {
        Name = null!;
        Slug = null!;
    }

    public Shop(string name, string slug, ShopKind kind, string? externalUrl = null, ShopStatus status = ShopStatus.Draft)
        : this(Guid.NewGuid(), name, slug, kind, externalUrl, status)
    {
    }

    /// <summary>Id cố định — cho test/seed (xem quy ước `BaseEntity`).</summary>
    public Shop(Guid id, string name, string slug, ShopKind kind, string? externalUrl = null, ShopStatus status = ShopStatus.Draft)
        : base(id)
    {
        EnsureExternalUrl(kind, externalUrl);
        Name = name;
        Slug = slug;
        Kind = kind;
        ExternalUrl = externalUrl;
        Status = status;
    }

    public string Name { get; private set; }

    /// <summary>Mutable qua <see cref="Update"/> — PATCH /shops/{shopId} cho phép đổi slug. Đổi slug PHẢI gọi
    /// <see cref="Vsite.Application.Shop.Interfaces.IShopLookupService.InvalidateAsync"/> cho CẢ
    /// slug cũ lẫn mới ngay sau khi SaveChangesAsync thành công (xem `UpdateShopHandler`).</summary>
    public string Slug { get; private set; }

    public ShopKind Kind { get; private set; }

    /// <summary>NOT NULL khi <see cref="Kind"/> = ExternalOnly (04 §2.1, CHECK constraint ở DB).</summary>
    public string? ExternalUrl { get; private set; }

    public ShopStatus Status { get; private set; }

    /// <summary>`MediaAsset` bản Library dùng làm logo (Quyết định #73, #76). Guid? thuần —
    /// KHÔNG navigation, KHÔNG reference type của module `Media` (Shop.dependsOn không gồm Media,
    /// xem `Docs/architecture/dependency-map.json`). FK ghép `(LogoId, Id) → MediaAsset (Id, ShopId)`
    /// khai từ phía `MediaAssetConfiguration` (namespace `Persistence` dùng chung, không vi phạm
    /// ranh giới module — cùng khuôn `UserShop`/`ShopConfiguration.cs:28-35`).</summary>
    public Guid? LogoId { get; private set; }

    public void Update(string name, string slug, ShopKind kind, string? externalUrl, ShopStatus status)
    {
        EnsureExternalUrl(kind, externalUrl);
        Name = name;
        Slug = slug;
        Kind = kind;
        ExternalUrl = externalUrl;
        Status = status;
    }

    /// <summary>Chỉ gán id — FK ghép ở DB bảo đảm asset thuộc đúng shop này.</summary>
    public void SetLogo(Guid? libraryAssetId) => LogoId = libraryAssetId;

    private static void EnsureExternalUrl(ShopKind kind, string? externalUrl)
    {
        if (kind == ShopKind.ExternalOnly && string.IsNullOrWhiteSpace(externalUrl))
        {
            throw new DomainException("SHOP_EXTERNAL_URL_REQUIRED", "ExternalUrl bắt buộc khi Kind = ExternalOnly.");
        }
    }
}
