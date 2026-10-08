using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Imaging;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Media.Dtos;
using Vsite.Domain.Exceptions;

namespace Vsite.Application.Media.Commands.CloneFromLibrary;

/// <summary>Sinh key mới (Quyết định #75 — file bất biến, không ghi đè), dùng
/// <see cref="MediaAssetWriter"/> để R4 (rollback best-effort) áp dụng như mọi command ghi khác.</summary>
public sealed class CloneFromLibraryHandler(
    IAppDbContext db,
    IObjectStorage storage,
    IImageProcessor imageProcessor,
    IImagePresetCatalog presetCatalog,
    MediaAssetWriter writer)
    : IRequestHandler<CloneFromLibraryCommand, MediaAssetDto>
{
    public async Task<MediaAssetDto> Handle(CloneFromLibraryCommand request, CancellationToken cancellationToken)
    {
        if (!presetCatalog.TryGet(request.Preset, out var preset))
        {
            throw new UnprocessableException("MEDIA_UNKNOWN_PRESET", $"Preset '{request.Preset}' không tồn tại.");
        }

        // MỘT query: Global Query Filter đã ràng ShopId == tenant + NOT IsDeleted. Không thấy (id lạ,
        // là clone, shop khác, hoặc bản Library đã soft delete) -> 404, không lộ chi tiết lý do.
        var source = await db.MediaAssets
            .FirstOrDefaultAsync(a => a.Id == request.AssetId && a.IsInLibrary, cancellationToken)
            ?? throw new NotFoundException("MediaAsset", request.AssetId);

        var stored = await storage.OpenReadAsync(source.StorageKey, cancellationToken)
            ?? throw new NotFoundException("MediaAsset", request.AssetId);

        using var sourceImage = await OpenSourceImageAsync(stored, cancellationToken);

        var focalX = request.FocalX ?? source.FocalPointX;
        var focalY = request.FocalY ?? source.FocalPointY;

        var clone = await writer.WriteDerivedAsync(source, sourceImage, preset, focalX, focalY, cancellationToken);

        await writer.SaveChangesAsync(cancellationToken);

        return MediaAssetDto.FromEntity(clone);
    }

    private async Task<ISourceImage> OpenSourceImageAsync(StoredObject stored, CancellationToken cancellationToken)
    {
        await using var content = stored.Content;
        return await imageProcessor.LoadAsync(content, cancellationToken);
    }
}
