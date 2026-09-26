using Microsoft.EntityFrameworkCore;
using Vsite.Application.Shop.Interfaces;
using Vsite.Domain.Identity;
using Vsite.Domain.Identity.Enums;
using Vsite.Infrastructure.Persistence;

namespace Vsite.Infrastructure.Shop;

/// <summary>Implementation thật của <see cref="IShopOwnershipService"/> — cùng khuôn truy vấn
/// `UpdateShopHandler`/`UserShopMembershipService`: `IgnoreQueryFilters()` vì đây CHÍNH LÀ bước xác
/// lập quyền, không phải đọc dữ liệu trong một tenant đã biết trước.</summary>
public sealed class ShopOwnershipService(AppDbContext db) : IShopOwnershipService
{
    public Task<bool> IsOwnerAsync(Guid userId, Guid shopId, CancellationToken cancellationToken) =>
        db.UserShops.IgnoreQueryFilters()
            .AnyAsync(
                us => us.UserId == userId
                    && us.ShopId == shopId
                    && us.Status == UserShopStatus.Active
                    && us.RoleId == WellKnownRoles.OwnerId,
                cancellationToken);
}
