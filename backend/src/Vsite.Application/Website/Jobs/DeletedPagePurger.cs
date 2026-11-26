using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Website.Options;
using Vsite.Domain.Abstractions;

namespace Vsite.Application.Website.Jobs;

/// <summary>
/// WEBSITE-008 (#118) — purge draft của trang đã xoá mềm quá <see cref="BuilderRetentionOptions.DeletedPageGrace"/> trong shop hiện tại
/// (<see cref="ITenantContext"/>), đồng thời gỡ <c>Page.OgImageId</c> (FK là SET NULL nên không chặn xoá ảnh; gỡ cho gọn, trang đã xoá không còn đường quay lại). Không xoá dòng <c>Page</c>.
/// <c>IgnoreQueryFilters()</c> tắt cả filter tenant lẫn soft-delete nên mọi query mang <c>ShopId == shopId</c> tường minh.
/// <c>ExecuteDelete</c> vì <c>Remove</c> sẽ bị interceptor đổi thành xoá mềm.
/// </summary>
public sealed class DeletedPagePurger(IAppDbContext db, ITenantContext tenant, IOptions<BuilderRetentionOptions> options, TimeProvider time)
{
    /// <summary>Trả số draft đã xoá cứng.</summary>
    public async Task<int> PurgeAsync(CancellationToken cancellationToken)
    {
        var shopId = tenant.RequireShopId();
        var cutoff = time.GetUtcNow() - options.Value.DeletedPageGrace;

        var expiredPages = db.Pages.IgnoreQueryFilters()
            .Where(p => p.ShopId == shopId && p.IsDeleted && p.DeletedAt < cutoff);

        var deleted = await db.PageDrafts.IgnoreQueryFilters()
            .Where(d => d.ShopId == shopId && expiredPages.Any(p => p.Id == d.Id))
            .ExecuteDeleteAsync(cancellationToken);

        // Không đụng UpdatedAt: ExecuteUpdate bỏ qua interceptor audit và đây là dọn dẹp, không phải sửa của người dùng.
        await expiredPages
            .Where(p => p.OgImageId != null)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.OgImageId, (Guid?)null), cancellationToken);

        return deleted;
    }
}
