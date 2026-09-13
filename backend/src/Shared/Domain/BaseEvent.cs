using MediatR;

namespace Shared.Domain;

/// <summary>
/// Base cho mọi domain event, dispatch qua MediatR SAU khi transaction commit (xem
/// `Shared.Persistence.AppDbContextBase.SaveChangesAsync`) — handler fail sau đó không để lại
/// side-effect "ma". DesignIdeal/architecture-guide.md §2.
/// </summary>
public abstract record BaseEvent : INotification;
