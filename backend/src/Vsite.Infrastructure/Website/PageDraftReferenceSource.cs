using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Media.Dtos;
using Vsite.Application.Media.Interfaces;
using Vsite.Application.Website.Validation;
using Vsite.Domain.Abstractions;

namespace Vsite.Infrastructure.Website;

/// <summary>
/// T11, WEBSITE-001 (MEDIA-001 #2, R13/R14) — nguồn tham chiếu "ảnh Library đang được dùng trong draft của trang nào". Tree chỉ chứa id
/// CLONE (không bao giờ id Library), nên: lấy các clone của shop có <c>SourceAssetId == assetId</c> → giao với <c>imageId</c> trong tree
/// của draft thuộc Page CÒN SỐNG của shop. Lấy <c>imageId</c> bằng walker của <see cref="PageTreeValidator"/> (không SQL jsonb thủ công),
/// so theo <see cref="Guid"/> (không so chuỗi — GUID chữ hoa/thường vẫn khớp). Mỗi trang một mục dù ảnh dùng nhiều chỗ. Draft của page đã
/// xoá mềm không được báo (join Page qua Global Filter). Shop khác không bao giờ thấy được: <c>ShopId</c> đến từ <see cref="ITenantContext"/>
/// và có mặt TƯỜNG MINH trong từng predicate (#21.2).
/// </summary>
public sealed class PageDraftReferenceSource(IAppDbContext db, ITenantContext tenant, PageTreeValidator validator) : IMediaReferenceSource
{
    public async Task<IReadOnlyList<MediaReferenceDto>> FindAsync(Guid assetId, CancellationToken ct)
    {
        if (tenant.ShopId is not { } shopId)
        {
            return [];
        }

        var cloneIds = (await db.MediaAssets
                .AsNoTracking()
                .Where(a => a.ShopId == shopId && a.SourceAssetId == assetId)
                .Select(a => a.Id)
                .ToListAsync(ct))
            .ToHashSet();
        if (cloneIds.Count == 0)
        {
            return [];
        }

        var drafts = await (
                from d in db.PageDrafts.AsNoTracking()
                join p in db.Pages.AsNoTracking() on d.Id equals p.Id
                where d.ShopId == shopId && p.ShopId == shopId
                orderby p.SortOrder, p.Title
                select new { PageId = p.Id, p.Title, p.Kind, d.Tree })
            .ToListAsync(ct);

        var references = new List<MediaReferenceDto>();
        foreach (var draft in drafts)
        {
            var usesAsset = validator.Validate(draft.Tree, draft.Kind).ImageIds
                .Any(raw => Guid.TryParse(raw, out var id) && cloneIds.Contains(id));
            if (usesAsset)
            {
                references.Add(new MediaReferenceDto(MediaReferenceKind.PageDraft, draft.PageId, draft.Title));
            }
        }

        return references;
    }
}
