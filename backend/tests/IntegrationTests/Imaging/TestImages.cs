using SkiaSharp;

namespace Vsite.IntegrationTests.Imaging;

/// <summary>
/// Sinh/đọc ảnh mẫu cho test bằng SkiaSharp — dùng chung cho mọi test Imaging/Media để không phải
/// lặp helper trong từng file. Chỉ dùng trong test; code sản phẩm nằm ở `SkiaImageProcessor`.
/// </summary>
internal static class TestImages
{
    public static byte[] Jpeg(int width, int height) => Encode(Solid(width, height, SKColors.SeaGreen), SKEncodedImageFormat.Jpeg);

    public static byte[] Png(int width, int height) => Encode(Solid(width, height, SKColors.SeaGreen), SKEncodedImageFormat.Png);

    /// <summary>JPEG nhiễu ngẫu nhiên — ảnh phẳng một màu nén xuống còn vài trăm byte, không đủ để
    /// test giới hạn dung lượng nhỏ.</summary>
    public static byte[] NoisyJpeg(int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        var rng = new Random(42);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, new SKColor((byte)rng.Next(256), (byte)rng.Next(256), (byte)rng.Next(256)));
            }
        }

        return Encode(bitmap, SKEncodedImageFormat.Jpeg);
    }

    /// <summary>PNG nửa trái đỏ, nửa phải xanh.</summary>
    public static byte[] SplitColorPng(int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            using var red = new SKPaint { Color = SKColors.Red };
            using var blue = new SKPaint { Color = SKColors.Blue };
            canvas.DrawRect(0, 0, width / 2, height, red);
            canvas.DrawRect(width / 2, 0, width - (width / 2), height, blue);
        }

        return Encode(bitmap, SKEncodedImageFormat.Png);
    }

    /// <summary>Kích thước đọc từ header, không decode.</summary>
    public static (int Width, int Height) Identify(byte[] bytes)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(bytes)) ?? throw new InvalidOperationException("Không đọc được ảnh.");
        return (codec.Info.Width, codec.Info.Height);
    }

    public static int FrameCount(byte[] bytes)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(bytes)) ?? throw new InvalidOperationException("Không đọc được ảnh.");
        return codec.FrameCount;
    }

    /// <summary>Decode ảnh (WebP/PNG/JPEG) rồi trả pixel (đã un-premultiply) tại toạ độ.</summary>
    public static SKColor PixelAt(byte[] bytes, Func<int, int, (int X, int Y)> locate)
    {
        using var bitmap = SKBitmap.Decode(bytes) ?? throw new InvalidOperationException("Không decode được ảnh.");
        var (x, y) = locate(bitmap.Width, bitmap.Height);
        return bitmap.GetPixel(x, y);
    }

    /// <summary>Pixel giữa ảnh.</summary>
    public static SKColor CenterPixel(byte[] bytes) => PixelAt(bytes, (w, h) => (w / 2, h / 2));

    /// <summary>Pixel cột ngoài cùng bên phải, giữa chiều cao.</summary>
    public static SKColor RightEdgePixel(byte[] bytes) => PixelAt(bytes, (w, h) => (w - 1, h / 2));

    /// <summary>WebP có chunk metadata (EXIF / XMP / ICCP) hay không — quét FourCC trong container RIFF.</summary>
    public static bool WebpHasMetadataChunk(byte[] webp)
    {
        var text = System.Text.Encoding.Latin1.GetString(webp);
        return text.Contains("EXIF", StringComparison.Ordinal)
            || text.Contains("XMP ", StringComparison.Ordinal)
            || text.Contains("ICCP", StringComparison.Ordinal);
    }

    public static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }

    private static SKBitmap Solid(int width, int height, SKColor color)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        bitmap.Erase(color);
        return bitmap;
    }

    private static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format)
    {
        using (bitmap)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(format, 90) ?? throw new InvalidOperationException("Không mã hoá được ảnh.");
            return data.ToArray();
        }
    }
}
