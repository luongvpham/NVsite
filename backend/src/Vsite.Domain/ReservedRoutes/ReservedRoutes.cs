using System.Text.Json.Serialization;

namespace Vsite.Domain.ReservedRoutes;

/// <summary>
/// Mirror của config/reserved-routes.json (Quyết định #24) — nguồn sự thật duy nhất,
/// dùng chung FE + BE. Không viết tay danh sách thứ hai.
/// </summary>
public sealed record ReservedRoutes(
    [property: JsonPropertyName("reservedPaths")] IReadOnlyList<string> ReservedPaths,
    [property: JsonPropertyName("reservedSubdomains")] IReadOnlyList<string> ReservedSubdomains)
{
    /// <summary>
    /// Đường dẫn bị chiếm trên DOMAIN SHOP (vd. <c>/media/*</c>, <c>/api/*</c>) — slug trang của shop không
    /// được trùng (WEBSITE-001 plan §4, R9). Khác <see cref="ReservedPaths"/> (chặn slug SHOP ở
    /// vsite.vn/{slug}; dùng thẳng sẽ chặn oan <c>blog</c>/<c>about</c>/<c>help</c> của trang shop).
    /// Key <c>reservedShopSitePaths</c> trong file config là BẮT BUỘC — <c>ReservedRoutesProvider</c> ném lỗi khi thiếu;
    /// giá trị mặc định <c>[]</c> chỉ dùng khi dựng record trực tiếp (test).
    /// </summary>
    [JsonPropertyName("reservedShopSitePaths")]
    public IReadOnlyList<string> ReservedShopSitePaths { get; init; } = [];
}
