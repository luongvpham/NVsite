using Microsoft.EntityFrameworkCore;
using Vsite.Domain.Identity.Entities;
using Vsite.Domain.Identity.Enums;

namespace Vsite.Application.Identity;

/// <summary>
/// REFACTOR-AUTHZ-001 — điểm DUY NHẤT được đọc <see cref="UserShop"/> XUYÊN shop (bỏ Global Query
/// Filter theo tenant). `UserShop` vẫn tenant-scoped để mọi query theo shop (vd. màn "Khách hàng"
/// sau này) fail-closed; nhưng luồng auth (login, đăng ký, reset password, cấp token) đọc membership
/// THEO USER khi chưa có tenant context, nên phải bỏ filter.
///
/// `IgnoreQueryFilters()` tắt CẢ filter soft-delete — trước task này 7 handler Identity gọi thẳng
/// nó và quên `!IsDeleted`/`Status == Active` (membership đã xoá hoặc bị đình chỉ vẫn đăng nhập,
/// đổi mật khẩu, nhận mail reset được). Helper này đưa lại hai điều kiện đó vào sẵn.
///
/// `IgnoreQueryFiltersAllowlistTests` (ArchitectureTests) chặn mọi `IgnoreQueryFilters()` mới ngoài
/// allowlist — cần đọc `UserShop` xuyên shop thì dùng helper ở đây.
/// </summary>
public static class UserShopQueries
{
    /// <summary>Membership còn hiệu lực (chưa xoá mềm, <c>Status = Active</c>) ở MỌI shop — dùng
    /// cho mọi quyết định cấp quyền/đăng nhập.</summary>
    public static IQueryable<UserShop> ActiveAcrossShops(this DbSet<UserShop> userShops) =>
        userShops.AcrossShops().Where(us => us.Status == UserShopStatus.Active);

    /// <summary>Membership chưa xoá mềm ở mọi shop, MỌI <see cref="UserShopStatus"/> — dùng khi
    /// trạng thái Suspended/Invited cũng phải được tính (vd. chặn đăng ký lại để lách đình chỉ).</summary>
    public static IQueryable<UserShop> AcrossShops(this DbSet<UserShop> userShops) =>
        userShops.IgnoreQueryFilters().Where(us => !us.IsDeleted);

    /// <summary>Kể cả membership đã xoá mềm — CHỈ dùng khi cần khôi phục dòng cũ (unique index
    /// <c>(user_id, shop_id)</c> không lọc soft delete nên không thể tạo dòng mới).</summary>
    public static IQueryable<UserShop> IncludingDeletedAcrossShops(this DbSet<UserShop> userShops) =>
        userShops.IgnoreQueryFilters();
}
