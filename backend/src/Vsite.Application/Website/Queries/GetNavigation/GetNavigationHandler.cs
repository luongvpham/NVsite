using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Website.Dtos;
using Vsite.Application.Website.Navigation;
using Vsite.Application.Website.Versioning;
using Vsite.Domain.Abstractions;

namespace Vsite.Application.Website.Queries.GetNavigation;

/// <summary>
/// Flow 1 + 3 (WEBSITE-007, #113): đọc dòng <c>navigation_config</c> (nếu có) + trang chưa xoá của website rồi
/// <see cref="NavigationItems.Merge"/>. Không ghi gì. Chưa từng lưu ⇒ <c>version: null</c>, <c>isDirty: true</c> (G6).
/// </summary>
public sealed class GetNavigationHandler(IAppDbContext db, ITenantContext tenant, DocumentVersioning versioning)
    : IRequestHandler<GetNavigationQuery, NavigationDto>
{
    public async Task<NavigationDto> Handle(GetNavigationQuery request, CancellationToken cancellationToken)
    {
        var shopId = tenant.RequireShopId();
        var websiteId = await NavigationQueries.RequireWebsiteIdAsync(db, shopId, cancellationToken);

        // PK = website_id; ShopId thêm trong câu query (cộng Global Filter).
        var row = await db.NavigationConfigs
            .AsNoTracking()
            .Where(n => n.Id == websiteId && n.ShopId == shopId)
            .Select(n => new { n.Items, n.Version, n.IsDirty })
            .FirstOrDefaultAsync(cancellationToken);

        var pages = await NavigationQueries.LoadPagesAsync(db, shopId, websiteId, cancellationToken);
        var stored = row is null ? null : NavigationItems.Parse(row.Items, versioning);
        var merged = NavigationItems.Merge(stored, pages);

        return new NavigationDto(row?.Version, row?.IsDirty ?? true, NavigationQueries.ToDtos(merged));
    }
}
