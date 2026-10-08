namespace Vsite.Application.Media.Dtos;

/// <summary>
/// T6, MEDIA-001 (#71) — `GET /shops/{shopId}/media/library/{assetId}/references`. Hiện chỉ có một
/// nguồn tham chiếu thật: <c>Shop.LogoId</c>. Component Tree (draft/published page) và snapshot
/// builder CHƯA quét được — <c>08-media-asset-design.md</c> §4 mô tả nguồn thứ hai/ba này, chỉ tồn
/// tại từ module `Website` (Bước 5) và snapshot publish (Bước 8).
///
/// TODO(Bước 5/8, `08` §4): thêm `references` từ `PageDraft`/`SitePublication` quét component tree
/// khi các entity đó tồn tại — không phải thiếu sót của T6, đây là ranh giới có chủ đích theo brief.
/// </summary>
public sealed record MediaReferencesDto(IReadOnlyList<MediaReferenceDto> References);

public sealed record MediaReferenceDto(MediaReferenceKind Kind);

/// <summary>Enum serialize dạng string (Quyết định #19) qua `JsonStringEnumConverter` global.</summary>
public enum MediaReferenceKind
{
    ShopLogo,
}
