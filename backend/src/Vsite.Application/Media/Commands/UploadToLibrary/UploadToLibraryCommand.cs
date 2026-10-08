using MediatR;
using Vsite.Application.Media.Dtos;

namespace Vsite.Application.Media.Commands.UploadToLibrary;

/// <summary>T5, MEDIA-001 — `POST /shops/{shopId}/media/library`. Luôn tạo đúng 1 record
/// <c>NewLibrary</c> (LongEdge 1600, không crop). `ShopId` từ route (Quyết định #21.4).</summary>
public sealed record UploadToLibraryCommand(
    Guid ShopId,
    Stream File,
    string FileName,
    string? AltText,
    string? Folder) : IRequest<MediaAssetDto>;
