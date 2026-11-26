using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vsite.Application.Identity.Jobs;
using Vsite.Application.Media.Jobs;
using Vsite.Application.Website.Jobs;

namespace Vsite.Infrastructure.Jobs;

/// <summary>
/// JOBS-001 T4/T5 — Hangfire trên Postgres hiện có (connection string <c>Identity</c>), schema riêng
/// <c>hangfire</c> do Hangfire tự tạo (<c>PrepareSchemaIfNecessary</c>; KHÔNG có EF migration cho schema này).
/// <c>IBackgroundJobClient</c> luôn có; worker chỉ chạy khi <see cref="JobsOptions.ServerEnabled"/>.
///
/// <see cref="JobsOptions"/> được đọc LAZY (lúc resolve, từ <see cref="IConfiguration"/> cuối cùng) chứ không lúc
/// đăng ký service: <c>WebApplicationFactory</c> áp <c>ConfigureAppConfiguration</c> SAU khi Program.cs chạy tới
/// đây, nên đọc sớm sẽ lờ mất <c>Jobs:ServerEnabled=false</c> của test (R8).
/// </summary>
public static class HangfireSetup
{
    public const string SchemaName = "hangfire";

    public static IServiceCollection AddJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(_ => configuration.GetSection(JobsOptions.SectionName).Get<JobsOptions>() ?? new JobsOptions());

        services.AddHangfire((sp, config) =>
        {
            // Đặt TRƯỚC mọi thứ có thể ném. AddHangfire vừa đặt log provider STATIC = AspNetCoreLogProvider(LoggerFactory của host này); bọc lại để host khác đã dispose
            // không làm Hangfire ném ObjectDisposedException (xem DisposalSafeLogProvider).
            config.UseLogProvider(new DisposalSafeLogProvider(new Hangfire.AspNetCore.AspNetCoreLogProvider(
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>())));
            var options = sp.GetRequiredService<JobsOptions>();
            config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(
                    o => o.UseNpgsqlConnection(configuration.GetConnectionString("Identity")),
                    new PostgreSqlStorageOptions
                    {
                        SchemaName = SchemaName,
                        PrepareSchemaIfNecessary = options.PrepareSchema,
                        // Hangfire.PostgreSql 1.21 mặc định retry ~5 lần với Thread.Sleep luỹ thừa (~30 s) khi DB chưa
                        // lên — đặt rõ, ngắn và cấu hình được thay vì dựa vào mặc định của thư viện.
                        StartupConnectionMaxRetries = options.StartupConnectionMaxRetries,
                        StartupConnectionBaseDelay = TimeSpan.FromMilliseconds(500),
                        StartupConnectionMaxDelay = TimeSpan.FromSeconds(2),
                    });
        });

        // Job Identity (#116 ngoại lệ (a)) — resolve từ scope của Hangfire.
        services.AddScoped<PendingRegistrationCleanupJob>();
        services.AddScoped<ExpiredTokenCleanupJob>();
        services.AddScoped<ZaloIdentityIntegrityJob>();

        // Job theo shop (#116/#117/#118) — resolve trong TenantScope của shop do ShopJobRunner dựng.
        services.AddScoped<PublicationPruner>();
        services.AddScoped<DeletedPagePurger>();
        services.AddScoped<NavigationOrphanCleaner>();
        services.AddScoped<WebsiteRetentionJob>();
        services.AddScoped<MediaOrphanCleanupJob>();

        // Khung fan-out theo shop (#116) — job con chạy trong TenantScope của từng shop.
        services.AddScoped<ShopFanOut>();
        services.AddScoped<ShopJobRunner>();

        // Worker: đăng ký luôn nhưng bọc cổng — chỉ dựng/chạy khi ServerEnabled (đọc lazy, xem doc class).
        var before = services.Count;
        var gated = false;
        services.AddHangfireServer((sp, server) => server.WorkerCount = sp.GetRequiredService<JobsOptions>().WorkerCount);
        for (var i = before; i < services.Count; i++)
        {
            var descriptor = services[i];
            if (descriptor.ServiceType != typeof(IHostedService) || descriptor.ImplementationFactory is not { } inner)
            {
                continue;
            }

            services[i] = ServiceDescriptor.Describe(
                typeof(IHostedService),
                sp => sp.GetRequiredService<JobsOptions>().ServerEnabled
                    ? (IHostedService)inner(sp)
                    : DisabledHostedService.Instance,
                descriptor.Lifetime);
            gated = true;
        }

        if (!gated)
        {
            // Thư viện đổi cách đăng ký hosted service: thà fail to còn hơn worker chạy trong test mà không ai biết.
            throw new InvalidOperationException("AddHangfireServer không đăng ký IHostedService dạng factory — cập nhật cổng ServerEnabled.");
        }

        return services;
    }

    private sealed class DisabledHostedService : IHostedService
    {
        public static readonly DisabledHostedService Instance = new();

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
