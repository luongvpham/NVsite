using Hangfire;
using Hangfire.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Vsite.Application.Media.Jobs;
using Vsite.Application.Website.Jobs;
using Vsite.Infrastructure.Jobs;

namespace Vsite.IntegrationTests.Jobs;

/// <summary>JOBS-001 T5 — lịch định kỳ: nội dung đăng ký, và cổng <c>Jobs:ServerEnabled</c> (host test không ghi storage, không worker).</summary>
[Collection(IdentityApiCollection.Name)]
public sealed class RecurringJobsTests(IdentityApiFactory factory)
{
    [Fact]
    public void Register_adds_five_jobs_with_expected_cron_in_configured_time_zone()
    {
        var manager = new RecordingManager();
        var grace = TimeSpan.FromDays(1);

        RecurringJobs.Register(manager, new JobsOptions { TokenCleanupGrace = grace });

        Assert.Equal(
            [
                RecurringJobs.PendingRegistrationCleanupId, RecurringJobs.ExpiredTokenCleanupId, RecurringJobs.ZaloIntegrityId,
                "website.retention", "media.orphan-cleanup",
            ],
            manager.Added.Select(a => a.Id).ToArray());
        Assert.Equal(
            ["0 3 * * *", "0 3 * * *", "0 3 * * *", "30 3 * * *", "0 4 * * *"],
            manager.Added.Select(a => a.Cron).ToArray());
        Assert.All(manager.Added, a =>
            Assert.Equal(RecurringJobs.ResolveTimeZone("Asia/Ho_Chi_Minh").BaseUtcOffset, a.Options.TimeZone.BaseUtcOffset));
        Assert.Equal(grace, manager.Added[0].Job.Args[0]);
        Assert.Equal(grace, manager.Added[1].Job.Args[0]);

        // Hai job mới là fan-out cha theo shop (#116), không chạy thẳng job con.
        Assert.Equal(typeof(ShopFanOut), manager.Added[3].Job.Type);
        Assert.Equal(typeof(WebsiteRetentionJob), manager.Added[3].Job.Method.GetGenericArguments().Single());
        Assert.Equal(typeof(ShopFanOut), manager.Added[4].Job.Type);
        Assert.Equal(typeof(MediaOrphanCleanupJob), manager.Added[4].Job.Method.GetGenericArguments().Single());
    }

    [Fact]
    public async Task Test_host_with_server_disabled_writes_no_recurring_jobs_and_runs_no_worker()
    {
        Assert.False(factory.Services.GetRequiredService<JobsOptions>().ServerEnabled);

        // Không worker Hangfire nào trong danh sách hosted service đang chạy (cổng trả DisabledHostedService).
        var hosted = factory.Services.GetServices<IHostedService>().Select(h => h.GetType().Name).ToList();
        Assert.DoesNotContain("BackgroundJobServerHostedService", hosted);

        // Storage có thể đã được tạo (dashboard test) nhưng KHÔNG có recurring job nào.
        _ = factory.Server; // đảm bảo host đã dựng
        await using var conn = new NpgsqlConnection(factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("Identity"));
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT to_regclass('hangfire.set') IS NOT NULL", conn);
        var schemaExists = (bool)(await cmd.ExecuteScalarAsync())!;
        if (schemaExists)
        {
            await using var count = new NpgsqlCommand("SELECT count(*) FROM hangfire.set WHERE key = 'recurring-jobs'", conn);
            Assert.Equal(0L, (long)(await count.ExecuteScalarAsync())!);
        }
    }

    private sealed class RecordingManager : IRecurringJobManager
    {
        public List<(string Id, Job Job, string Cron, RecurringJobOptions Options)> Added { get; } = [];

        public void AddOrUpdate(string recurringJobId, Job job, string cronExpression, RecurringJobOptions options) =>
            Added.Add((recurringJobId, job, cronExpression, options));

        public void Trigger(string recurringJobId) => throw new NotSupportedException();

        public void RemoveIfExists(string recurringJobId) => throw new NotSupportedException();
    }
}
