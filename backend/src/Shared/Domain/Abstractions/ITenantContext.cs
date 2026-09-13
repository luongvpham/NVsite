namespace Shared.Domain.Abstractions;

/// <summary>Quyết định #27/#31 — 3 audience cố định. `Shop` luôn kèm `ShopId`; `Main`/`Portal` thì
/// `ShopId` luôn null.</summary>
public enum TenantAudienceKind
{
    Main,
    Portal,
    Shop,
}

/// <summary>
/// Nguồn duy nhất cho `ShopId`/audience hiện tại của request — KHÔNG BAO GIỜ từ request
/// body/route param do client cung cấp (Quyết định #21.4). Resolve từ `Host` header bởi
/// `Api.Tenancy.TenantResolutionMiddleware` (Quyết định #7, thu hẹp lại: chỉ Path/Subdomain qua
/// `Shop.Slug`, custom domain qua `ShopDomain` vẫn deferred tới khi module Shop đầy đủ tồn tại).
///
/// Sống ở `Shared` (không phải module cụ thể) vì mọi module có entity kế thừa
/// <see cref="ShopEntity"/>/<see cref="ShopAuditableEntity"/> đều cần cùng cơ chế Global Query
/// Filter (`Shared.Persistence.TenantQueryFilterExtensions`).
/// </summary>
public interface ITenantContext
{
    TenantAudienceKind AudienceKind { get; }

    /// <summary>Chỉ có giá trị khi <see cref="AudienceKind"/> = Shop.</summary>
    Guid? ShopId { get; }
}
