using Hangfire;
using Vsite.Application.Identity.Jobs;

namespace Vsite.Infrastructure.Jobs;

/// <summary>
/// JOBS-001 T5 — đăng ký lịch định kỳ (idempotent: <c>AddOrUpdate</c> theo id). Chỉ gọi sau <c>app.Build()</c> khi
/// <see cref="JobsOptions.ServerEnabled"/>; host test không bao giờ ghi lịch vào storage.
/// </summary>
public static class RecurringJobs
{
    public const string Daily = "0 3 * * *";

    public const string PendingRegistrationCleanupId = "identity.pending-registration-cleanup";
    public const string ExpiredTokenCleanupId = "identity.expired-token-cleanup";
    public const string ZaloIntegrityId = "identity.zalo-integrity";

    public static void Register(IRecurringJobManager manager, JobsOptions options)
    {
        var tz = ResolveTimeZone(options.TimeZone);
        var recurring = new RecurringJobOptions { TimeZone = tz };
        var grace = options.TokenCleanupGrace;

        manager.AddOrUpdate<PendingRegistrationCleanupJob>(
            PendingRegistrationCleanupId, j => j.RunAsync(grace, CancellationToken.None), Daily, recurring);
        manager.AddOrUpdate<ExpiredTokenCleanupJob>(
            ExpiredTokenCleanupId, j => j.RunAsync(grace, CancellationToken.None), Daily, recurring);
        manager.AddOrUpdate<ZaloIdentityIntegrityJob>(
            ZaloIntegrityId, j => j.RunAsync(CancellationToken.None), Daily, recurring);
    }

    /// <summary>
    /// Ưu tiên id IANA (Linux prod đọc tzdata). Backend chạy <c>InvariantGlobalization=true</c> (Directory.Build.props)
    /// nên Windows dev KHÔNG đổi được IANA ⇒ Windows id (R6) — hai id cùng UTC+7, không DST nên lịch như nhau.
    /// </summary>
    public static TimeZoneInfo ResolveTimeZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException) when (WindowsFallbacks.TryGetValue(id, out var windowsId))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
        }
    }

    private static readonly Dictionary<string, string> WindowsFallbacks = new(StringComparer.Ordinal)
    {
        ["Asia/Ho_Chi_Minh"] = "SE Asia Standard Time",
    };
}
