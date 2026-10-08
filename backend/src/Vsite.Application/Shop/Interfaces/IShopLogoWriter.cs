namespace Vsite.Application.Shop.Interfaces;

/// <summary>
/// T7, MEDIA-001 (#73, #82) — Public Contract của module `Shop` cho `Media`
/// (`Media.dependsOn = ["Shop"]`, `Docs/architecture/dependency-map.json`). Cho phép `Media` set
/// `Shop.LogoId` sau khi upload logo mà KHÔNG cần reference `Vsite.Domain.Shop.Entities.Shop` trực
/// tiếp trong handler của Media (interface sống ở `Application/Shop`, đúng mẫu
/// <see cref="IShopOwnershipService"/>).
///
/// Chỉ set property trên entity ĐANG được track qua CÙNG <c>IAppDbContext</c> scoped instance —
/// KHÔNG tự gọi <c>SaveChangesAsync</c>. Người gọi (handler của Media, dùng chung scope/DbContext)
/// lưu trong CÙNG một unit of work như các <c>MediaAsset</c> vừa <c>Add</c> — một
/// <c>SaveChangesAsync</c> duy nhất cho cả insert Library/derivatives lẫn update `Shop.LogoId`.
/// </summary>
public interface IShopLogoWriter
{
    /// <summary>Không tìm thấy shop → <c>NotFoundException</c>.</summary>
    Task SetLogoAsync(Guid shopId, Guid libraryAssetId, CancellationToken ct);
}
