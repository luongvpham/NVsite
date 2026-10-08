namespace Vsite.Application.Common.Imaging;

/// <summary>
/// Giới hạn upload cho pipeline ảnh dùng chung (Quyết định #85). Đọc từ config section
/// <see cref="Section"/>, đăng ký qua <c>services.Configure&lt;ImageUploadOptions&gt;</c> trong
/// <c>AddInfrastructure()</c>.
/// </summary>
public sealed class ImageUploadOptions
{
    public const string Section = "Imaging:Upload";

    /// <summary>Giới hạn dung lượng file thô (byte) trước khi decode — Quyết định #85.</summary>
    public long MaxBytes { get; init; } = 10 * 1024 * 1024;

    /// <summary>Giới hạn tổng số pixel (Width × Height), kiểm tra từ header TRƯỚC khi decode —
    /// Quyết định #85.</summary>
    public long MaxPixels { get; init; } = 25_000_000;

    /// <summary>Cạnh dài tối đa sau xử lý cho bản Library/file full — Quyết định #53.</summary>
    public int MaxLongEdge { get; init; } = 1600;

    /// <summary>Chất lượng encode WebP đầu ra.</summary>
    public int WebpQuality { get; init; } = 82;
}
