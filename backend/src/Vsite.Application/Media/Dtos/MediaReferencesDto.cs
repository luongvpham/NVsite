namespace Vsite.Application.Media.Dtos;

/// <summary>
/// T6/T11, MEDIA-001 (#71) — `GET /shops/{shopId}/media/library/{assetId}/references`. Nguồn tham chiếu:
/// <c>Shop.LogoId</c> (<see cref="MediaReferenceKind.ShopLogo"/>) và Component Tree của draft (<see cref="MediaReferenceKind.PageDraft"/>,
/// do module Website đăng ký qua <c>IMediaReferenceSource</c> — T11 WEBSITE-001). Mỗi nguồn là một <c>IMediaReferenceSource</c>.
///
/// TODO(Bước 8, `08` §4): thêm nguồn <c>SitePublication</c> (snapshot publish) khi entity đó tồn tại — cũng chỉ là một
/// <c>IMediaReferenceSource</c> mới + một giá trị enum, không đổi handler.
/// </summary>
public sealed record MediaReferencesDto(IReadOnlyList<MediaReferenceDto> References);

/// <summary><paramref name="PageId"/>/<paramref name="PageTitle"/> chỉ có với <see cref="MediaReferenceKind.PageDraft"/> (additive, null với ShopLogo).</summary>
public sealed record MediaReferenceDto(MediaReferenceKind Kind, Guid? PageId = null, string? PageTitle = null);

/// <summary>Enum serialize dạng string (Quyết định #19) qua `JsonStringEnumConverter` global.</summary>
public enum MediaReferenceKind
{
    ShopLogo,
    PageDraft,
}
