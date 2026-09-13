using Microsoft.EntityFrameworkCore;
using Vsite.Domain.Identity.Entities;

namespace Vsite.Application.Common.Interfaces;

/// <summary>
/// Abstraction của `AppDbContext` (Infrastructure) — Application không được reference
/// Infrastructure trực tiếp (DesignIdeal/architecture-guide.md §1). `DbSet&lt;T&gt;` là pattern
/// chuẩn cho interface này (không phải vi phạm "no infra concerns" — DbSet là data-shape, không
/// phải implementation).
///
/// Module mới thêm `DbSet` của mình vào MỘT section riêng bên dưới — không tạo interface
/// `I{Module}DbContext` thứ hai (xem ghi chú lý do ở <c>AppDbContext</c>).
/// </summary>
public interface IAppDbContext
{
    // ---- Identity ----
    DbSet<User> Users { get; }
    DbSet<ExternalLogin> ExternalLogins { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserShop> UserShops { get; }
    DbSet<Shop> Shops { get; }
    DbSet<PendingRegistration> PendingRegistrations { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
