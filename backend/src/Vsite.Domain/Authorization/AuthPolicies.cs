namespace Vsite.Domain.Authorization;

/// <summary>Tên policy dùng chung mọi module — Quyết định #32. `RequireGlobalScope` = token phải
/// có audience `vsite-main`/`vsite-portal`, KHÔNG chấp nhận `shop:{shopId}`.</summary>
public static class AuthPolicies
{
    public const string RequireGlobalScope = "RequireGlobalScope";

    /// <summary>Quyết định #114 — role claim `PlatformAdmin` VÀ audience `vsite-portal`. Policy gắn ở cấp nhóm
    /// `MapGroup("/platform")` (tức `/api/platform`), không gắn từng endpoint.</summary>
    public const string PlatformAdmin = "PlatformAdmin";
}
