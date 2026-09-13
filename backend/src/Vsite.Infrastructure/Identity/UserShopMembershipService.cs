using Microsoft.EntityFrameworkCore;
using Vsite.Application.Identity.Interfaces;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Identity.Enums;
using Vsite.Infrastructure.Persistence;

namespace Vsite.Infrastructure.Identity;

public sealed class UserShopMembershipService(AppDbContext db) : IUserShopMembershipService
{
    public Task<bool> IsActiveMemberAsync(Guid userId, Guid shopId, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters — đây CHÍNH LÀ bước xác lập tenant hợp lệ hay không, không phải đọc
        // dữ liệu trong một tenant đã biết trước, nên không dựa vào Global Query Filter.
        return db.UserShops.IgnoreQueryFilters()
            .AnyAsync(us => us.UserId == userId && us.ShopId == shopId && us.Status == UserShopStatus.Active, cancellationToken);
    }
}
