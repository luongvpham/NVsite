using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Website.Dtos;
using Vsite.Domain.Exceptions;

namespace Vsite.Application.Website.Navigation;

/// <summary>Đọc dữ liệu dùng chung của <c>GetNavigation</c>/<c>SaveNavigation</c> (WEBSITE-007). Mọi query có <c>ShopId</c> (từ
/// <c>ITenantContext</c>, #99) TRONG câu query, cộng Global Query Filter (tenant + soft delete) — #21.2, #21.3.</summary>
internal static class NavigationQueries
{
    private static readonly NavItem Empty = new(string.Empty, null, null, false, []);

    /// <summary>Id website của shop hiện tại; chưa có → 404 <c>WEBSITE_NOT_FOUND</c> (như <c>getTheme</c>/<c>getWebsite</c>).</summary>
    public static async Task<Guid> RequireWebsiteIdAsync(IAppDbContext db, Guid shopId, CancellationToken ct) =>
        await db.Websites
            .AsNoTracking()
            .Where(w => w.ShopId == shopId)
            .Select(w => (Guid?)w.Id)
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Website", shopId);

    /// <summary>
    /// Trang CHƯA XOÁ của đúng website này (R1: lọc <c>website_id</c> + <c>shop_id</c> trong câu query — id trang của shop khác
    /// hay đã xoá đơn giản là không có trong tập ⇒ <c>NAV_PAGE_NOT_FOUND</c>, không lộ tồn tại).
    /// </summary>
    public static async Task<IReadOnlyList<NavPage>> LoadPagesAsync(IAppDbContext db, Guid shopId, Guid websiteId, CancellationToken ct) =>
        await db.Pages
            .AsNoTracking()
            .Where(p => p.ShopId == shopId && p.WebsiteId == websiteId)
            .Select(p => new NavPage(p.Id, p.Title, p.SystemType, p.IsEnabled, p.SortOrder))
            .ToListAsync(ct);

    /// <summary>Model → DTO ba cấp (mỗi cấp 1/2 luôn có mảng <c>children</c>). Model đã cắt ở 3 cấp (Parse/Merge của T2).</summary>
    public static IReadOnlyList<NavigationItemDto> ToDtos(IReadOnlyList<NavItem> items) =>
        items.Select(i => new NavigationItemDto(
                i.Key,
                i.Label,
                i.Url,
                i.Hidden,
                i.Children.Select(c => new NavigationChildItemDto(
                        c.Key,
                        c.Label,
                        c.Url,
                        c.Hidden,
                        c.Children.Select(g => new NavigationGrandchildItemDto(g.Key, g.Label, g.Url, g.Hidden)).ToList()))
                    .ToList()))
            .ToList();

    /// <summary>
    /// Body → model thuần. Binding đã bắt buộc <c>key</c>/<c>hidden</c> (400) và chặn cấp 4 (cấp 3 từ chối field lạ ⇒ 400); ở đây vẫn
    /// phòng thủ cho caller không qua HTTP: phần tử/khoá <c>null</c> ⇒ <c>key ""</c> (Validate báo <c>NAV_PAGE_NOT_FOUND</c> tại
    /// <c>/…/key</c>, không 500), <c>children</c> thiếu ⇒ rỗng.
    /// </summary>
    public static IReadOnlyList<NavItem> FromDtos(IReadOnlyList<NavigationItemDto?> items) =>
        items.Select(d => d is null
                ? Empty
                : new NavItem(d.Key ?? string.Empty, d.Label, d.Url, d.Hidden, d.Children?.Select(FromChild).ToList() ?? []))
            .ToList();

    private static NavItem FromChild(NavigationChildItemDto? c) =>
        c is null
            ? Empty
            : new NavItem(c.Key ?? string.Empty, c.Label, c.Url, c.Hidden, c.Children?.Select(FromGrandchild).ToList() ?? []);

    private static NavItem FromGrandchild(NavigationGrandchildItemDto? g) =>
        g is null ? Empty : new NavItem(g.Key ?? string.Empty, g.Label, g.Url, g.Hidden, []);
}
