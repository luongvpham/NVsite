namespace Vsite.Application.Website.Interfaces;

/// <summary>
/// Con trỏ "bản đang chạy" của Shop Site ở Redis (#109, plan §4): key <c>site:pointer:{shopId}</c> →
/// <c>{ websiteStatus, publicationId? }</c>. Publish/rollback gọi <see cref="InvalidateAsync"/> SAU khi đã lưu, để request
/// công khai kế tiếp thấy bản mới ngay (R5). Bản thật ở T6; T4 đăng ký bản no-op.
/// </summary>
public interface ISitePointerCache
{
    Task InvalidateAsync(Guid shopId, CancellationToken cancellationToken);
}
