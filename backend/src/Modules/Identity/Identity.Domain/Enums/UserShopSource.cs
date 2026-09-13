namespace Identity.Domain.Enums;

/// <summary>
/// 03 §3.3 — bất biến sau khi tạo (chỉ set ở nhánh INSERT), phục vụ thống kê khách hàng cho chủ shop.
/// </summary>
public enum UserShopSource
{
    RegisteredOnShop,
    LoggedInOnShop,
    InvitedByShop,
    ShopCreator,
}
