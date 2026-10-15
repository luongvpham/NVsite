namespace Vsite.Application.Website.Options;

/// <summary>
/// Giới hạn cứng khi validate Component Tree (T4, WEBSITE-001, G6, R8). Đọc từ appsettings
/// (<see cref="SectionName"/>); đổi số không cần đổi code. Chạy TRƯỚC validate sâu để cây độc hại không tới được
/// JSON Schema/walker.
/// </summary>
public sealed class PageTreeLimits
{
    public const string SectionName = "Website:TreeLimits";

    /// <summary>Kích thước body (byte UTF-8) tối đa — `05` §5 nói tree "vài trăm KB".</summary>
    public int MaxBodyBytes { get; set; } = 1_048_576;

    /// <summary>
    /// Độ sâu node tối đa (con trực tiếp của gốc = 1). Trần cứng hơn: JSON lồng quá 64 tầng bị từ chối
    /// (cùng mặc định System.Text.Json và JsonSchema.Net) nên MaxDepth &gt; ~28 không có tác dụng.
    /// </summary>
    public int MaxDepth { get; set; } = 20;

    public int MaxNodes { get; set; } = 2000;

    /// <summary>Số lỗi tối đa trả về một lần (Inspector tô đỏ nhiều ô, không phải vô hạn).</summary>
    public int MaxErrors { get; set; } = 50;
}
