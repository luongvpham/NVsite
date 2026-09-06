using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Sample.Application.Behaviors;

namespace Sample.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddSampleApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
