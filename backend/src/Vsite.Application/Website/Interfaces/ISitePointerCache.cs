using Vsite.Domain.Website.Enums;

namespace Vsite.Application.Website.Interfaces;

/// <summary>
/// Con trỏ "bản đang chạy" của Website một shop (#109). <paramref name="WebsiteStatus"/> = null nghĩa là shop CHƯA có website
/// (kết quả âm tính cũng được cache). Chỉ giữ dữ liệu của Website — dòng <c>Shop</c> luôn đọc sống nên đình chỉ shop có hiệu lực ngay.
/// </summary>
public sealed record SitePointer(WebsiteStatus? WebsiteStatus, Guid? PublicationId);

/// <summary>
/// Cache con trỏ ở Redis (#109, plan §4): key <c>site:pointer:{shopId:N}</c>. <see cref="SetAsync"/> TTL 5 phút khi có website,
/// 1 phút khi chưa có. Publish/rollback gọi <see cref="InvalidateAsync"/> SAU khi đã lưu để request công khai kế tiếp thấy bản
/// mới ngay (R5); lỗi xoá chỉ log nên TTL là lưới an toàn — đừng kéo dài.
/// </summary>
public interface ISitePointerCache
{
    Task<SitePointer?> GetAsync(Guid shopId, CancellationToken cancellationToken);

    Task SetAsync(Guid shopId, SitePointer pointer, CancellationToken cancellationToken);

    Task InvalidateAsync(Guid shopId, CancellationToken cancellationToken);
}
