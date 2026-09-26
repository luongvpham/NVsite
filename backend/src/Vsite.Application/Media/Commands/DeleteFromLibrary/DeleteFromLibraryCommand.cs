using MediatR;

namespace Vsite.Application.Media.Commands.DeleteFromLibrary;

/// <summary>T6, MEDIA-001 — `DELETE /shops/{shopId}/media/library/{assetId}`. Chỉ `Owner` (cùng
/// khuôn <c>UpdateShopHandler</c>). Soft delete CHỈ bản Library — không đụng clone/phái sinh, không
/// xoá file (invariant Bước 4: không xoá file nào ngoài trạng thái soft-delete của record).</summary>
public sealed record DeleteFromLibraryCommand(Guid ShopId, Guid AssetId) : IRequest;
