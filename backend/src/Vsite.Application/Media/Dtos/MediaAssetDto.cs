using Vsite.Domain.Media.Entities;

namespace Vsite.Application.Media.Dtos;

/// <summary>T5, MEDIA-001 — shape trả về cho mọi endpoint đọc/ghi <see cref="MediaAsset"/> (bảng
/// endpoint module ở `backend/docs/modules/media.md`).</summary>
public sealed record MediaAssetDto(
    Guid Id,
    string StorageKey,
    string MimeType,
    int Width,
    int Height,
    long SizeBytes,
    string? AltText,
    float FocalPointX,
    float FocalPointY,
    string? OriginalFileName,
    string? Folder,
    bool IsInLibrary,
    string? Preset,
    Guid? SourceAssetId,
    DateTimeOffset CreatedAt)
{
    public static MediaAssetDto FromEntity(MediaAsset asset) => new(
        asset.Id,
        asset.StorageKey,
        asset.MimeType,
        asset.Width,
        asset.Height,
        asset.SizeBytes,
        asset.AltText,
        asset.FocalPointX,
        asset.FocalPointY,
        asset.OriginalFileName,
        asset.Folder,
        asset.IsInLibrary,
        asset.Preset,
        asset.SourceAssetId,
        asset.CreatedAt);
}

/// <summary>Kết quả `POST /shops/{shopId}/media/slot-uploads` — hai chế độ (T5). <see cref="Asset"/>
/// luôn là bản record đặt vào tree (bản Direct khi không tick, bản Derived/clone khi có tick);
/// <see cref="LibraryAsset"/> chỉ có giá trị khi tick "Lưu vào thư viện".</summary>
public sealed record SlotUploadResultDto(MediaAssetDto Asset, MediaAssetDto? LibraryAsset);
