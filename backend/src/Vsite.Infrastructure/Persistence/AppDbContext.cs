using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Common;
using Vsite.Domain.Identity.Entities;
using Vsite.Domain.Media.Entities;
using Vsite.Infrastructure.Persistence.Seed;
using ShopEntity = Vsite.Domain.Shop.Entities.Shop;

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
public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ITenantContext tenantContext,
    IPublisher? publisher = null,
    IAuditActor? auditActor = null)
    : DbContext(options), IAppDbContext
{
    public ITenantContext TenantContext { get; } = tenantContext;

    // ---- Identity ----
    public DbSet<User> Users => Set<User>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserShop> UserShops => Set<UserShop>();
    public DbSet<PendingRegistration> PendingRegistrations => Set<PendingRegistration>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    // ---- Shop ----
    public DbSet<ShopEntity> Shops => Set<ShopEntity>();

    // ---- Media ----
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

    /// <summary>
    /// snake_case cho MỌI tên bảng/cột/khoá/index (REFACTOR-DB-001) — bật Ở ĐÂY chứ không ở
    /// `AddDbContext`, để mọi nơi tự dựng context (test, tool) đều ra cùng một model với migration.
    /// Tên bảng là tên entity số ít (`shop`, `user_shop`, `media_asset`). SQL viết tay trong
    /// configuration (CHECK, filter index) phải dùng tên snake_case, không quote.
    /// </summary>
    /// ⚠️ Không dùng được với `AddDbContextPool` (pooling cấm OnConfiguring đổi options) — chuyển sang
    /// pool thì dời convention vào chỗ đăng ký và cho test dùng chung một helper dựng options.
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSnakeCaseNamingConvention();
    }

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
        // null khi chưa đăng nhập (Register, ForgotPassword…) hoặc ngoài HTTP (test, job) — giữ
        // nguyên giá trị đang có thay vì ghi đè bằng null.
        var actorId = auditActor?.UserId;
        foreach (var entry in ChangeTracker.Entries<BaseAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedByUserId ??= actorId;
                    if (entry.Entity.IsDeleted)
                    {
                        entry.Entity.DeletedAt ??= now; // insert sẵn ở trạng thái xoá (seed, job, test)
                    }

                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    if (actorId is not null)
                    {
                        entry.Entity.UpdatedByUserId = actorId;
                    }

                    // Xoá mềm bằng cách set thẳng IsDeleted (vd. MediaAsset.SoftDeleteFromLibrary,
                    // không đi qua EntityState.Deleted) cũng phải có DeletedAt.
                    if (entry.Entity.IsDeleted && entry.Entity.DeletedAt is null)
                    {
                        entry.Entity.DeletedAt = now;
                    }
                    else if (!entry.Entity.IsDeleted)
                    {
                        entry.Entity.DeletedAt = null; // khôi phục
                    }

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
            entry.Entity.DeletedAt ??= entry.Entity.UpdatedAt;
            if (auditActor?.UserId is { } actorId)
            {
                entry.Entity.UpdatedByUserId = actorId;
            }
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
