namespace Vsite.Domain.Website.Enums;

/// <summary>
/// `05` §4 — tập đóng các trang hệ thống, shop không tạo/xoá được. Route của từng loại nằm ở
/// <c>config/system-pages.json</c> (nguồn duy nhất, #17) — KHÔNG viết lại ở đây. Khoá JSON = tên enum.
/// </summary>
public enum SystemType
{
    Contact,

    /// <summary>Không có route công khai; <c>Page.Slug = NULL</c> (lệch `05` §4, xem plan §4).</summary>
    NotFound,

    ProductListing,
    ProductDetail,
    ProductSearch,
    Booking,
}
