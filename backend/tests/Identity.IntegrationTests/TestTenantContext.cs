using Shared.Domain.Abstractions;

namespace Identity.IntegrationTests;

/// <summary>Test double — set <see cref="ShopId"/>/<see cref="AudienceKind"/> để mô phỏng request
/// đã được `TenantResolutionMiddleware` resolve.</summary>
public sealed class TestTenantContext(Guid? shopId = null) : ITenantContext
{
    public TenantAudienceKind AudienceKind { get; set; } = shopId is null ? TenantAudienceKind.Main : TenantAudienceKind.Shop;
    public Guid? ShopId { get; set; } = shopId;
}
