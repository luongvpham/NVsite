namespace Shared.Domain;

/// <summary>DesignIdeal/architecture-guide.md §2 — entity cần audit trail + soft-delete.</summary>
public abstract class BaseAuditableEntity : BaseEntity
{
    /// <summary>UTC timestamp lúc row được ghi lần đầu.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>UTC timestamp lần sửa gần nhất.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Không FK cứng ở tầng Shared (Shared không được reference entity User của module
    /// Identity — Quyết định #1). Module nào cần FK thật thì tự cấu hình trong Infrastructure của
    /// module đó.</summary>
    public Guid? CreatedByUserId { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    /// <summary>Soft-delete — true thì bị ẩn bởi global query filter (Shared.Persistence).</summary>
    public bool IsDeleted { get; set; }

    protected BaseAuditableEntity()
    {
    }

    protected BaseAuditableEntity(Guid id) : base(id)
    {
    }
}
