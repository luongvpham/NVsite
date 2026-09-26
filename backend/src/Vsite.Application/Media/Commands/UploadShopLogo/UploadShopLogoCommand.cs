using MediatR;
using Vsite.Application.Media.Dtos;

namespace Vsite.Application.Media.Commands.UploadShopLogo;

/// <summary>T7, MEDIA-001 (#73, #82) — `PUT /shops/{shopId}/logo`. Chỉ `Owner` (kiểm ở handler,
/// cùng khuôn `DeleteFromLibraryHandler`). `ShopId` từ route (Quyết định #21.4). Luồng:
/// 1. Pipeline ảnh → 1 bản Library (LongEdge 1600, focal Center).
/// 2. Với mỗi preset trong <c>IDerivativePresetCatalog.For("Shop")</c>: 1 bản Derived (focal
///    Center). Artifact rỗng lúc runtime → lỗi cấu hình (`InvalidOperationException`), không sinh
///    logo thiếu.
/// 3. <c>IShopLogoWriter.SetLogoAsync</c>, rồi ĐÚNG MỘT <c>SaveChangesAsync</c> cho toàn bộ insert
///    (Library + derivatives) và update (`Shop.LogoId`).</summary>
public sealed record UploadShopLogoCommand(Guid ShopId, Stream File, string FileName) : IRequest<ShopLogoDto>;
