using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Vsite.Application.Common.Imaging;

namespace Vsite.Infrastructure.Imaging;

/// <summary>
/// <see cref="ISourceImage"/> giữ ảnh gốc đã qua <see cref="ImageSharpImageProcessor.LoadAsync"/>
/// (đã AutoOrient, đã xoá metadata) trong bộ nhớ, để render nhiều biến thể (`ImageTransform`) mà
/// không phải decode lại. Luôn xuất WebP (Quyết định #53).
/// </summary>
internal sealed class SourceImage(Image<Rgba32> image, ImageUploadOptions options) : ISourceImage
{
    public int Width => image.Width;

    public int Height => image.Height;

    public async Task<EncodedImage> RenderAsync(ImageTransform transform, CancellationToken ct)
    {
        using var working = image.Clone(_ => { });
        ApplyTransform(working, transform);

        using var output = new MemoryStream();
        var encoder = new WebpEncoder { Quality = options.WebpQuality };
        await working.SaveAsWebpAsync(output, encoder, ct);

        return new EncodedImage(output.ToArray(), working.Width, working.Height);
    }

    public void Dispose() => image.Dispose();

    private static void ApplyTransform(Image<Rgba32> img, ImageTransform transform)
    {
        switch (transform)
        {
            case ImageTransform.LongEdge longEdge:
                ApplyLongEdge(img, longEdge.Max);
                break;
            case ImageTransform.Inside inside:
                ApplyInside(img, inside.Width, inside.Height);
                break;
            case ImageTransform.Cover cover:
                ApplyCover(img, cover.Width, cover.Height, cover.Focal);
                break;
            default:
                throw new NotSupportedException($"ImageTransform '{transform.GetType().Name}' chưa được hỗ trợ.");
        }
    }

    /// <summary>Chỉ giới hạn cạnh dài, giữ tỉ lệ, CHỈ thu nhỏ nếu cạnh dài gốc lớn hơn <paramref name="max"/>.</summary>
    private static void ApplyLongEdge(Image<Rgba32> img, int max)
    {
        var longEdge = Math.Max(img.Width, img.Height);
        if (longEdge <= max)
        {
            return;
        }

        var scale = (double)max / longEdge;
        var size = new Size(
            Math.Max(1, (int)Math.Round(img.Width * scale)),
            Math.Max(1, (int)Math.Round(img.Height * scale)));
        img.Mutate(x => x.Resize(size));
    }

    /// <summary>Thu nhỏ để vừa khít khung (contain, giữ tỉ lệ, không crop) — CHỈ thu nhỏ.</summary>
    private static void ApplyInside(Image<Rgba32> img, int boxWidth, int boxHeight)
    {
        if (img.Width <= boxWidth && img.Height <= boxHeight)
        {
            return;
        }

        var scale = Math.Min((double)boxWidth / img.Width, (double)boxHeight / img.Height);
        var size = new Size(
            Math.Max(1, (int)Math.Round(img.Width * scale)),
            Math.Max(1, (int)Math.Round(img.Height * scale)));
        img.Mutate(x => x.Resize(size));
    }

    /// <summary>Crop đúng tỉ lệ khung quanh <paramref name="focal"/> ở độ phân giải gốc, rồi CHỈ thu
    /// nhỏ nếu crop lớn hơn khung yêu cầu (R3) — ảnh nhỏ hơn preset giữ nguyên kích thước crop thật.</summary>
    private static void ApplyCover(Image<Rgba32> img, int targetWidth, int targetHeight, FocalPoint focal)
    {
        // Kích thước crop luôn ≥ 1px: ảnh 1×N hoặc N×1 với preset dọc/ngang làm phép làm tròn ra 0 →
        // Crop ném ngoại lệ → HTTP 500 (final fix S1).
        var targetAspect = (double)targetWidth / targetHeight;
        var srcAspect = (double)img.Width / img.Height;

        int cropWidth, cropHeight;
        if (srcAspect > targetAspect)
        {
            cropHeight = img.Height;
            cropWidth = Math.Clamp((int)Math.Round(img.Height * targetAspect), 1, img.Width);
        }
        else
        {
            cropWidth = img.Width;
            cropHeight = Math.Clamp((int)Math.Round(img.Width / targetAspect), 1, img.Height);
        }

        var centerX = (int)Math.Round(focal.X * img.Width);
        var centerY = (int)Math.Round(focal.Y * img.Height);
        var x = Math.Clamp(centerX - (cropWidth / 2), 0, img.Width - cropWidth);
        var y = Math.Clamp(centerY - (cropHeight / 2), 0, img.Height - cropHeight);

        img.Mutate(ctx => ctx.Crop(new Rectangle(x, y, cropWidth, cropHeight)));

        if (img.Width > targetWidth)
        {
            img.Mutate(ctx => ctx.Resize(targetWidth, targetHeight));
        }
    }
}
