using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Identity.Interfaces;
using Vsite.Infrastructure.Persistence;

namespace Vsite.IntegrationTests;

/// <summary>
/// `WebApplicationFactory&lt;Program&gt;` thật (không chỉ DbContext trực tiếp như
/// <see cref="InfrastructurePipelineTests"/>) — cần cho Phase 3 vì test phải đi qua TOÀN BỘ pipeline
/// HTTP thật (`TenantResolutionMiddleware` → `UseAuthentication` →
/// `ShopMembershipValidationMiddleware` → `UseAuthorization`), không chỉ handler đơn lẻ. Tự dựng
/// Postgres VÀ Redis container riêng (không nhận `PostgresFixture` qua constructor của collection
/// khác) — xUnit không đảm bảo thứ tự khởi tạo giữa hai `ICollectionFixture` cùng collection
/// (`Type.GetInterfaces()` không có thứ tự xác định), nên từng fail thật với lỗi "unresolved
/// constructor arguments" dù container Postgres đã start và ready đúng.
/// </summary>
public sealed class IdentityApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("vsite_test")
        .WithUsername("vsite")
        .WithPassword("vsite_test_only")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder().WithImage("redis:7-alpine").Build();

    public TestEmailSpy EmailSpy { get; } = new();

    /// <summary>Dừng/khởi động lại container Postgres đang chạy — dùng để verify cache Redis
    /// (<see cref="Vsite.Infrastructure.Identity.ShopLookupService"/>) thực sự phục vụ mà không
    /// chạm DB, thay vì chỉ tình cờ đúng. Chỉ dùng trong collection RIÊNG — connection pool của
    /// Npgsql giữ handle cũ qua lần restart, nên xoá pool sau khi bật lại để request kế tiếp
    /// (kể cả của test khác lỡ dùng chung factory) không dính connection chết.</summary>
    public Task StopPostgresAsync() => _postgres.StopAsync();

    public async Task StartPostgresAsync()
    {
        await _postgres.StartAsync();
        NpgsqlConnection.ClearAllPools();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Identity"] = _postgres.GetConnectionString(),
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
        await _postgres.StartAsync();
        await _redis.StartAsync();

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _redis.DisposeAsync();
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class IdentityApiCollection : ICollectionFixture<IdentityApiFactory>
{
    public const string Name = "IdentityApi";
}
