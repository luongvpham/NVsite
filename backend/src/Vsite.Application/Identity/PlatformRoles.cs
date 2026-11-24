using Vsite.Domain.Authorization;
using Vsite.Domain.Identity;
using Vsite.Domain.Identity.Entities;

namespace Vsite.Application.Identity;

/// <summary>
/// Quyết định #114 mục 1–3: claim `role = "PlatformAdmin"` tính từ `User.RoleId` (nguồn duy nhất), CHỈ
/// cho audience `vsite-portal` — không bao giờ `shop:*` (rủi ro XSS) hay `vsite-main`. Login và refresh
/// cùng gọi đây để refresh tính lại từ DB thay vì chép từ token cũ.
/// </summary>
internal static class PlatformRoles
{
    private static readonly IReadOnlyCollection<string> None = [];
    private static readonly IReadOnlyCollection<string> PlatformAdmin = [WellKnownRoles.PlatformAdminCode];

    public static IReadOnlyCollection<string> For(User user, string audience) =>
        user.RoleId == WellKnownRoles.PlatformAdminId && audience == AudienceHelpers.Portal ? PlatformAdmin : None;
}
