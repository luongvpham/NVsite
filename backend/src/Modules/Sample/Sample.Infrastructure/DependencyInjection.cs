using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sample.Application.Abstractions;

namespace Sample.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSampleInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<SampleDbContext>(options => options.UseInMemoryDatabase("sample-module"));
        services.AddScoped<ISampleRepository, SampleRepository>();
        return services;
    }
}
