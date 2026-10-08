using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Vsite.Application.Identity.Interfaces;
using Vsite.Infrastructure.Persistence;

namespace Vsite.IntegrationTests.Media;

/// <summary>
/// `WebApplicationFactory&lt;Program&gt;` riêng cho module Media (T5, MEDIA-001) — mô phỏng theo
/// <c>Vsite.IntegrationTests.ShopApiFactory</c>. Storage ghi vào một thư mục temp riêng cho mỗi run
/// (LocalDisk, KHÔNG dùng S3/MinIO — pipeline đã có <c>S3ObjectStorageTests</c> riêng), xoá lại lúc
/// dispose.
///
/// CẦN Docker daemon (Postgres + Redis Testcontainers) — không chạy được trên máy không có Docker,
/// xem `Docs/DOCKER-TEST-DEBT.md`.
/// </summary>
public sealed class MediaApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("vsite_test")
        .WithUsername("vsite")
        .WithPassword("vsite_test_only")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder().WithImage("redis:7-alpine").Build();

    public string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), "vsite-media-api-test-" + Guid.NewGuid());

    public TestEmailSpy EmailSpy { get; } = new();

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
                // LocalDisk tuyệt đối — không phụ thuộc ContentRootPath của WebApplicationFactory.
                ["Storage:Provider"] = "LocalDisk",
                ["Storage:LocalDiskRoot"] = StorageRoot,
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSpy);
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _redis.StartAsync();

        Directory.CreateDirectory(StorageRoot);

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (Directory.Exists(StorageRoot))
        {
            Directory.Delete(StorageRoot, recursive: true);
        }

        await _redis.DisposeAsync();
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class MediaApiCollection : ICollectionFixture<MediaApiFactory>
{
    public const string Name = "MediaApi";
}
