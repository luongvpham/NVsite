using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.Redis;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Identity.Interfaces;
using Vsite.Infrastructure.Persistence;

namespace Vsite.IntegrationTests;

/// <summary>
/// `WebApplicationFactory&lt;Program&gt;` thật (không chỉ DbContext trực tiếp như
/// <see cref="InfrastructurePipelineTests"/>) — cần cho Phase 3 vì test phải đi qua TOÀN BỘ pipeline
/// HTTP thật (`TenantResolutionMiddleware` → `UseAuthentication` →
/// `ShopMembershipValidationMiddleware` → `UseAuthorization`), không chỉ handler đơn lẻ. Dùng chung
/// Postgres container với <see cref="PostgresFixture"/> (qua collection), tự dựng Redis container
/// riêng vì `ShopLookupService`/`LoginAttemptThrottle` cần `IDistributedCache` thật.
/// </summary>
public sealed class IdentityApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private readonly RedisContainer _redis = new RedisBuilder().WithImage("redis:7-alpine").Build();

    public TestEmailSpy EmailSpy { get; } = new();

    public IdentityApiFactory(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Identity"] = _postgres.ConnectionString,
                ["ConnectionStrings:Redis"] = _redis.GetConnectionString(),
                ["Jwt:SigningKey"] = "test-signing-key-not-for-production-use-32-chars-min",
                ["Jwt:Issuer"] = "vsite-test",
                ["Jwt:AccessTokenLifetimeMinutes"] = "15",
                ["Auth:ApiBaseUrl"] = "http://localhost",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Thay LoggingEmailSender (chỉ log ra console) bằng spy bắt lại nội dung email — test
            // cần đọc raw verify-email token, không có cách nào khác lấy token này (DB chỉ lưu hash).
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSpy);
        });
    }

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _redis.DisposeAsync();
        await base.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class IdentityApiCollection : ICollectionFixture<PostgresFixture>, ICollectionFixture<IdentityApiFactory>
{
    public const string Name = "IdentityApi";
}
