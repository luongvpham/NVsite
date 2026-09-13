namespace Vsite.Domain.Common;

/// <summary>
/// DesignIdeal/architecture-guide.md §2. `Id` chỉ set được từ bên trong cây kế thừa (constructor)
/// — không object-initializer tự do từ bên ngoài, để entity không thể được dựng ở trạng thái
/// "id lạ" ngoài ý muốn. Entity cần id cố định (vd. seed data) phải tự expose constructor nhận
/// `Guid id` và forward xuống <see cref="BaseEntity(Guid)"/>.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    private readonly List<BaseEvent> _domainEvents = [];

    /// <summary>Dispatch qua MediatR sau khi SaveChanges thành công — xem `AppDbContextBase`.</summary>
    public IReadOnlyCollection<BaseEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected BaseEntity()
    {
    }

    protected BaseEntity(Guid id)
    {
        Id = id;
    }

    public void AddDomainEvent(BaseEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void RemoveDomainEvent(BaseEvent domainEvent) => _domainEvents.Remove(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
