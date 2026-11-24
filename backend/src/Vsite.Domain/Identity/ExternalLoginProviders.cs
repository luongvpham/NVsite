namespace Vsite.Domain.Identity;

/// <summary>
/// Giá trị <c>ExternalLogin.Provider</c> (cột string). 03 §3.2: <c>{ Google, Facebook, Zalo }</c> — PascalCase,
/// cùng kiểu lưu với các enum khác (vd. <c>primary_identity_kind = 'Zalo'</c>).
/// </summary>
public static class ExternalLoginProviders
{
    public const string Google = "Google";
    public const string Facebook = "Facebook";
    public const string Zalo = "Zalo";
}
