using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Shop.Interfaces;
using Vsite.Domain.Exceptions;

namespace Vsite.Application.Media.Commands.DeleteFromLibrary;

/// <summary>Owner-only theo cùng khuôn `UpdateShopHandler` (SHOP-001 §4.4) — endpoint đã qua
/// `RequireShopMembership()` (member bất kỳ được xác nhận), handler tự tra lại quyền Owner vì
/// business rule "chỉ Owner" là riêng của endpoint này.
///
/// Dùng <see cref="IShopOwnershipService"/> (module `Shop`) thay vì tự query `UserShop`/
/// `WellKnownRoles` trực tiếp — `Media.dependsOn = ["Shop"]`, KHÔNG gồm `Identity`
/// (`Docs/architecture/dependency-map.json`), nên không được reference thẳng `Vsite.*.Identity.*`
/// (xem doc trên <see cref="IShopOwnershipService"/> vì sao interface sống ở module `Shop`).
///
/// Asset đang là `Shop.LogoId` thì VẪN xoá được — cảnh báo nằm ở `references`, không phải một ràng
/// buộc chặn xoá ở đây (brief T6).</summary>
public sealed class DeleteFromLibraryHandler(IAppDbContext db, ICurrentUserContext currentUser, IShopOwnershipService ownership)
    : IRequestHandler<DeleteFromLibraryCommand>
{
    public async Task Handle(DeleteFromLibraryCommand request, CancellationToken cancellationToken)
    {
        var isOwner = await ownership.IsOwnerAsync(currentUser.UserId, request.ShopId, cancellationToken);
        if (!isOwner)
        {
            throw new ForbiddenAccessException("MEDIA_OWNER_REQUIRED", "Chỉ chủ shop (Owner) mới được xoá khỏi thư viện.");
        }

        var asset = await db.MediaAssets
            .FirstOrDefaultAsync(a => a.Id == request.AssetId && a.IsInLibrary, cancellationToken)
            ?? throw new NotFoundException("MediaAsset", request.AssetId);

        // Global filter đã ràng ShopId == tenant; Remove() -> AppDbContext.InterceptSoftDelete()
        // chặn EntityState.Deleted thành Modified + IsDeleted=true. KHÔNG đụng clone/phái sinh
        // (SourceAssetId trỏ record này) và KHÔNG gọi IObjectStorage.DeleteAsync.
        db.MediaAssets.Remove(asset);
        await db.SaveChangesAsync(cancellationToken);
    }
}
