using Microsoft.Extensions.DependencyInjection;
using Sample.Application;
using Sample.Infrastructure;

namespace Sample.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddSampleModule(this IServiceCollection services)
    {
        services.AddSampleApplication();
        services.AddSampleInfrastructure();
        return services;
    }
}
