using FluentValidation;
using Identity.Application.Auth.Commands.Register;
using Identity.Application.Common.Interfaces;
using Identity.Application.Common.Options;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Domain.Abstractions;

namespace Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Identity")));
        services.AddScoped<IIdentityDbContext>(sp => sp.GetRequiredService<IdentityDbContext>());

        // Rate-limit/lockout theo scope (Quyết định #32, Phase 3) — wire ngay ở Phase 0 để không
        // phải thêm hạ tầng giữa chừng, dùng thật lần đầu ở Phase 3.
        services.AddStackExchangeRedisCache(options =>
            options.Configuration = configuration.GetConnectionString("Redis"));

        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        // PasswordHasher<User> — không dùng UserManager/SignInManager đầy đủ (shape User không
        // khớp IdentityUser mặc định, xem CLAUDE.md module Identity).
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IEmailSender, LoggingEmailSender>();
        services.AddScoped<IShopLookupService, ShopLookupService>();
        services.AddScoped<IUserShopMembershipService, UserShopMembershipService>();
        services.AddScoped<ILoginAttemptThrottle, LoginAttemptThrottle>();

        var applicationAssembly = typeof(RegisterCommand).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(applicationAssembly));
        services.AddValidatorsFromAssembly(applicationAssembly);
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(Identity.Application.Common.Behaviors.ValidationBehavior<,>));

        return services;
    }
}
