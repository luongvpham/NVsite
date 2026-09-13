using Shared.Domain.Abstractions;

namespace Shared.Persistence;

/// <summary>
/// DbContext nào muốn `TenantQueryFilterExtensions.ApplyGlobalFilters` tự động lọc theo ShopId thì
/// implement interface này (đã có sẵn qua <see cref="AppDbContextBase"/>).
/// </summary>
public interface ITenantScopedDbContext
{
    ITenantContext TenantContext { get; }
}
