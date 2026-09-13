namespace Vsite.Domain.Authorization;

/// <summary>Tên policy dùng chung mọi module — Quyết định #32. `RequireGlobalScope` = token phải
/// có audience `vsite-main`/`vsite-portal`, KHÔNG chấp nhận `shop:{shopId}`.</summary>
public static class AuthPolicies
{
    public const string RequireGlobalScope = "RequireGlobalScope";
}
