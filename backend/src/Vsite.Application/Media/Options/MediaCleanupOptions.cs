namespace Vsite.Application.Media.Options;

/// <summary>WEBSITE-008 — cấu hình job dọn ảnh mồ côi (#117). Section <see cref="SectionName"/>.</summary>
public sealed class MediaCleanupOptions
{
    public const string SectionName = "Jobs:MediaCleanup";

    /// <summary>Ảnh mồ côi phải không dùng liên tục quá thời hạn này mới bị xoá.</summary>
    public TimeSpan OrphanGrace { get; set; } = TimeSpan.FromDays(7);
}
