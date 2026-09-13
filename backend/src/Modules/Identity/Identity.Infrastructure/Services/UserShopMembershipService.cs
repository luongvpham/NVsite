using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Shared.Domain.Abstractions;

namespace Identity.Infrastructure.Services;

public sealed class UserShopMembershipService(IdentityDbContext db) : IUserShopMembershipService
{
    public Task<bool> IsActiveMemberAsync(Guid userId, Guid shopId, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters — đây CHÍNH LÀ bước xác lập tenant hợp lệ hay không, không phải đọc
        // dữ liệu trong một tenant đã biết trước, nên không dựa vào Global Query Filter.
        return db.UserShops.IgnoreQueryFilters()
            .AnyAsync(us => us.UserId == userId && us.ShopId == shopId && us.Status == UserShopStatus.Active, cancellationToken);
    }
}
