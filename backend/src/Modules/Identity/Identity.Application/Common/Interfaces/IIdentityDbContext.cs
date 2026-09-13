using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Application.Common.Interfaces;

/// <summary>
/// Abstraction của `IdentityDbContext` (Infrastructure) — Application không được reference
/// Infrastructure trực tiếp (DesignIdeal/architecture-guide.md §1). `DbSet&lt;T&gt;` là pattern
/// chuẩn cho interface này (không phải vi phạm "no infra concerns" — DbSet là data-shape, không
/// phải implementation).
/// </summary>
public interface IIdentityDbContext
{
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
