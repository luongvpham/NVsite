using Shared.Domain.Abstractions;

namespace Identity.IntegrationTests;

/// <summary>Test double — set <see cref="ShopId"/> để mô phỏng request đã resolve tenant.</summary>
public sealed class TestTenantContext(Guid? shopId = null) : ITenantContext
{
    public Guid? ShopId { get; set; } = shopId;
}
