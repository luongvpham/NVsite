namespace Vsite.Application.Media.Dtos;

/// <summary>
/// T6/T11, MEDIA-001 (#71) — `GET /shops/{shopId}/media/library/{assetId}/references`. Nguồn tham chiếu:
/// <c>Shop.LogoId</c> (<see cref="MediaReferenceKind.ShopLogo"/>) và Component Tree của draft (<see cref="MediaReferenceKind.PageDraft"/>,
/// do module Website đăng ký qua <c>IMediaReferenceSource</c> — T11 WEBSITE-001; T8 WEBSITE-004 thêm snapshot publish và ảnh OG/favicon).
/// Mỗi nguồn là một <c>IMediaReferenceSource</c>.
/// </summary>
public sealed record MediaReferencesDto(IReadOnlyList<MediaReferenceDto> References);

/// <summary><paramref name="PageId"/>/<paramref name="PageTitle"/> chỉ có với <see cref="MediaReferenceKind.PageDraft"/> và
/// <see cref="MediaReferenceKind.PageOgImage"/> (additive, null với các kind khác). <paramref name="IsCurrent"/> chỉ có với
/// <see cref="MediaReferenceKind.SitePublication"/>: <c>true</c> = bản đang chạy có chứa ảnh, <c>false</c> = chỉ còn ở bản cũ.</summary>
public sealed record MediaReferenceDto(MediaReferenceKind Kind, Guid? PageId = null, string? PageTitle = null, bool? IsCurrent = null);

/// <summary>Enum serialize dạng string (Quyết định #19) qua `JsonStringEnumConverter` global.</summary>
public enum MediaReferenceKind
{
    ShopLogo,
    PageDraft,
    SitePublication,
    PageOgImage,
    WebsiteOgImage,
    WebsiteFavicon,
}
