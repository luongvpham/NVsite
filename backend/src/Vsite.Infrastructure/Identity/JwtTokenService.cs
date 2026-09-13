using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Identity.Interfaces;

namespace Vsite.Infrastructure.Identity;

/// <summary>Quyết định #3 (JWT tự cấp) + #27 (claims: sub/email/phoneNumber/name/avatar/ownerShopIds).</summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options) : IJwtTokenService
{
    public AccessTokenResult CreateAccessToken(AccessTokenRequest request)
    {
        var jwtOptions = options.Value;
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(jwtOptions.AccessTokenLifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, request.UserId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        if (!string.IsNullOrEmpty(request.Email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, request.Email));
        }

        if (!string.IsNullOrEmpty(request.Phone))
        {
            claims.Add(new Claim("phoneNumber", request.Phone));
        }

        if (!string.IsNullOrEmpty(request.Name))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Name, request.Name));
        }

        if (!string.IsNullOrEmpty(request.Avatar))
        {
            claims.Add(new Claim("avatar", request.Avatar));
        }

        // Nhiều claim cùng key "ownerShopIds" → JwtSecurityTokenHandler serialize thành JSON array —
        // đúng shape `ownerShopIds: string[]` của Quyết định #27. CHỈ để render UI shop switcher.
        claims.AddRange(request.OwnerShopIds.Select(shopId => new Claim("ownerShopIds", shopId.ToString())));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: request.Audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessTokenResult(tokenString, expiresAt);
    }

    public string GenerateOpaqueToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    public string HashToken(string rawToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash);
    }
}
