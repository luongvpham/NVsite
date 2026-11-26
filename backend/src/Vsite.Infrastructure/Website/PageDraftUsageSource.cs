using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Media.Interfaces;
using Vsite.Application.Website.Validation;
using Vsite.Domain.Abstractions;

namespace Vsite.Infrastructure.Website;

/// <summary>
/// WEBSITE-008 T6 (#117) — id ảnh trong tree draft của mọi trang CÒN SỐNG của shop (join Page qua Global Filter nên draft của trang
/// xoá mềm không tính). Cùng walker với <see cref="PageDraftReferenceSource"/> (<see cref="PageTreeImageIds"/>) nhưng strict: tree không
/// đọc được thì NÉM (R5). <c>ShopId</c> từ <see cref="ITenantContext"/>, tường minh trong predicate.
/// </summary>
public sealed class PageDraftUsageSource(IAppDbContext db, ITenantContext tenant, PageTreeValidator validator) : IMediaUsageSource
{
    public async Task<IReadOnlySet<Guid>> CollectUsedAssetIdsAsync(CancellationToken ct)
    {
        var shopId = tenant.ShopId ?? throw new InvalidOperationException("Không có tenant để thu ảnh đang dùng.");

        var drafts = await (
                from d in db.PageDrafts.AsNoTracking()
                join p in db.Pages.AsNoTracking() on d.Id equals p.Id
                where d.ShopId == shopId && p.ShopId == shopId
                select new { p.Kind, d.Tree })
            .ToListAsync(ct);

        var used = new HashSet<Guid>();
        foreach (var draft in drafts)
        {
            used.UnionWith(PageTreeImageIds.Read(validator, draft.Tree, draft.Kind, strict: true));
        }

        return used;
    }
}
