using MediatR;
using Vsite.Application.Common.Imaging;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Media.Dtos;
using Vsite.Application.Shop.Interfaces;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Media.Entities;

namespace Vsite.Application.Media.Commands.UploadShopLogo;

/// <summary>T7, MEDIA-001 (#73, #82) — xem doc trên <see cref="UploadShopLogoCommand"/> cho luồng
/// đầy đủ. Owner-only cùng khuôn `DeleteFromLibraryHandler` (module `Media` không reference
/// `Identity` trực tiếp — dùng <see cref="IShopOwnershipService"/>, `Media.dependsOn = ["Shop"]`).
/// `IShopLogoWriter` (module `Shop`) chỉ set property trên entity đang track qua CÙNG
/// <see cref="IAppDbContext"/> scoped — <see cref="MediaAssetWriter.SaveChangesAsync"/> là điểm
/// SaveChanges DUY NHẤT của cả request.</summary>
public sealed class UploadShopLogoHandler(
    IImageProcessor imageProcessor,
    IImagePresetCatalog presetCatalog,
    IDerivativePresetCatalog derivativePresetCatalog,
    MediaAssetWriter writer,
    IShopLogoWriter shopLogoWriter,
    ICurrentUserContext currentUser,
    IShopOwnershipService ownership)
    : IRequestHandler<UploadShopLogoCommand, ShopLogoDto>
{
    private const string DerivativeSource = "Shop";

    public async Task<ShopLogoDto> Handle(UploadShopLogoCommand request, CancellationToken cancellationToken)
    {
        var isOwner = await ownership.IsOwnerAsync(currentUser.UserId, request.ShopId, cancellationToken);
        if (!isOwner)
        {
            throw new ForbiddenAccessException("MEDIA_OWNER_REQUIRED", "Chỉ chủ shop (Owner) mới được đổi logo.");
        }

        // Artifact FULL SET (#86) — rỗng là lỗi cấu hình (startup lẽ ra đã fail fast trên file
        // thiếu/sai shape), KHÔNG sinh logo thiếu derivative. Kiểm TRƯỚC khi đụng file/ảnh.
        var presetNames = derivativePresetCatalog.For(DerivativeSource);
        if (presetNames.Count == 0)
        {
            throw new InvalidOperationException(
                $"Derivative preset set '{DerivativeSource}' rỗng — lỗi cấu hình " +
                "derivative-presets.json, không sinh logo thiếu.");
        }

        using var source = await imageProcessor.LoadAsync(request.File, cancellationToken);

        var library = await writer.WriteLibraryAsync(
            request.ShopId, source, FocalPoint.Center.X, FocalPoint.Center.Y,
            request.FileName, altText: null, folder: null, cancellationToken);

        var derivatives = new List<MediaAsset>(presetNames.Count);
        foreach (var presetName in presetNames)
        {
            if (!presetCatalog.TryGet(presetName, out var preset))
            {
                throw new InvalidOperationException(
                    $"Derivative preset '{presetName}' (source '{DerivativeSource}') không tồn tại trong " +
                    "image-presets.json — lỗi cấu hình, không phải input người dùng.");
            }

            var derived = await writer.WriteDerivedAsync(
                library, source, preset, FocalPoint.Center.X, FocalPoint.Center.Y, cancellationToken);
            derivatives.Add(derived);
        }

        // Chưa SaveChanges — set trên entity Shop đang track qua CÙNG scoped IAppDbContext.
        await shopLogoWriter.SetLogoAsync(request.ShopId, library.Id, cancellationToken);

        // MỘT SaveChangesAsync cho toàn bộ: insert Library + derivatives, update Shop.LogoId. FK
        // ghép Shop(LogoId, Id) -> MediaAsset(Id, ShopId) khiến EF Core tự order INSERT MediaAsset
        // TRƯỚC UPDATE Shop trong cùng transaction.
        await writer.SaveChangesAsync(cancellationToken);

        return new ShopLogoDto(
            MediaAssetDto.FromEntity(library),
            derivatives.Select(MediaAssetDto.FromEntity).ToArray());
    }
}
