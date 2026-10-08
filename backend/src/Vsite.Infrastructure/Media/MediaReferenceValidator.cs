using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Media.Interfaces;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Media.Enums;

namespace Vsite.Infrastructure.Media;

/// <summary>
/// T8, MEDIA-001 (#77, `08` §3.5) — implementation của <see cref="IMediaReferenceValidator"/>.
/// Public Contract cho Bước 5 (Website/PageDraft, chưa build): Website module sẽ gọi service này
/// khi lưu draft/page.
///
/// Một câu query duy nhất (08 §3.5) đếm SỐ ID KHÁC NHAU thoả điều kiện hợp lệ rồi so với số ID KHÁC
/// NHAU trong cả hai danh sách gộp lại — lệch số là có id sai, không cần biết id nào (không lộ chi
/// tiết trong error, đúng khuôn 422 `UnprocessableException`).
///
/// `ShopId` lấy từ <see cref="ITenantContext"/> — KHÔNG bao giờ là tham số (Quyết định #21.4).
/// `ShopId` cũng được đưa TƯỜNG MINH vào predicate (<c>a.ShopId == shopId</c>) dù
/// <see cref="IAppDbContext.MediaAssets"/> đã có Global Query Filter theo cùng `ITenantContext`
/// (`TenantQueryFilterExtensions`, lọc luôn cả `IsDeleted`) — mục đích là để bất biến tenant isolation
/// NHÌN THẤY ĐƯỢC ngay trong câu query này (backend/CLAUDE.md #21.2), không dựa hoàn toàn vào filter
/// ẩn ở tầng model.
/// </summary>
public sealed class MediaReferenceValidator(IAppDbContext db, ITenantContext tenantContext) : IMediaReferenceValidator
{
    public async Task EnsureValidAsync(
        IReadOnlyCollection<Guid> treeImageIds,
        IReadOnlyCollection<Guid> businessImageIds,
        CancellationToken ct)
    {
        if (treeImageIds.Count == 0 && businessImageIds.Count == 0)
        {
            return;
        }

        var shopId = tenantContext.ShopId
            ?? throw new UnprocessableException(
                "MEDIA_INVALID_IMAGE_REFERENCE", "Không xác định được shop hiện tại để kiểm tra ảnh tham chiếu.");

        var treeSet = treeImageIds.Distinct().ToArray();
        var businessSet = businessImageIds.Distinct().ToArray();

        // Số id khác nhau trong cả hai danh sách gộp lại (in-memory — hai mảng nhỏ, không đáng một
        // roundtrip DB riêng). Một id xuất hiện ở CẢ HAI danh sách chỉ tính 1 lần ở đây.
        var expectedDistinctCount = treeSet.Union(businessSet).Count();

        // MỘT câu query duy nhất cho cả hai danh sách (08 §3.5). Loại trừ tường minh trường hợp id
        // xuất hiện ở CẢ HAI danh sách (!businessSet.Contains / !treeSet.Contains) — một id như vậy
        // không bao giờ được coi là hợp lệ, bất kể Kind thật của nó là gì, vì nó không thể vừa là clone
        // (tree) vừa là bản Library (business) cùng lúc. Tree chỉ nhận Clone/Direct — KHÔNG nhận phái
        // sinh của ảnh nghiệp vụ (Derivative, vd. logo): phái sinh thuộc về entity nghiệp vụ, có thể
        // bị sinh lại/dọn theo nó (REFACTOR-DB-001).
        var matchedDistinctCount = await db.MediaAssets
            .Where(a => a.ShopId == shopId && !a.IsDeleted)
            .Where(a =>
                (treeSet.Contains(a.Id) && !businessSet.Contains(a.Id)
                    && (a.Kind == MediaAssetKind.Clone || a.Kind == MediaAssetKind.Direct)) ||
                (businessSet.Contains(a.Id) && !treeSet.Contains(a.Id) && a.Kind == MediaAssetKind.Library))
            .Select(a => a.Id)
            .Distinct()
            .CountAsync(ct);

        if (matchedDistinctCount != expectedDistinctCount)
        {
            throw new UnprocessableException(
                "MEDIA_INVALID_IMAGE_REFERENCE",
                "Một hoặc nhiều ảnh tham chiếu không hợp lệ: thuộc shop khác, đã bị xoá, sai loại (tree phải là clone/ảnh upload thẳng, business phải là bản Library), hoặc trùng ở cả hai danh sách.");
        }
    }
}
