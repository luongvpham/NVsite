using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Vsite.Application.Identity.Interfaces;
using Vsite.Application.Shop.Interfaces;
using Vsite.Infrastructure.Persistence;

namespace Vsite.IntegrationTests;

/// <summary>
/// `WebApplicationFactory&lt;Program&gt;` riêng cho module Shop — không dùng chung
/// <see cref="IdentityApiFactory"/> vì test đổi slug (SHOP-001 §4.5, "InvalidateAsync được gọi cho
/// cả hai slug") cần thay <see cref="IShopLookupService"/> thật bằng <see cref="ShopLookupServiceSpy"/>
/// để verify lời gọi, tương tự cách <see cref="TestEmailSpy"/> thay `LoggingEmailSender`.
/// </summary>
public sealed class ShopApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("vsite_test")
        .WithUsername("vsite")
        .WithPassword("vsite_test_only")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder().WithImage("redis:7-alpine").Build();

    public TestEmailSpy EmailSpy { get; } = new();
    public ShopLookupServiceSpy ShopLookupSpy { get; } = new();

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
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSpy);

            services.RemoveAll<IShopLookupService>();
            services.AddSingleton<IShopLookupService>(ShopLookupSpy);
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _redis.StartAsync();

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        ShopLookupSpy.AttachScopeFactory(Services.GetRequiredService<IServiceScopeFactory>());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _redis.DisposeAsync();
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}

/// <summary>Singleton — ghi lại slug đã bị invalidate trong khi vẫn uỷ quyền hành vi thật cho
/// <see cref="Vsite.Infrastructure.Shop.ShopLookupService"/> (tạo scope riêng mỗi lần gọi vì
/// `AppDbContext`/`IDistributedCache` là Scoped, không thể constructor-inject thẳng vào Singleton).</summary>
public sealed class ShopLookupServiceSpy : IShopLookupService
{
    private IServiceScopeFactory? _scopeFactory;
    private readonly List<string> _invalidatedSlugs = [];
    private readonly object _lock = new();

    public IReadOnlyList<string> InvalidatedSlugs
    {
        get
        {
            lock (_lock)
            {
                return _invalidatedSlugs.ToArray();
            }
        }
    }

    public void AttachScopeFactory(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task<Guid?> FindShopIdBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory!.CreateAsyncScope();
        var inner = CreateInner(scope.ServiceProvider);
        return await inner.FindShopIdBySlugAsync(slug, cancellationToken);
    }

    public async Task InvalidateAsync(string slug, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _invalidatedSlugs.Add(slug);
        }

        await using var scope = _scopeFactory!.CreateAsyncScope();
        var inner = CreateInner(scope.ServiceProvider);
        await inner.InvalidateAsync(slug, cancellationToken);
    }

    private static Vsite.Infrastructure.Shop.ShopLookupService CreateInner(IServiceProvider sp) =>
        new(sp.GetRequiredService<AppDbContext>(), sp.GetRequiredService<IDistributedCache>());
}

[CollectionDefinition(Name)]
public sealed class ShopApiCollection : ICollectionFixture<ShopApiFactory>
{
    public const string Name = "ShopApi";
}
