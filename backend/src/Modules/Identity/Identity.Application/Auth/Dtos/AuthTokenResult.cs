namespace Identity.Application.Auth.Dtos;

/// <summary>Trả về từ Login/RefreshToken — dùng chung vì cùng shape (Quyết định #27, #3 rotation).</summary>
public sealed record AuthTokenResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
