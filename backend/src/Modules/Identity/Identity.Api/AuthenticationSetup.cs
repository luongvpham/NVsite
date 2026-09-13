using System.Text;
using Identity.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Shared.Authorization;

namespace Identity.Api;

/// <summary>
/// JWT Bearer validation + policy `RequireGlobalScope` — sống ở `Identity.Api` (không phải root
/// `Api`) vì cần `JwtOptions` (Identity.Infrastructure). Root `Api/Program.cs` chỉ gọi ĐÚNG một
/// method này, giữ đúng quy ước "Api chỉ reference {Module}.Api" (backend/CLAUDE.md).
/// </summary>
public static class AuthenticationSetup
{
    public static IServiceCollection AddIdentityAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptionsAccessor) =>
            {
                var jwtOptions = jwtOptionsAccessor.Value;
                bearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    // KHÔNG validate audience bằng cơ chế mặc định — có 3 giá trị hợp lệ khác
                    // nhau (vsite-main/vsite-portal/shop:{shopId}, shopId không cố định trước
                    // được). Tự kiểm qua policy RequireGlobalScope + ShopMembershipValidationMiddleware.
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthPolicies.RequireGlobalScope, policy =>
                policy.RequireAssertion(ctx => AudienceHelpers.IsGlobalScope(ctx.User.FindFirst("aud")?.Value)));

        return services;
    }
}
