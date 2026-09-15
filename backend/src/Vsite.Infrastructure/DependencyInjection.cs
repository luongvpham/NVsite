using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vsite.Application.Common.Behaviors;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Identity.Auth.Commands.Register;
using Vsite.Application.Identity.Interfaces;
using Vsite.Application.Identity.Options;
using Vsite.Application.Shop.Interfaces;
using Vsite.Domain.Identity.Entities;
using Vsite.Infrastructure.Identity;
using Vsite.Infrastructure.Persistence;
using Vsite.Infrastructure.Shop;

namespace Vsite.Infrastructure;

/// <summary>
/// Điểm DUY NHẤT wiring Infrastructure — `Program.cs` chỉ gọi `AddInfrastructure()`, không tự đăng
/// ký implementation nào (architecture-guide.md §1 "API layer clarification"). Module mới thêm
/// registration của mình vào một section riêng bên dưới.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Identity")));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // Redis: cache slug→ShopId cho TenantResolutionMiddleware (Quyết định #7) + đếm lockout
        // đăng nhập theo scope (Quyết định #32).
        services.AddStackExchangeRedisCache(options =>
            options.Configuration = configuration.GetConnectionString("Redis"));

        // MediatR + FluentValidation quét assembly Application (mọi module nằm chung assembly này).
        var applicationAssembly = typeof(RegisterCommand).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(applicationAssembly));
        services.AddValidatorsFromAssembly(applicationAssembly);
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddIdentityModule(configuration);
        services.AddShopModule();

        return services;
    }

    // ---- Identity ----
    private static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        // PasswordHasher<User> — không dùng UserManager/SignInManager đầy đủ (shape User không
        // khớp IdentityUser mặc định, xem backend/docs/modules/identity.md).
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IEmailSender, LoggingEmailSender>();
        services.AddScoped<IUserShopMembershipService, UserShopMembershipService>();
        services.AddScoped<ILoginAttemptThrottle, LoginAttemptThrottle>();

        return services;
    }

    // ---- Shop ----
    private static IServiceCollection AddShopModule(this IServiceCollection services)
    {
        services.AddScoped<IShopLookupService, ShopLookupService>();

        return services;
    }
}
