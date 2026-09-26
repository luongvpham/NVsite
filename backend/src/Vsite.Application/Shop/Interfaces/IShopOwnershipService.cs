namespace Vsite.Application.Shop.Interfaces;

/// <summary>
/// T6, MEDIA-001 — facade "user này có phải Owner của shop này không", để module khác (vd. `Media`,
/// `Media.dependsOn = ["Shop"]` — KHÔNG gồm `Identity`, xem `Docs/architecture/dependency-map.json`)
/// kiểm tra được quyền Owner mà không phải reference thẳng `Vsite.*.Identity.*`
/// (`UserShop`/`WellKnownRoles` là entity/hằng số của module Identity).
///
/// Interface sống ở `Application/Shop` (không phải `Application/Identity`) đúng theo mẫu
/// `IShopLookupService` — implementation thật (`Vsite.Infrastructure.Shop.ShopOwnershipService`)
/// được PHÉP reference Identity vì `Shop.dependsOn = ["Identity"]`.
/// </summary>
public interface IShopOwnershipService
{
    /// <summary>`true` khi có `UserShop(userId, shopId)` còn `Active` VÀ role là `Owner`. Không phân
    /// biệt "không phải thành viên" với "thành viên nhưng không phải Owner" — cả hai đều `false`,
    /// caller tự quyết error_code phù hợp ngữ cảnh của mình.</summary>
    Task<bool> IsOwnerAsync(Guid userId, Guid shopId, CancellationToken cancellationToken);
}
