namespace Vsite.Infrastructure.Identity;

/// <summary>Bind từ config section "Jwt" (appsettings + user-secrets cho SigningKey thật).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string SigningKey { get; init; }
    public required string Issuer { get; init; }

    /// <summary>Quyết định #3 — TTL ngắn 10–15 phút.</summary>
    public int AccessTokenLifetimeMinutes { get; init; } = 15;
}
