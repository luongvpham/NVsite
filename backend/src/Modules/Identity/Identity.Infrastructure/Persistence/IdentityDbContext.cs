using Identity.Application.Common.Interfaces;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Shared.Domain.Abstractions;
using Shared.Persistence;
using MediatR;

namespace Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, ITenantContext tenantContext, IPublisher? publisher = null)
    : AppDbContextBase(options, tenantContext, publisher), IIdentityDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserShop> UserShops => Set<UserShop>();
    public DbSet<Shop> Shops => Set<Shop>();
    public DbSet<PendingRegistration> PendingRegistrations => Set<PendingRegistration>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new ExternalLoginConfiguration());
        modelBuilder.ApplyConfiguration(new UserShopConfiguration());
        modelBuilder.ApplyConfiguration(new ShopConfiguration());
        modelBuilder.ApplyConfiguration(new PendingRegistrationConfiguration());
        modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
        modelBuilder.ApplyConfiguration(new PasswordResetTokenConfiguration());

        modelBuilder.Entity<Role>().HasData(RoleSeed.All);

        // Global Query Filter (ShopId + soft-delete) áp dụng TỰ ĐỘNG cho UserShop (kế thừa
        // ShopAuditableEntity) qua base.OnModelCreating — không viết tay HasQueryFilter ở đây nữa.
        base.OnModelCreating(modelBuilder);
    }
}
