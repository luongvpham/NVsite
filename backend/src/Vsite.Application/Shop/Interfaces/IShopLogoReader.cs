namespace Vsite.Application.Shop.Interfaces;

/// <summary>
/// MEDIA-001 D4 (#88) — port do module `Shop` khai báo để đọc logo "sẵn dùng" cho <c>ShopDto</c> /
/// <c>ShopSummaryDto</c>. `Shop` không reference `Media` (#1); adapter nằm ở `Vsite.Infrastructure.Media`
/// (Media dependsOn Shop — chiều ngược với <see cref="IShopLogoWriter"/>). Adapter trả URL đã có tiền tố
/// (<c>/media/{storageKey}</c>, qua <c>ImagePaths.MediaUrl</c>) nên Shop không biết gì về Media.
/// </summary>
public interface IShopLogoReader
{
    /// <summary>
    /// URL logo hiển thị (phái sinh `320x96,inside`) của MỘT shop, hoặc <c>null</c> khi
    /// <paramref name="logoId"/> null hoặc không tìm thấy phái sinh. Không ném exception.
    /// </summary>
    Task<string?> GetLogoUrlAsync(Guid shopId, Guid? logoId, CancellationToken ct);

    /// <summary>
    /// Tra theo lô: đúng MỘT câu SQL cho mọi shop (không N+1). Cặp (ShopId, Shop.LogoId) được ghép ngay
    /// trong câu query. Khoá kết quả = ShopId; shop không có logo/phái sinh KHÔNG có mặt trong dictionary.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> GetLogoUrlsAsync(IReadOnlyCollection<Guid> shopIds, CancellationToken ct);
}
