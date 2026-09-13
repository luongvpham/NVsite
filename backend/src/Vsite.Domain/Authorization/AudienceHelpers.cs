namespace Vsite.Domain.Authorization;

/// <summary>Quyết định #27/#31 — 3 giá trị audience cố định của JWT: `vsite-main`, `vsite-portal`,
/// `shop:{shopId}`. Parse/kiểm tra tập trung ở đây, không rải rác chuỗi ma thuật khắp nơi.</summary>
public static class AudienceHelpers
{
    public const string Main = "vsite-main";
    public const string Portal = "vsite-portal";
    private const string ShopPrefix = "shop:";

    public static string ForShop(Guid shopId) => $"{ShopPrefix}{shopId}";

    public static bool IsGlobalScope(string? audience) => audience is Main or Portal;

    public static Guid? TryGetShopId(string? audience)
    {
        if (audience is null || !audience.StartsWith(ShopPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        return Guid.TryParse(audience.AsSpan(ShopPrefix.Length), out var shopId) ? shopId : null;
    }
}
