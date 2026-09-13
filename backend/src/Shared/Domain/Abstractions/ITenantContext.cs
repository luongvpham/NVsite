namespace Shared.Domain.Abstractions;

/// <summary>
/// Nguồn duy nhất cho `ShopId` hiện tại của request — KHÔNG BAO GIỜ từ request body (Quyết định
/// #21.4). Sống ở `Shared` (không phải module cụ thể) vì mọi module có entity kế thừa
/// <see cref="ShopEntity"/>/<see cref="ShopAuditableEntity"/> đều cần applied cùng một cơ chế
/// Global Query Filter (`Shared.Persistence.TenantQueryFilterExtensions`). Implementation thật
/// (đọc JWT audience / route param) sống ở tầng Api của từng module (Quyết định #27/#31).
/// </summary>
public interface ITenantContext
{
    Guid? ShopId { get; }
}
