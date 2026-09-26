using MediatR;
using Vsite.Application.Common.Imaging;
using Vsite.Application.Media.Dtos;

namespace Vsite.Application.Media.Commands.UploadToLibrary;

public sealed class UploadToLibraryHandler(IImageProcessor imageProcessor, MediaAssetWriter writer)
    : IRequestHandler<UploadToLibraryCommand, MediaAssetDto>
{
    public async Task<MediaAssetDto> Handle(UploadToLibraryCommand request, CancellationToken cancellationToken)
    {
        using var source = await imageProcessor.LoadAsync(request.File, cancellationToken);

        var library = await writer.WriteLibraryAsync(
            request.ShopId, source, focalX: 0.5f, focalY: 0.5f,
            request.FileName, request.AltText, request.Folder, cancellationToken);

        await writer.SaveChangesAsync(cancellationToken);

        return MediaAssetDto.FromEntity(library);
    }
}
