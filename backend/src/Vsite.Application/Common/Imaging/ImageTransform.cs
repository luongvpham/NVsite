namespace Vsite.Application.Common.Imaging;

/// <summary>
/// Biến thể render yêu cầu cho một <see cref="ISourceImage"/>. Luật không upscale (R3) — xem XML
/// doc từng case.
/// </summary>
public abstract record ImageTransform
{
    /// <summary>Bản Library / file full: chỉ giới hạn cạnh dài, giữ nguyên tỉ lệ, CHỈ thu nhỏ nếu
    /// ảnh gốc lớn hơn <paramref name="Max"/> — không bao giờ phóng to.</summary>
    public sealed record LongEdge(int Max) : ImageTransform;

    /// <summary>Crop đúng tỉ lệ khung (<paramref name="Width"/>×<paramref name="Height"/>) quanh
    /// <paramref name="Focal"/>, rồi CHỈ thu nhỏ nếu khung crop lớn hơn kích thước yêu cầu — ảnh nhỏ
    /// hơn preset thì giữ crop ở độ phân giải gốc (R3), `EncodedImage.Width/Height` phản ánh kích
    /// thước thật, không phải kích thước preset.</summary>
    public sealed record Cover(int Width, int Height, FocalPoint Focal) : ImageTransform;

    /// <summary>Thu nhỏ để vừa khít khung (giữ tỉ lệ, không crop) — CHỈ thu nhỏ, không upscale.</summary>
    public sealed record Inside(int Width, int Height) : ImageTransform;
}

/// <summary>Toạ độ tương đối (0..1) đánh dấu điểm quan trọng nhất của ảnh — dùng để neo crop của
/// <see cref="ImageTransform.Cover"/> thay vì luôn crop giữa ảnh.</summary>
public readonly record struct FocalPoint(float X, float Y)
{
    public static readonly FocalPoint Center = new(0.5f, 0.5f);
}

/// <summary>Kết quả render — luôn là WebP (Quyết định #53). <see cref="Width"/>/<see cref="Height"/>
/// là kích thước THẬT của <see cref="Bytes"/>, không phải kích thước yêu cầu trong
/// <see cref="ImageTransform"/> (khác nhau khi ảnh gốc nhỏ hơn preset, R3).</summary>
public sealed record EncodedImage(byte[] Bytes, int Width, int Height)
{
    public string MimeType => "image/webp";
}
