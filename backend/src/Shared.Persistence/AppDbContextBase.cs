using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Domain;
using Shared.Domain.Abstractions;

namespace Shared.Persistence;

/// <summary>
/// DesignIdeal/architecture-guide.md §2/§5 — DbContext base dùng chung cho mọi module: mọi
/// `IdentityDbContext`/`ShopDbContext`/... nên kế thừa từ đây thay vì `DbContext` thẳng, để tự động
/// có Global Query Filter (`TenantQueryFilterExtensions`), audit stamping, và dispatch domain event
/// qua MediatR SAU khi transaction commit — không phải viết lại 3 việc này ở từng module.
/// </summary>
public abstract class AppDbContextBase : DbContext, ITenantScopedDbContext
{
    public ITenantContext TenantContext { get; }

    private readonly IPublisher? _publisher;

    protected AppDbContextBase(DbContextOptions options, ITenantContext tenantContext, IPublisher? publisher = null)
        : base(options)
    {
        TenantContext = tenantContext;
        _publisher = publisher;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // BaseEntity.DomainEvents trông giống navigation collection (IReadOnlyCollection<BaseEvent>)
        // với convention discovery của EF Core — chặn hẳn BaseEvent khỏi model, nếu không EF đòi
        // PK cho nó lúc build model (BaseEvent không phải entity, chỉ dispatch qua MediatR).
        modelBuilder.Ignore<BaseEvent>();

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
        if (_publisher is not null)
        {
            foreach (var domainEvent in events)
            {
                await _publisher.Publish(domainEvent, cancellationToken);
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
