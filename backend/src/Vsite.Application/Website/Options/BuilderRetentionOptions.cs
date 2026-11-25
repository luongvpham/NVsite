namespace Vsite.Application.Website.Options;

/// <summary>
/// WEBSITE-008 — cấu hình job dọn dẹp của builder (#117, #118). Section <see cref="SectionName"/>.
/// <c>IShopJob</c> không nhận tham số nên cấu hình đi qua options (tiền lệ <c>AuthOptions</c>).
/// </summary>
public sealed class BuilderRetentionOptions
{
    public const string SectionName = "Jobs:BuilderRetention";

    /// <summary>Số bản publish giữ lại, gồm bản current (#118). Tối thiểu 1.</summary>
    public int PublicationsToKeep { get; set; } = 20;

    /// <summary>Trang xoá mềm quá thời hạn này mới bị purge draft.</summary>
    public TimeSpan DeletedPageGrace { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Menu sửa trong khoảng này thì job không đụng tới.</summary>
    public TimeSpan NavigationQuietPeriod { get; set; } = TimeSpan.FromDays(1);
}
