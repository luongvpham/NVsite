using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Domain.Exceptions;

namespace Vsite.Application.Media.Commands.DeleteFromLibrary;

/// <summary>Owner-only — quyền kiểm ở endpoint (`RequireShopOwner`, REFACTOR-BE-001), handler
/// không tự tra `UserShop`/role.
///
/// Asset đang là `Shop.LogoId` thì VẪN xoá được — cảnh báo nằm ở `references`, không phải một ràng
/// buộc chặn xoá ở đây (brief T6).</summary>
public sealed class DeleteFromLibraryHandler(IAppDbContext db)
    : IRequestHandler<DeleteFromLibraryCommand>
{
    public async Task Handle(DeleteFromLibraryCommand request, CancellationToken cancellationToken)
    {
        var asset = await db.MediaAssets
            .FirstOrDefaultAsync(a => a.Id == request.AssetId && a.IsInLibrary, cancellationToken)
            ?? throw new NotFoundException("MediaAsset", request.AssetId);

        // Global filter đã ràng ShopId == tenant. `asset.SoftDeleteFromLibrary()` (KHÔNG
        // `db.MediaAssets.Remove(asset)` — xem XML doc trên method đó vì sao Remove() làm clone mất
        // SourceAssetId thật qua cascade fix-up của EF Core, review fix #72). KHÔNG đụng clone/phái
        // sinh (SourceAssetId trỏ record này) và KHÔNG gọi IObjectStorage.DeleteAsync.
        asset.SoftDeleteFromLibrary();
        await db.SaveChangesAsync(cancellationToken);
    }
}
