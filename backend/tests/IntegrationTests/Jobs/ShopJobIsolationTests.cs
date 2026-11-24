using System.Collections.Concurrent;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Vsite.Api.Tenancy;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Common.Jobs;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Media.Entities;
using Vsite.Domain.Shop.Enums;
using Vsite.Infrastructure.Jobs;
using Vsite.Infrastructure.Persistence;
using ShopEntity = Vsite.Domain.Shop.Entities.Shop;

namespace Vsite.IntegrationTests.Jobs;

/// <summary>
/// step.md 8a / #116 — "job xử lý shop A không chạm dữ liệu shop B". Postgres thật. Job mẫu chạy trên bảng tenant
/// (`media_asset`) và KHÔNG nhận `shopId`: cách ly chỉ đến từ <c>BeginShopScope</c> + Global Query Filter.
/// Kiểm kết quả bằng SQL thô (không dùng IgnoreQueryFilters).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ShopJobIsolationTests : IAsyncLifetime
{
    private const string Untouched = "untouched";
    private const string Touched = "touched-by-job";
    private const int AssetsA = 3;
    private const int AssetsB = 2;

    private readonly PostgresFixture _postgres;
    private readonly Guid _shopA = Guid.NewGuid();
    private readonly Guid _shopB = Guid.NewGuid();
    private readonly Guid _shopDeleted = Guid.NewGuid();

    public ShopJobIsolationTests(PostgresFixture postgres) => _postgres = postgres;

    public async Task InitializeAsync()
    {
        SampleShopJob.Seen.Clear();
        await using (var db = CreateDb(null))
        {
            await db.Database.MigrateAsync();
            db.Shops.Add(new ShopEntity(_shopA, "A", $"iso-{_shopA:N}", ShopKind.Hosted));
            db.Shops.Add(new ShopEntity(_shopB, "B", $"iso-{_shopB:N}", ShopKind.Hosted));
            db.Shops.Add(new ShopEntity(_shopDeleted, "Dead", $"iso-{_shopDeleted:N}", ShopKind.Hosted));
            await db.SaveChangesAsync();
        }

        await SeedAssetsAsync(_shopA, AssetsA);
        await SeedAssetsAsync(_shopB, AssetsB);
        await SeedAssetsAsync(_shopDeleted, 2);

        await using (var db = CreateDb(null))
        {
            var dead = await db.Shops.SingleAsync(s => s.Id == _shopDeleted);
            dead.IsDeleted = true;
            await db.SaveChangesAsync();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Runner_for_shop_A_changes_only_shop_A_rows_and_sees_only_them()
    {
        await using var provider = BuildProvider();
        await using var outer = provider.CreateAsyncScope();
        var runner = outer.ServiceProvider.GetRequiredService<ShopJobRunner>();

        await runner.RunAsync<SampleShopJob>(_shopA, CancellationToken.None);

        Assert.Equal(AssetsA, await CountAltAsync(_shopA, Touched));
        Assert.Equal(0, await CountAltAsync(_shopA, Untouched));
        Assert.Equal(AssetsB, await CountAltAsync(_shopB, Untouched));
        Assert.Equal(0, await CountAltAsync(_shopB, Touched));

        var seen = Assert.Single(SampleShopJob.Seen.ToArray());
        Assert.Equal(AssetsA, seen.Count);
        Assert.Equal([_shopA], seen.ShopIds);
    }

    [Fact]
    public async Task Runner_for_shop_B_sees_exactly_its_own_row_count()
    {
        await using var provider = BuildProvider();
        await using var outer = provider.CreateAsyncScope();
        var runner = outer.ServiceProvider.GetRequiredService<ShopJobRunner>();

        await runner.RunAsync<SampleShopJob>(_shopB, CancellationToken.None);

        var seen = Assert.Single(SampleShopJob.Seen.ToArray());
        Assert.Equal(AssetsB, seen.Count);
        Assert.Equal([_shopB], seen.ShopIds);
        Assert.Equal(AssetsA, await CountAltAsync(_shopA, Untouched));
    }

    [Fact]
    public async Task Runner_for_a_soft_deleted_shop_changes_nothing()
    {
        await using var provider = BuildProvider();
        await using var outer = provider.CreateAsyncScope();
        var runner = outer.ServiceProvider.GetRequiredService<ShopJobRunner>();

        await runner.RunAsync<SampleShopJob>(_shopDeleted, CancellationToken.None);

        Assert.Empty(SampleShopJob.Seen);
        Assert.Equal(2, await CountAltAsync(_shopDeleted, Untouched));
        Assert.Equal(AssetsA, await CountAltAsync(_shopA, Untouched));
        Assert.Equal(AssetsB, await CountAltAsync(_shopB, Untouched));
    }

    [Fact]
    public async Task Fan_out_then_running_each_enqueued_job_updates_each_shop_with_only_its_own_data()
    {
        await using var provider = BuildProvider();
        await using var outer = provider.CreateAsyncScope();
        var client = new FakeJobClient();
        var fanOut = new ShopFanOut(outer.ServiceProvider.GetRequiredService<IAppDbContext>(), client);

        await fanOut.EnqueuePerShop<SampleShopJob>();

        // Fixture Postgres dùng chung — chỉ chạy job con của shop do test này tạo.
        var mine = client.Created
            .Where(j => j.Args[0] is Guid id && (id == _shopA || id == _shopB || id == _shopDeleted))
            .ToList();
        Assert.Equal(2, mine.Count); // shop xoá mềm không được enqueue

        foreach (var job in mine)
        {
            // Chạy chính Job Hangfire đã enqueue (như worker): runner resolve từ scope job, chưa có tenant.
            await using var jobScope = provider.CreateAsyncScope();
            var runner = jobScope.ServiceProvider.GetRequiredService<ShopJobRunner>();
            var task = (Task)job.Method.Invoke(runner, job.Args.ToArray())!;
            await task;
        }

        Assert.Equal(AssetsA, await CountAltAsync(_shopA, Touched));
        Assert.Equal(AssetsB, await CountAltAsync(_shopB, Touched));
        Assert.Equal(0, await CountAltAsync(_shopA, Untouched));
        Assert.Equal(0, await CountAltAsync(_shopB, Untouched));
        Assert.Equal(2, await CountAltAsync(_shopDeleted, Untouched));

        var seen = SampleShopJob.Seen.ToArray();
        Assert.Equal(2, seen.Length);
        Assert.All(seen, s => Assert.Single(s.ShopIds));
        Assert.Equal(AssetsA, seen.Single(s => s.ShopIds[0] == _shopA).Count);
        Assert.Equal(AssetsB, seen.Single(s => s.ShopIds[0] == _shopB).Count);
    }

    private AppDbContext CreateDb(Guid? shopId) => new(
        new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_postgres.ConnectionString).Options,
        new TestTenantContext(shopId));

    private async Task SeedAssetsAsync(Guid shopId, int count)
    {
        await using var db = CreateDb(shopId);
        for (var i = 0; i < count; i++)
        {
            db.MediaAssets.Add(MediaAsset.NewLibrary(
                shopId, $"shops/{shopId}/library/{Guid.NewGuid():N}.webp", 100, 100, 10, 0.5f, 0.5f, null, Untouched));
        }

        await db.SaveChangesAsync();
    }

    private async Task<long> CountAltAsync(Guid shopId, string alt)
    {
        await using var conn = new NpgsqlConnection(_postgres.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT count(*) FROM media_asset WHERE shop_id = @s AND alt_text = @a", conn);
        cmd.Parameters.AddWithValue("s", shopId);
        cmd.Parameters.AddWithValue("a", alt);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_postgres.ConnectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<ITenantScopeFactory, TenantScopeFactory>();
        services.AddScoped<SampleShopJob>();
        services.AddScoped<ShopJobRunner>();
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    /// <summary>Job mẫu — KHÔNG nhận shopId, KHÔNG tắt query filter. Thấy gì là do tenant scope quyết định.</summary>
    public sealed class SampleShopJob(IAppDbContext db) : IShopJob
    {
        public static readonly ConcurrentQueue<(int Count, Guid[] ShopIds)> Seen = new();

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var rows = await db.MediaAssets.ToListAsync(cancellationToken);
            Seen.Enqueue((rows.Count, rows.Select(r => r.ShopId).Distinct().ToArray()));
            foreach (var row in rows)
            {
                row.AltText = Touched;
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class FakeJobClient : IBackgroundJobClient
    {
        public List<Job> Created { get; } = [];

        public string Create(Job job, IState state)
        {
            Assert.IsType<EnqueuedState>(state);
            Created.Add(job);
            return Guid.NewGuid().ToString();
        }

        public bool ChangeState(string jobId, IState state, string expectedState) => throw new NotSupportedException();
    }
}
