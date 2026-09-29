namespace Vsite.Application.Shop.Interfaces;

/// <summary>
/// MEDIA-001 D4 (#88) — port do module `Shop` khai báo để đọc logo "sẵn dùng" cho <c>ShopDto</c>.
/// `Shop` không reference `Media` (#1); adapter nằm ở `Vsite.Infrastructure.Media` (Media dependsOn
/// Shop — chiều ngược với <see cref="IShopLogoWriter"/>).
/// </summary>
public interface IShopLogoReader
{
    /// <summary>
    /// Storage key TƯƠNG ĐỐI của phái sinh logo dùng để hiển thị (`320x96,inside`), hoặc <c>null</c>
    /// khi <paramref name="logoId"/> null hoặc không tìm thấy phái sinh. Không ném exception; không
    /// bao giờ trả URL có tiền tố `/media/`.
    /// </summary>
    Task<string?> GetLogoStorageKeyAsync(Guid shopId, Guid? logoId, CancellationToken ct);
}
