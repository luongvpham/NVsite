namespace Vsite.Application.Identity.Interfaces;

public sealed record AccessTokenRequest(
    Guid UserId,
    string Audience,
    string? Email,
    string? Phone,
    string? Name,
    string? Avatar,
    IReadOnlyCollection<Guid> OwnerShopIds);

public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresAt);

/// <summary>
/// Quyết định #3 (JWT tự cấp, KHÔNG OIDC/OpenIddict) + #27 (claims: sub/email/phoneNumber/name/
/// avatar/ownerShopIds). Implementation (ký JWT, đọc secret) sống ở Vsite.Infrastructure.
/// </summary>
public interface IJwtTokenService
{
    AccessTokenResult CreateAccessToken(AccessTokenRequest request);

    /// <summary>Chuỗi ngẫu nhiên thô cho refresh token — hash trước khi lưu DB (không lưu raw),
    /// cùng nguyên tắc với `PendingRegistration.TokenHash` (03 §5).</summary>
    string GenerateOpaqueToken();

    string HashToken(string rawToken);
}
