using Vsite.Domain.Abstractions;

namespace Vsite.Api.Tenancy;

/// <summary>
/// Implementation cụ thể của `ITenantContext`, đăng ký Scoped. Giá trị (audience + ShopId) được đặt
/// ĐÚNG MỘT LẦN qua <see cref="Set"/> (#116): bởi <see cref="TenantResolutionMiddleware"/> cho request
/// HTTP, hoặc bởi <see cref="TenantScopeFactory"/> cho scope ngoài request (job nền). Lần hai ném
/// <see cref="InvalidOperationException"/>, giá trị cũ không đổi — không có đường ghi đè tenant.
/// Không có setter public; chỉ code cùng assembly mới gọi được.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private bool _isSet;

    public TenantAudienceKind AudienceKind { get; private set; } = TenantAudienceKind.Main;
    public Guid? ShopId { get; private set; }

    internal void Set(TenantAudienceKind audienceKind, Guid? shopId)
    {
        if (_isSet)
        {
            throw new InvalidOperationException(
                $"TenantContext đã được đặt (AudienceKind={AudienceKind}, ShopId={ShopId}) — chỉ đặt được một lần mỗi scope.");
        }

        AudienceKind = audienceKind;
        ShopId = shopId;
        _isSet = true;
    }

    /// <summary>
    /// Chỉ dành cho <see cref="ShopMembershipEndpointFilter"/> (Portal gọi endpoint có `{shopId}`, #31): gắn
    /// thêm `ShopId` SAU KHI middleware đã đặt audience, `AudienceKind` giữ nguyên. Ném nếu `ShopId` đã có
    /// giá trị — vẫn không ghi đè được tenant đã chốt.
    /// </summary>
    internal void AttachShop(Guid shopId)
    {
        if (ShopId is not null)
        {
            throw new InvalidOperationException($"TenantContext đã có ShopId={ShopId} — không ghi đè được.");
        }

        ShopId = shopId;
    }
}
