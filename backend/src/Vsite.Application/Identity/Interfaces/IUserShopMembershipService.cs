namespace Vsite.Application.Identity.Interfaces;

/// <summary>
/// Quyết định #21.5/#31 — "token cũ vẫn hiệu lực sau khi revoke quyền cho tới khi hết TTL" là rủi
/// ro chấp nhận được TRỪ KHI có middleware re-check tại mỗi request. Dùng bởi
/// `Vsite.Api.Tenancy.ShopMembershipValidationMiddleware`: token audience `shop:{shopId}` không đủ để
/// authorize — PHẢI query `UserShop(userId, shopId)` còn tồn tại và `Status = Active` mỗi request,
/// không tin claim trong token.
/// </summary>
public interface IUserShopMembershipService
{
    Task<bool> IsActiveMemberAsync(Guid userId, Guid shopId, CancellationToken cancellationToken);
}
