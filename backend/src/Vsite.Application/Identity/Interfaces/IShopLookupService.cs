namespace Vsite.Application.Identity.Interfaces;

/// <summary>
/// Tra `ShopId` theo slug — dùng bởi `Vsite.Api.Tenancy.TenantResolutionMiddleware` để resolve tenant từ
/// Host header (Path/Subdomain, Quyết định #7 thu hẹp). Interface sống ở `Shared` vì middleware ở
/// root Api không được reference thẳng Infrastructure của module (chỉ {Module}.Api) — implementation
/// thật (query bảng Shop) nằm ở Vsite.Infrastructure, đăng ký qua DI như các service khác.
/// Khi module Shop đầy đủ ra đời, implementation này chuyển sang Shop.Infrastructure mà không đổi
/// interface hay code gọi nó.
///
/// ⚠️ Được cache (Redis, Quyết định #7 "Cache Redis: host+path → shop_id, invalidate khi đổi
/// domain") vì gọi ở MỌI request qua middleware — bất kỳ command nào SAU NÀY sửa `Shop.Slug` hoặc
/// xoá/disable một Shop đều PHẢI gọi <see cref="InvalidateAsync"/>, nếu không middleware sẽ tiếp
/// tục resolve theo giá trị cũ tới khi cache tự hết hạn.
/// </summary>
public interface IShopLookupService
{
    Task<Guid?> FindShopIdBySlugAsync(string slug, CancellationToken cancellationToken);

    /// <summary>Xoá cache cho MỘT slug cụ thể — gọi ngay sau khi command đổi `Shop.Slug` (cả slug
    /// cũ lẫn slug mới nếu đổi) hoặc xoá/disable Shop commit thành công.</summary>
    Task InvalidateAsync(string slug, CancellationToken cancellationToken);
}
