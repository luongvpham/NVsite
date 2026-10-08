namespace Vsite.Application.Media.Dtos;

/// <summary>Kết quả `PUT /shops/{shopId}/logo` (T7, MEDIA-001, #73, #82). <see cref="LibraryAsset"/>
/// là bản Library gốc (LongEdge 1600) — cũng là record `Shop.LogoId` trỏ tới.
/// <see cref="Derivatives"/> là toàn bộ phái sinh pre-generate theo `IDerivativePresetCatalog.For("Shop")`,
/// mỗi preset đúng một record, cùng `SourceAssetId` = `LibraryAsset.Id`.</summary>
public sealed record ShopLogoDto(MediaAssetDto LibraryAsset, IReadOnlyList<MediaAssetDto> Derivatives);
