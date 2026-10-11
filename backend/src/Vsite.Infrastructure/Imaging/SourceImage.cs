using SkiaSharp;
using Vsite.Application.Common.Imaging;

namespace Vsite.Infrastructure.Imaging;

/// <summary>
/// <see cref="ISourceImage"/> giữ ảnh gốc đã qua <see cref="SkiaImageProcessor.LoadAsync"/>
/// (đã áp Orientation, không mang metadata) trong bộ nhớ, để render nhiều biến thể (`ImageTransform`)
/// mà không phải decode lại. Luôn xuất WebP (Quyết định #53).
/// </summary>
internal sealed class SourceImage : ISourceImage
{
    // Cần mipmap: thu nhỏ mạnh (vd. 25MP → 320px) bằng lọc song tuyến thường sẽ răng cưa.
    private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    private readonly SKBitmap _bitmap;
    private readonly SKImage _image;
    private readonly ImageUploadOptions _options;

    public SourceImage(SKBitmap bitmap, ImageUploadOptions options)
    {
        // Immutable → SKImage dùng chung pixel với bitmap, không nhân đôi ~100MB cho ảnh 25MP.
        bitmap.SetImmutable();
        _bitmap = bitmap;
        _image = SKImage.FromBitmap(bitmap);
        _options = options;
    }

    public int Width => _bitmap.Width;

    public int Height => _bitmap.Height;

    public Task<EncodedImage> RenderAsync(ImageTransform transform, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var (crop, outWidth, outHeight) = transform switch
        {
            ImageTransform.LongEdge longEdge => PlanLongEdge(longEdge.Max),
            ImageTransform.Inside inside => PlanInside(inside.Width, inside.Height),
            ImageTransform.Cover cover => PlanCover(cover.Width, cover.Height, cover.Focal),
            _ => throw new NotSupportedException($"ImageTransform '{transform.GetType().Name}' chưa được hỗ trợ."),
        };

        using var output = new SKBitmap(new SKImageInfo(outWidth, outHeight, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(output))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawImage(_image, crop, new SKRect(0, 0, outWidth, outHeight), Sampling, null);
        }

        using var pixmap = output.PeekPixels();
        using var data = pixmap.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossy, _options.WebpQuality))
            ?? throw new InvalidOperationException("Không mã hoá được WebP.");

        return Task.FromResult(new EncodedImage(data.ToArray(), outWidth, outHeight));
    }

    public void Dispose()
    {
        _image.Dispose();
        _bitmap.Dispose();
    }

    private SKRect FullRect => new(0, 0, Width, Height);

    /// <summary>Chỉ giới hạn cạnh dài, giữ tỉ lệ, CHỈ thu nhỏ nếu cạnh dài gốc lớn hơn <paramref name="max"/>.</summary>
    private (SKRect Crop, int Width, int Height) PlanLongEdge(int max)
    {
        var longEdge = Math.Max(Width, Height);
        if (longEdge <= max)
        {
            return (FullRect, Width, Height);
        }

        var (w, h) = Scale(max / (double)longEdge);
        return (FullRect, w, h);
    }

    /// <summary>Thu nhỏ để vừa khít khung (contain, giữ tỉ lệ, không crop) — CHỈ thu nhỏ.</summary>
    private (SKRect Crop, int Width, int Height) PlanInside(int boxWidth, int boxHeight)
    {
        if (Width <= boxWidth && Height <= boxHeight)
        {
            return (FullRect, Width, Height);
        }

        var (w, h) = Scale(Math.Min((double)boxWidth / Width, (double)boxHeight / Height));
        return (FullRect, w, h);
    }

    /// <summary>Crop đúng tỉ lệ khung quanh <paramref name="focal"/> ở độ phân giải gốc, rồi CHỈ thu
    /// nhỏ nếu crop lớn hơn khung yêu cầu (R3) — ảnh nhỏ hơn preset giữ nguyên kích thước crop thật.</summary>
    private (SKRect Crop, int Width, int Height) PlanCover(int targetWidth, int targetHeight, FocalPoint focal)
    {
        // Kích thước crop luôn ≥ 1px: ảnh 1×N hoặc N×1 với preset dọc/ngang làm phép làm tròn ra 0
        // (final fix S1).
        var targetAspect = (double)targetWidth / targetHeight;
        var srcAspect = (double)Width / Height;

        int cropWidth, cropHeight;
        if (srcAspect > targetAspect)
        {
            cropHeight = Height;
            cropWidth = Math.Clamp((int)Math.Round(Height * targetAspect), 1, Width);
        }
        else
        {
            cropWidth = Width;
            cropHeight = Math.Clamp((int)Math.Round(Width / targetAspect), 1, Height);
        }

        var centerX = (int)Math.Round(focal.X * Width);
        var centerY = (int)Math.Round(focal.Y * Height);
        var x = Math.Clamp(centerX - (cropWidth / 2), 0, Width - cropWidth);
        var y = Math.Clamp(centerY - (cropHeight / 2), 0, Height - cropHeight);

        var crop = new SKRect(x, y, x + cropWidth, y + cropHeight);
        return cropWidth > targetWidth
            ? (crop, targetWidth, targetHeight)
            : (crop, cropWidth, cropHeight);
    }

    private (int Width, int Height) Scale(double scale) => (
        Math.Max(1, (int)Math.Round(Width * scale)),
        Math.Max(1, (int)Math.Round(Height * scale)));
}
