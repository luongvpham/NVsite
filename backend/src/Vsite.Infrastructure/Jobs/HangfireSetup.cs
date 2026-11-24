using System.Reflection;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Vsite.Infrastructure.Jobs;

/// <summary>
/// JOBS-001 T4 — Hangfire trên Postgres hiện có (connection string <c>Identity</c>), schema riêng
/// <c>hangfire</c> do Hangfire tự tạo (<c>PrepareSchemaIfNecessary</c>; KHÔNG có EF migration cho schema này).
/// Server (worker) chỉ đăng ký khi <see cref="JobsOptions.ServerEnabled"/>; <c>IBackgroundJobClient</c> luôn có.
/// </summary>
public static class HangfireSetup
{
    public const string SchemaName = "hangfire";

    public static IServiceCollection AddJobs(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(JobsOptions.SectionName).Get<JobsOptions>() ?? new JobsOptions();
        services.AddSingleton(options);

        var connectionString = configuration.GetConnectionString("Identity");

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                o => o.UseNpgsqlConnection(connectionString),
                new PostgreSqlStorageOptions
                {
                    SchemaName = SchemaName,
                    PrepareSchemaIfNecessary = true,
                }));

        // Build-time OpenAPI generation (Microsoft.Extensions.ApiDescription.Server) khởi động host thật — không để nó
        // đăng ký worker vào DB dev/CI trong lúc build.
        var isDocumentGeneration = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
        if (options.ServerEnabled && !isDocumentGeneration)
        {
            services.AddHangfireServer(server => server.WorkerCount = options.WorkerCount);
        }

        return services;
    }
}
