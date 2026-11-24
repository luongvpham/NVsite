namespace Vsite.Infrastructure.Jobs;

/// <summary>Section <c>Jobs</c> (JOBS-001, #115). Mặc định phục vụ chạy thật; test đặt <c>ServerEnabled=false</c> (R8).</summary>
public sealed class JobsOptions
{
    public const string SectionName = "Jobs";

    /// <summary>Chạy Hangfire server trong process này. Tắt ở integration test để không có worker nền chạy giữa chừng.</summary>
    public bool ServerEnabled { get; set; } = true;

    public int WorkerCount { get; set; } = 2;

    /// <summary>Múi giờ IANA cho lịch cron (ICU — cùng kết quả Windows dev và Linux prod, R6).</summary>
    public string TimeZone { get; set; } = "Asia/Ho_Chi_Minh";

    /// <summary>Đệm trước khi dọn token/đăng ký hết hạn; mặc định 0 = xoá ngay khi hết hạn.</summary>
    public TimeSpan TokenCleanupGrace { get; set; } = TimeSpan.Zero;
}
