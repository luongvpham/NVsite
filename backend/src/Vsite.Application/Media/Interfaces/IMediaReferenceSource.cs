using Vsite.Application.Media.Dtos;

namespace Vsite.Application.Media.Interfaces;

/// <summary>
/// T11, WEBSITE-001 (MEDIA-001 #2) — một nguồn tham chiếu tới ảnh Library, đăng ký ở DI và được <c>GetReferencesHandler</c> gom lại.
/// Public Contract: Media KHÔNG tham chiếu module Website; Website (Infrastructure) cài interface này (`PageDraftReferenceSource`).
/// Implementation đọc <c>ITenantContext</c> (không nhận ShopId qua tham số, #21.4) và chỉ trả tham chiếu CỦA SHOP HIỆN TẠI.
/// </summary>
public interface IMediaReferenceSource
{
    Task<IReadOnlyList<MediaReferenceDto>> FindAsync(Guid assetId, CancellationToken ct);
}
