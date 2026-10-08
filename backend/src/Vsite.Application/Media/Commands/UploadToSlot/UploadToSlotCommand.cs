using MediatR;
using Vsite.Application.Media.Dtos;

namespace Vsite.Application.Media.Commands.UploadToSlot;

/// <summary>
/// T5, MEDIA-001 (#70) — `ShopId` đến từ route `/shops/{shopId}/media/slot-uploads` (Quyết định
/// #21.4), đã qua `ShopMembershipEndpointFilter` xác nhận membership. Hai chế độ theo
/// <see cref="SaveToLibrary"/>:
/// - false → 1 record <c>NewDirect</c> (crop theo <see cref="Preset"/>).
/// - true → <c>NewLibrary</c> (LongEdge 1600) + <c>NewClone</c> từ bản Library đó, CÙNG một
///   <c>SaveChangesAsync</c> (<see cref="MediaAssetWriter"/>).
/// </summary>
public sealed record UploadToSlotCommand(
    Guid ShopId,
    Stream File,
    string FileName,
    string Preset,
    float FocalX,
    float FocalY,
    bool SaveToLibrary,
    string? AltText) : IRequest<SlotUploadResultDto>;
