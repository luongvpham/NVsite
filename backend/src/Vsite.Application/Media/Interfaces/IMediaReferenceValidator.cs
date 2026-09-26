namespace Vsite.Application.Media.Interfaces;

/// <summary>
/// Public Contract cho Bước 5 (Website/PageDraft, chưa build — task T8, MEDIA-001, Quyết định #77,
/// `08` §3.5). Website module sẽ gọi service này khi lưu draft/page để chặn image id: thuộc shop
/// khác, đã soft-delete, hoặc sai loại (tree id phải là clone, business id phải là bản Library).
/// </summary>
public interface IMediaReferenceValidator
{
    /// <summary>
    /// Một câu query duy nhất (08 §3.5) cho cả hai danh sách. `ShopId` lấy từ
    /// <c>ITenantContext</c> — KHÔNG bao giờ nhận qua tham số (Quyết định #21.4). Sai bất kỳ id nào
    /// (thuộc shop khác, đã soft-delete, sai loại — bao gồm id xuất hiện ở CẢ HAI danh sách) → ném
    /// <c>UnprocessableException("MEDIA_INVALID_IMAGE_REFERENCE", ...)</c>. Danh sách rỗng cả hai →
    /// không query, trả về ngay (hợp lệ).
    /// </summary>
    /// <param name="treeImageIds">Id ảnh dùng trong Component Tree — phải là clone/phái sinh
    /// (<c>IsInLibrary = false</c>).</param>
    /// <param name="businessImageIds">Id ảnh dùng ở field nghiệp vụ khác (vd. Product gallery) —
    /// phải là bản Library (<c>IsInLibrary = true</c>).</param>
    Task EnsureValidAsync(
        IReadOnlyCollection<Guid> treeImageIds,
        IReadOnlyCollection<Guid> businessImageIds,
        CancellationToken ct);
}
