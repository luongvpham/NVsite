using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Common;
using Vsite.Domain.Identity.Entities;
using Vsite.Infrastructure.Persistence.Seed;

namespace Vsite.Infrastructure.Persistence;

/// <summary>
/// MỘT DbContext cho toàn hệ (architecture-guide.md §1/§5). Mọi module thêm `DbSet` của mình vào
/// đây và đặt `IEntityTypeConfiguration` dưới `Configurations/{Module}/` — không tạo DbContext thứ
/// hai. Lý do (Quyết định #1, sửa 2026-09-13): thiết kế có ≥6 FK **xuyên module**, trong đó
/// `Listing.(TargetPageId, ShopId) → Page(Id, ShopId)` là biện pháp bảo mật ở tầng DB (`04` §4.1);
/// nhiều DbContext thì EF Core không diễn đạt được các FK đó.
///
/// Ba việc được xử lý tập trung ở đây, module không phải viết lại:
/// Global Query Filter theo ShopId + soft-delete, audit stamping, và dispatch domain event qua
/// MediatR SAU khi transaction commit.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext, IPublisher? publisher = null)
    : DbContext(options), IAppDbContext
{
    public ITenantContext TenantContext { get; } = tenantContext;

    // ---- Identity ----
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
        base.OnModelCreating(modelBuilder);

        // Tự quét mọi IEntityTypeConfiguration trong assembly này — module mới chỉ cần thêm file
        // vào Configurations/{Module}/, không phải sửa chỗ này.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<Role>().HasData(RoleSeed.All);

        // BaseEntity.DomainEvents trông giống navigation collection (IReadOnlyCollection<BaseEvent>)
        // với convention discovery của EF Core — chặn hẳn BaseEvent khỏi model, nếu không EF đòi
        // PK cho nó lúc build model (BaseEvent không phải entity, chỉ dispatch qua MediatR).
        modelBuilder.Ignore<BaseEvent>();

        // Phải chạy SAU khi entity type đã được discover/configure.
        modelBuilder.ApplyGlobalFilters(this);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditFields();
        InterceptSoftDelete();

        var events = CollectDomainEvents();

        var result = await base.SaveChangesAsync(cancellationToken);

        // Dispatch SAU khi commit thành công — handler lỗi không để lại side-effect "ma" (xem
        // BaseEvent.cs). Không rollback transaction nếu handler fail; đây là quyết định thiết kế
        // của architecture-guide.md §2, không phải sơ suất.
        if (publisher is not null)
        {
            foreach (var domainEvent in events)
            {
                await publisher.Publish(domainEvent, cancellationToken);
            }
        }

        return result;
    }

    private void StampAuditFields()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }

    /// <summary>Xoá luôn là soft-delete cho entity kế thừa BaseAuditableEntity — EntityState.Deleted
    /// bị chặn lại thành Modified + IsDeleted=true, để Global Query Filter tự ẩn nó đi.</summary>
    private void InterceptSoftDelete()
    {
        foreach (var entry in ChangeTracker.Entries<BaseAuditableEntity>())
        {
            if (entry.State != EntityState.Deleted)
            {
                continue;
            }

            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private List<BaseEvent> CollectDomainEvents()
    {
        var entitiesWithEvents = ChangeTracker.Entries<BaseEntity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();

        var events = entitiesWithEvents.SelectMany(e => e.DomainEvents).ToList();

        foreach (var entity in entitiesWithEvents)
        {
            entity.ClearDomainEvents();
        }

        return events;
    }
}
