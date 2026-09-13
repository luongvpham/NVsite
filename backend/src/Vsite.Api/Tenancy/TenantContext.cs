using Vsite.Domain.Abstractions;

namespace Vsite.Api.Tenancy;

/// <summary>
/// Implementation cụ thể của `ITenantContext`, đăng ký Scoped — <see cref="TenantResolutionMiddleware"/>
/// set giá trị đúng MỘT LẦN, sớm nhất có thể trong pipeline (trước mọi middleware/handler khác
/// dùng tới nó). Không có setter public — chỉ middleware (cùng assembly, `internal set`) được ghi.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    public TenantAudienceKind AudienceKind { get; internal set; } = TenantAudienceKind.Main;
    public Guid? ShopId { get; internal set; }
}
