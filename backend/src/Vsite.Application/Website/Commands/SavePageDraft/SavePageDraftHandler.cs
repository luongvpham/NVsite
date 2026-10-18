using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Common.Persistence;
using Vsite.Application.Media.Interfaces;
using Vsite.Application.Website.Dtos;
using Vsite.Application.Website.Exceptions;
using Vsite.Application.Website.Interfaces;
using Vsite.Application.Website.Validation;
using Vsite.Application.Website.Versioning;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Website.Enums;

namespace Vsite.Application.Website.Commands.SavePageDraft;

/// <summary>
/// Flow 2 (plan §3), T7 + T9. Thứ tự: nạp draft + page theo <c>PageId</c> trong tenant (lạ/shop khác → 404) → so <c>Version</c>
/// (lệch → 409 <c>DRAFT_CONFLICT</c>) → <see cref="PageTreeValidator"/> theo <see cref="PageKind"/> (422 <c>TREE_INVALID</c> /
/// <c>SCHEMA_VERSION_UNSUPPORTED</c>) → trang System: so cấu trúc với bản đang lưu (<see cref="SystemPageGuard"/>) → ảnh qua
/// <see cref="IMediaReferenceValidator"/> (parse MỌI id trước; hỏng → <c>MEDIA_INVALID_IMAGE_REFERENCE</c>) → ghi bản ĐÃ LÀM SẠCH,
/// <c>IsDirty = true</c> (G11). Race: <c>xmin</c> lệch lúc UPDATE → <see cref="DbUpdateConcurrencyException"/> → cũng <c>DRAFT_CONFLICT</c>.
/// </summary>
public sealed class SavePageDraftHandler(
    IAppDbContext db,
    ITenantContext tenant,
    PageTreeValidator validator,
    ITreeRulesCatalog rules,
    DocumentVersioning versioning,
    IMediaReferenceValidator media)
    : IRequestHandler<SavePageDraftCommand, SavePageDraftResultDto>
{
    public async Task<SavePageDraftResultDto> Handle(SavePageDraftCommand request, CancellationToken cancellationToken)
    {
        var shopId = tenant.RequireShopId();

        // Ownership trong câu query: PageId + ShopId (+ Global Filter). Trang đã xoá mềm cũng không còn thấy.
        var page = await db.Pages
            .AsNoTracking()
            .Where(p => p.Id == request.PageId && p.ShopId == shopId)
            .Select(p => new { p.Kind, p.SystemType })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Page", request.PageId);

        var draft = await db.PageDrafts.FirstOrDefaultAsync(d => d.Id == request.PageId && d.ShopId == shopId, cancellationToken)
            ?? throw new NotFoundException("Page", request.PageId);

        if (draft.Version != request.Version)
        {
            throw Conflict(draft.Version, draft.UpdatedAt ?? draft.CreatedAt, draft.UpdatedByUserId ?? draft.CreatedByUserId);
        }

        var result = validator.Validate(request.TreeJson, page.Kind);
        if (!result.IsValid)
        {
            throw new DocumentInvalidException(result.ProblemCode!, result.Errors, result.ErrorsTruncated);
        }

        var clean = result.CleanTree!;

        if (page.Kind == PageKind.System)
        {
            var stored = StoredDocuments.ReadCurrent(DocumentKind.Tree, draft.Tree, versioning);
            var structural = SystemPageGuard.Compare(stored, clean, rules);
            if (structural.Count > 0)
            {
                throw new DocumentInvalidException(TreeErrorCodes.Invalid, structural, truncated: false);
            }
        }

        await media.EnsureValidAsync(ParseImageIds(result.ImageIds), [], cancellationToken);

        draft.Replace(clean);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Hai PUT song song cùng version: chỉ một UPDATE ... WHERE xmin = @x trúng dòng. Đọc lại trạng thái thật để FE hoà giải.
            var current = await db.PageDrafts
                .AsNoTracking()
                .Where(d => d.Id == request.PageId && d.ShopId == shopId)
                .Select(d => new { d.Version, d.UpdatedAt, d.CreatedAt, d.UpdatedByUserId, d.CreatedByUserId })
                .FirstOrDefaultAsync(CancellationToken.None)
                ?? throw new NotFoundException("Page", request.PageId);
            throw Conflict(current.Version, current.UpdatedAt ?? current.CreatedAt, current.UpdatedByUserId ?? current.CreatedByUserId);
        }

        // EF đọc lại xmin mới qua RETURNING ngay sau UPDATE (cột store-generated) — không cần query thêm.
        return new SavePageDraftResultDto(draft.Version, draft.IsDirty, result.Sanitized);
    }

    /// <summary>Id ảnh trong cây là chuỗi; validator Media nhận Guid. Parse MỌI phần tử trước (định dạng "D" chặt) — hỏng một cái là cả cây bị từ chối.</summary>
    private static List<Guid> ParseImageIds(IReadOnlyList<string> imageIds)
    {
        var guids = new List<Guid>(imageIds.Count);
        foreach (var id in imageIds)
        {
            if (!Guid.TryParseExact(id, "D", out var guid))
            {
                throw new UnprocessableException(
                    "MEDIA_INVALID_IMAGE_REFERENCE", "Một hoặc nhiều ảnh tham chiếu không hợp lệ (id không đúng định dạng).");
            }

            guids.Add(guid);
        }

        return guids;
    }

    private static DraftConflictException Conflict(uint version, DateTimeOffset updatedAt, Guid? updatedBy) =>
        new(new DraftConflictParams(version, updatedAt, updatedBy));
}
