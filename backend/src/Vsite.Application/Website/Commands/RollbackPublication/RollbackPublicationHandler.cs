using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Common.Persistence;
using Vsite.Application.Website.Dtos;
using Vsite.Application.Website.Interfaces;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Exceptions;

namespace Vsite.Application.Website.Commands.RollbackPublication;

/// <summary>
/// Flow 2 (WEBSITE-004 T5). Bản được tìm bằng <c>Id + ShopId + WebsiteId</c> trong MỘT câu query (#21.3, R4) → không thấy: 404
/// <c>PUBLICATION_NOT_FOUND</c>. Đã là bản đang chạy: trả luôn, không ghi gì. Còn lại: <c>Website.PointTo</c> (không đổi
/// <c>Status</c>) + mọi draft và menu (nếu có dòng, WEBSITE-007) <c>MarkDirty</c> (G7, KHÔNG đụng nội dung) trong MỘT <c>SaveChanges</c>; sau commit xoá
/// con trỏ cache (#109, R5) — lỗi xoá chỉ log (cùng quy ước publish; con trỏ cũ hết hạn theo TTL).
/// </summary>
public sealed class RollbackPublicationHandler(
    IAppDbContext db,
    ITenantContext tenant,
    ISitePointerCache pointerCache,
    ILogger<RollbackPublicationHandler> logger)
    : IRequestHandler<RollbackPublicationCommand, PublicationDto>
{
    public async Task<PublicationDto> Handle(RollbackPublicationCommand request, CancellationToken cancellationToken)
    {
        var shopId = tenant.RequireShopId();

        var website = await db.Websites.FirstOrDefaultAsync(w => w.ShopId == shopId, cancellationToken)
            ?? throw new NotFoundException("Website", shopId);

        // Nạp cả entity (kể cả Snapshot, một bản) vì Website.PointTo nhận entity; bản bất biến nên không bị ghi lại.
        var publication = await db.SitePublications
            .FirstOrDefaultAsync(p => p.Id == request.PublicationId && p.ShopId == shopId && p.WebsiteId == website.Id, cancellationToken)
            ?? throw new NotFoundException("Publication", request.PublicationId);

        if (website.CurrentPublicationId == publication.Id)
        {
            return Mappings.ToDto(publication, website.CurrentPublicationId);
        }

        website.PointTo(publication);

        // Chỉ draft đang sạch cần ghi (draft bẩn đã đúng); draft của trang đã xoá mềm bị Global Filter của Page loại qua join.
        var pageIds = db.Pages.Where(p => p.ShopId == shopId && p.WebsiteId == website.Id).Select(p => p.Id);
        var cleanDrafts = await db.PageDrafts
            .Where(d => d.ShopId == shopId && !d.IsDirty && pageIds.Contains(d.Id))
            .ToListAsync(cancellationToken);
        foreach (var draft in cleanDrafts)
        {
            draft.MarkDirty();
        }

        // Menu đang sửa giữ nguyên nhưng giờ khác bản đang chạy (WEBSITE-007 Flow 6). Chưa có dòng (website publish trước Bước 7) ⇒ không làm gì.
        var nav = await db.NavigationConfigs
            .FirstOrDefaultAsync(n => n.Id == website.Id && n.ShopId == shopId && !n.IsDirty, cancellationToken);
        nav?.MarkDirty();

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (UniqueViolation.IsConcurrencyConflict(ex))
        {
            throw new ConflictException("ROLLBACK_CONFLICT", "Website vừa được chỉnh sửa hoặc xuất bản ở nơi khác. Hãy tải lại rồi thử lại.");
        }
        catch (DbUpdateException ex) when (UniqueViolation.IsForeignKeyViolation(ex))
        {
            // Bản bị job prune xoá cứng giữa lúc nạp và ghi (WEBSITE-008, #118, R2).
            throw new NotFoundException("Publication", request.PublicationId);
        }

        try
        {
            await pointerCache.InvalidateAsync(shopId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not invalidate site pointer cache for shop {ShopId}", shopId);
        }

        return Mappings.ToDto(publication, website.CurrentPublicationId);
    }
}
