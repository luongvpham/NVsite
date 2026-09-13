using Identity.Application.Common.Interfaces;
using Identity.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Api;

/// <summary>Root `Api` chỉ gọi đúng HAI method này — không tự ý gọi thẳng
/// `AddIdentityInfrastructure` để giữ đúng quy ước "Api chỉ reference {Module}.Api" (backend/CLAUDE.md).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIdentityInfrastructure(configuration);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();
        return services;
    }
}
