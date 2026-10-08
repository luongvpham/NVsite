using Microsoft.EntityFrameworkCore;
using Vsite.Application.Identity;
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

    // Đây CHÍNH LÀ bước xác lập tenant hợp lệ hay không (chưa có tenant context) — đọc xuyên shop qua
    // helper, điều kiện "membership còn hiệu lực" nằm sẵn trong đó. Thêm điều kiện User còn Active
    // (#90): chạy MỖI request (middleware + endpoint filter) nên token shop của tài khoản bị đình
    // chỉ mất hiệu lực ngay, không đợi access token hết hạn. Helper tắt filter của CẢ câu query nên
    // `!u.IsDeleted` của User phải viết tay.
    private IQueryable<UserShop> ActiveMembership(Guid userId, Guid shopId) =>
        db.UserShops.ActiveAcrossShops().Where(us => us.UserId == userId && us.ShopId == shopId
            && db.Users.Any(u => u.Id == us.UserId && !u.IsDeleted && u.Status == UserStatus.Active));
}
