using Vsite.Application.Website.Interfaces;
using Vsite.Domain.Exceptions;
using Vsite.Domain.ReservedRoutes;
using Vsite.Domain.Website.Entities;

namespace Vsite.Application.Website.Validation;

/// <summary>
/// Luật Slug của trang Composable (plan Flow 3, R9, #98). Thứ tự: (1) slug bị chiếm chỗ → 422 <c>PAGE_SLUG_RESERVED</c>;
/// (2) sai khuôn → 422 <c>PAGE_SLUG_INVALID</c>. "Chiếm chỗ" gồm mã trong <c>locales.json</c> (kể cả locale chưa bật, #98),
/// MỌI route trong <c>system-pages.json</c> (kể cả loại chưa seed — đoạn đầu của đường dẫn) và <c>reservedShopSitePaths</c>
/// của <c>reserved-routes</c> (<c>media</c>, <c>api</c>…, không dùng <c>reservedPaths</c> vì chặn oan <c>blog</c>/<c>about</c>).
/// So khớp KHÔNG phân biệt hoa/thường; (1) chạy trước (2) nên <c>EN</c> báo "reserved" chứ không "sai khuôn".
/// Trùng slug TRONG website (409 <c>PAGE_SLUG_TAKEN</c>) là việc của handler vì cần DB.
/// </summary>
public sealed class SlugRules(ILocaleCatalog locales, ISystemPageCatalog systemPages, IReservedRoutesProvider reserved)
{
    public const string Reserved = "PAGE_SLUG_RESERVED";
    public const string Invalid = "PAGE_SLUG_INVALID";

    /// <exception cref="UnprocessableException"><c>PAGE_SLUG_RESERVED</c> | <c>PAGE_SLUG_INVALID</c>.</exception>
    public void EnsureAllowed(string? slug)
    {
        if (slug is not null && IsReserved(slug))
        {
            throw new UnprocessableException(Reserved, "Slug này được hệ thống dành riêng, hãy chọn slug khác.");
        }

        if (!Page.IsWellFormedSlug(slug))
        {
            throw new UnprocessableException(
                Invalid,
                $"Slug phải khớp ^[a-z0-9]+(-[a-z0-9]+)*$ và tối đa {Page.MaxSlugLength} ký tự (slug rỗng chỉ dành cho trang chủ).");
        }
    }

    private bool IsReserved(string slug) =>
        locales.Contains(slug)
        || systemPages.AllRoutes.Contains(slug)
        || reserved.Routes.ReservedShopSitePaths.Contains(slug, StringComparer.OrdinalIgnoreCase);
}
