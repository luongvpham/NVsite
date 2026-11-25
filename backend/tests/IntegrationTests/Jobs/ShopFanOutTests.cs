using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vsite.Api.Tenancy;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Common.Jobs;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Shop.Enums;
using Vsite.Infrastructure.Jobs;
using Vsite.Infrastructure.Persistence;
using ShopEntity = Vsite.Domain.Shop.Entities.Shop;

namespace Vsite.IntegrationTests.Jobs;

/// <summary>#116 — khung fan-out cha/con. Postgres thật (Docker).</summary>
[Collection(PostgresCollection.Name)]
public sealed class ShopFanOutTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private readonly Guid[] _liveIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
    private readonly Guid _deletedId = Guid.NewGuid();
    private readonly ShopStatus[] _statuses = [ShopStatus.Draft, ShopStatus.Active, ShopStatus.Suspended, ShopStatus.Closed];

    public ShopFanOutTests(PostgresFixture postgres) => _postgres = postgres;

    public async Task InitializeAsync()
    {
        await using var db = CreateDb();
        await db.Database.MigrateAsync();
        for (var i = 0; i < _liveIds.Length; i++)
        {
            db.Shops.Add(new ShopEntity(_liveIds[i], $"S{i}", $"fanout-{_liveIds[i]:N}", ShopKind.Hosted, null, _statuses[i]));
        }

        var dead = new ShopEntity(_deletedId, "Dead", $"fanout-{_deletedId:N}", ShopKind.Hosted);
        db.Shops.Add(dead);
        await db.SaveChangesAsync();
        dead.IsDeleted = true;
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private AppDbContext CreateDb() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_postgres.ConnectionString).Options,
        new TestTenantContext());

    [Fact]
    public async Task ListShopIds_includes_every_status_and_excludes_soft_deleted()
    {
        await using var db = CreateDb();
        var sut = new ShopFanOut(db, new FakeJobClient());

        var ids = await sut.ListShopIdsAsync();

        Assert.All(_liveIds, id => Assert.Contains(id, ids));
        Assert.DoesNotContain(_deletedId, ids);
    }

    [Fact]
    public async Task EnqueuePerShop_enqueues_exactly_one_runner_job_per_shop()
    {
        await using var db = CreateDb();
        var client = new FakeJobClient();
        var sut = new ShopFanOut(db, client);

        var count = await sut.EnqueuePerShop<MarkerJob>();

        var all = await sut.ListShopIdsAsync();
        Assert.Equal(all.Count, count);
        Assert.Equal(all.Count, client.Created.Count);
        foreach (var job in client.Created)
        {
            Assert.Equal(typeof(ShopJobRunner), job.Type);
            Assert.Equal(nameof(ShopJobRunner.RunAsync), job.Method.Name);
            Assert.Equal([typeof(MarkerJob)], job.Method.GetGenericArguments());
            Assert.IsType<Guid>(job.Args[0]);
        }

        var enqueued = client.Created.Select(j => (Guid)j.Args[0]!).ToList();
        Assert.Equal(enqueued.Count, enqueued.Distinct().Count());
        Assert.Equal(all.OrderBy(x => x), enqueued.OrderBy(x => x));
    }

    [Fact]
    public async Task Runner_runs_job_in_the_shop_scope()
    {
        await using var provider = BuildProvider();
        MarkerJob.Seen.Clear();
        await using var outer = provider.CreateAsyncScope(); // như Hangfire: runner nằm trong scope job, chưa có tenant
        var runner = outer.ServiceProvider.GetRequiredService<ShopJobRunner>();

        await runner.RunAsync<MarkerJob>(_liveIds[1], CancellationToken.None);

        Assert.Equal([(TenantAudienceKind.Shop, (Guid?)_liveIds[1])], MarkerJob.Seen.ToArray());
    }

    [Fact]
    public async Task Runner_is_a_noop_when_shop_was_soft_deleted_after_enqueue()
    {
        await using var provider = BuildProvider();
        MarkerJob.Seen.Clear();
        await using var outer = provider.CreateAsyncScope(); // như Hangfire: runner nằm trong scope job, chưa có tenant
        var runner = outer.ServiceProvider.GetRequiredService<ShopJobRunner>();

        await runner.RunAsync<MarkerJob>(_deletedId, CancellationToken.None);
        await runner.RunAsync<MarkerJob>(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(MarkerJob.Seen);
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_postgres.ConnectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<ITenantScopeFactory, TenantScopeFactory>();
        services.AddScoped<MarkerJob>();
        services.AddScoped<ShopJobRunner>();
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    public sealed class MarkerJob(ITenantContext tenant) : IShopJob
    {
        public static readonly System.Collections.Concurrent.ConcurrentQueue<(TenantAudienceKind, Guid?)> Seen = new();

        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Seen.Enqueue((tenant.AudienceKind, tenant.ShopId));
            return Task.CompletedTask;
        }
    }

    /// <summary>`Enqueue&lt;T&gt;` là extension → rơi xuống `Create(Job, IState)`; ghi lại `Job` để kiểm.</summary>
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

/// <summary>WEBSITE-008 T1 — DI production (host thật) nối khung fan-out + options job builder.</summary>
[Collection(IdentityApiCollection.Name)]
public sealed class ShopFanOutWiringTests(IdentityApiFactory factory)
{
    [Fact]
    public void Production_host_resolves_ShopFanOut_and_ShopJobRunner()
    {
        using var scope = factory.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ShopFanOut>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ShopJobRunner>());
    }

    [Fact]
    public void Job_options_have_planned_defaults()
    {
        var retention = factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Vsite.Application.Website.Options.BuilderRetentionOptions>>().Value;
        var media = factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Vsite.Application.Media.Options.MediaCleanupOptions>>().Value;

        Assert.Equal(20, retention.PublicationsToKeep);
        Assert.Equal(TimeSpan.FromDays(7), retention.DeletedPageGrace);
        Assert.Equal(TimeSpan.FromDays(1), retention.NavigationQuietPeriod);
        Assert.Equal(TimeSpan.FromDays(7), media.OrphanGrace);
    }

    [Theory]
    [InlineData("Jobs:BuilderRetention:PublicationsToKeep", "0")]
    [InlineData("Jobs:BuilderRetention:DeletedPageGrace", "-1.00:00:00")]
    [InlineData("Jobs:BuilderRetention:NavigationQuietPeriod", "-00:00:01")]
    [InlineData("Jobs:MediaCleanup:OrphanGrace", "-1.00:00:00")]
    public void Invalid_job_options_fail_host_startup(string key, string value)
    {
        var bad = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })));

        Assert.ThrowsAny<Exception>(() => bad.Server);
    }
}
