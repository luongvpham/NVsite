using Microsoft.EntityFrameworkCore;
using Vsite.Application.Identity.Interfaces;
using Vsite.Domain.Identity.Entities;
using Vsite.Domain.Identity.Enums;
using Vsite.Infrastructure.Persistence;

namespace Vsite.Infrastructure.Identity;

public sealed class UserShopMembershipService(AppDbContext db) : IUserShopMembershipService
{
    public Task<bool> IsActiveMemberAsync(Guid userId, Guid shopId, CancellationToken cancellationToken) =>
        ActiveMembership(userId, shopId).AnyAsync(cancellationToken);

    public Task<Guid?> FindActiveRoleIdAsync(Guid userId, Guid shopId, CancellationToken cancellationToken) =>
        ActiveMembership(userId, shopId).Select(us => (Guid?)us.RoleId).FirstOrDefaultAsync(cancellationToken);

    // IgnoreQueryFilters — đây CHÍNH LÀ bước xác lập tenant hợp lệ hay không, không phải đọc dữ liệu
    // trong một tenant đã biết trước, nên không dựa vào Global Query Filter — và vì thế phải lọc
    // `!IsDeleted` tường minh (#21).
    private IQueryable<UserShop> ActiveMembership(Guid userId, Guid shopId) =>
        db.UserShops.IgnoreQueryFilters()
            .Where(us => us.UserId == userId && us.ShopId == shopId && !us.IsDeleted && us.Status == UserShopStatus.Active);
}
