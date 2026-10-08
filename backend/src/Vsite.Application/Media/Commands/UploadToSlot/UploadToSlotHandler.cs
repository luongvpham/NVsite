using MediatR;
using Vsite.Application.Common.Imaging;
using Vsite.Application.Media.Dtos;
using Vsite.Domain.Exceptions;

namespace Vsite.Application.Media.Commands.UploadToSlot;

/// <summary>T5, MEDIA-001 (#70) — xem doc trên <see cref="UploadToSlotCommand"/> cho hai chế độ.</summary>
public sealed class UploadToSlotHandler(
    IImageProcessor imageProcessor,
    IImagePresetCatalog presetCatalog,
    MediaAssetWriter writer)
    : IRequestHandler<UploadToSlotCommand, SlotUploadResultDto>
{
    public async Task<SlotUploadResultDto> Handle(UploadToSlotCommand request, CancellationToken cancellationToken)
    {
        // Validator đã xác nhận preset tồn tại — TryGet lại đây không thể miss trừ khi race đổi
        // catalog runtime (không xảy ra: catalog load một lần lúc startup, xem ImagePresetCatalog).
        if (!presetCatalog.TryGet(request.Preset, out var preset))
        {
            throw new UnprocessableException("MEDIA_UNKNOWN_PRESET", $"Preset '{request.Preset}' không tồn tại.");
        }

        using var source = await imageProcessor.LoadAsync(request.File, cancellationToken);

        if (!request.SaveToLibrary)
        {
            var direct = await writer.WriteDirectAsync(
                request.ShopId, source, preset, request.FocalX, request.FocalY,
                request.FileName, request.AltText, cancellationToken);

            await writer.SaveChangesAsync(cancellationToken);

            return new SlotUploadResultDto(MediaAssetDto.FromEntity(direct), null);
        }

        var library = await writer.WriteLibraryAsync(
            request.ShopId, source, request.FocalX, request.FocalY,
            request.FileName, request.AltText, folder: null, cancellationToken);

        var clone = await writer.WriteCloneAsync(
            library, source, preset, request.FocalX, request.FocalY, cancellationToken);

        await writer.SaveChangesAsync(cancellationToken);

        // Response trả id CLONE làm asset đặt vào tree (test bắt buộc 4) — bản Library đi kèm để
        // FE có thể hiển thị/điều hướng, nhưng không phải thứ được gán vào component tree.
        return new SlotUploadResultDto(MediaAssetDto.FromEntity(clone), MediaAssetDto.FromEntity(library));
    }
}
