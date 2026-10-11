using Microsoft.Extensions.Options;
using SkiaSharp;
using Vsite.Application.Common.Imaging;
using Vsite.Domain.Exceptions;

namespace Vsite.Infrastructure.Imaging;

/// <summary>
/// Implementation duy nhất của <see cref="IImageProcessor"/>, dựa trên SkiaSharp (MIT) — thay
/// ImageSharp vì mọi bản ImageSharp đã vá CVE đều đòi file license lúc build (Quyết định #84).
/// Thứ tự xử lý CỐ ĐỊNH trong <see cref="LoadAsync"/> — đừng đổi thứ tự khi sửa: dung lượng → magic
/// bytes → pixel từ header (chưa decode) → decode → áp EXIF Orientation. Metadata (EXIF/ICC/XMP) bị
/// loại tự nhiên vì ảnh được giữ dưới dạng bitmap thô rồi mã hoá lại từ đầu; riêng Orientation phải
/// áp vào pixel TRƯỚC khi bỏ (R2), nếu không ảnh chụp dọc bị lưu nằm ngang.
/// </summary>
public sealed class SkiaImageProcessor(IOptions<ImageUploadOptions> options) : IImageProcessor
{
    private readonly ImageUploadOptions _options = options.Value;

    public async Task<ISourceImage> LoadAsync(Stream input, CancellationToken ct)
    {
        // 1) Dung lượng — đọc có giới hạn, không tin Stream.Length (stream network có thể không
        // seekable / báo sai Length).
        var bytes = await ReadCappedAsync(input, _options.MaxBytes, ct);

        // 2) Magic bytes — không bao giờ tin extension/Content-Type client gửi lên.
        var kind = DetectKind(bytes);
        if (kind == ImageKind.Heic)
        {
            throw new UnprocessableException(
                ImageErrorCodes.HeicUnsupported,
                "Định dạng HEIC chưa được hỗ trợ, vui lòng chuyển sang JPEG/PNG/WebP.");
        }

        if (kind == ImageKind.Unsupported)
        {
            throw new UnprocessableException(
                ImageErrorCodes.UnsupportedFormat,
                "Định dạng ảnh không được hỗ trợ.");
        }

        // Client huỷ request — phải để nguyên cho caller xử lý (vd. 499), không nuốt thành 422.
        ct.ThrowIfCancellationRequested();

        // 3) Pixel từ header — SKCodec.Create chỉ đọc header, KHÔNG decode ảnh: tránh tốn CPU/RAM giải
        // mã một ảnh sẽ bị từ chối ngay sau đó vì quá nhiều pixel (bomb ảnh nén nhỏ, pixel khổng lồ).
        using var codec = SKCodec.Create(new SKMemoryStream(bytes))
            ?? throw new UnprocessableException(ImageErrorCodes.CorruptImage, "Không đọc được thông tin ảnh.");

        var width = codec.Info.Width;
        var height = codec.Info.Height;
        if (width < 1 || height < 1)
        {
            throw new UnprocessableException(ImageErrorCodes.CorruptImage, "Không đọc được thông tin ảnh.");
        }

        var pixels = (long)width * height;
        if (pixels > _options.MaxPixels)
        {
            throw new UnprocessableException(
                ImageErrorCodes.TooManyPixels,
                $"Ảnh có {pixels} pixel, vượt quá giới hạn {_options.MaxPixels}.");
        }

        ct.ThrowIfCancellationRequested();

        // 4) Decode thật. `GetPixels` mặc định chỉ giải mã KHUNG ĐẦU của WebP động — không bao giờ chạm
        // byte của khung sau (R5; test `Animated_webp_decode_never_touches_frame_2`). Premul để
        // resize/encode đúng alpha.
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);
        try
        {
            var result = codec.GetPixels(info, bitmap.GetPixels());
            if (result != SKCodecResult.Success)
            {
                throw new UnprocessableException(ImageErrorCodes.CorruptImage, "Dữ liệu ảnh bị hỏng, không decode được.");
            }

            // 5) Áp EXIF Orientation vào pixel (R2). Từ đây ảnh không còn mang metadata nào.
            var oriented = ApplyOrigin(bitmap, codec.EncodedOrigin);
            if (!ReferenceEquals(oriented, bitmap))
            {
                bitmap.Dispose();
            }

            return new SourceImage(oriented, _options);
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    /// <summary>Xoay/lật bitmap theo EXIF Orientation (cùng ma trận với <c>SkEncodedOriginToMatrix</c>
    /// của Skia). Trả chính <paramref name="src"/> nếu Orientation là mặc định.</summary>
    private static SKBitmap ApplyOrigin(SKBitmap src, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.TopLeft or SKEncodedOrigin.Default)
        {
            return src;
        }

        float w = src.Width, h = src.Height;
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

        // x' = a·x + b·y + c ; y' = d·x + e·y + f
        var (a, b, c, d, e, f) = origin switch
        {
            SKEncodedOrigin.TopRight => (-1f, 0f, w, 0f, 1f, 0f),
            SKEncodedOrigin.BottomRight => (-1f, 0f, w, 0f, -1f, h),
            SKEncodedOrigin.BottomLeft => (1f, 0f, 0f, 0f, -1f, h),
            SKEncodedOrigin.LeftTop => (0f, 1f, 0f, 1f, 0f, 0f),
            SKEncodedOrigin.RightTop => (0f, -1f, h, 1f, 0f, 0f),
            SKEncodedOrigin.RightBottom => (0f, -1f, h, -1f, 0f, w),
            SKEncodedOrigin.LeftBottom => (0f, 1f, 0f, -1f, 0f, w),
            _ => (1f, 0f, 0f, 0f, 1f, 0f),
        };

        var dst = new SKBitmap(new SKImageInfo(
            swap ? src.Height : src.Width,
            swap ? src.Width : src.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Premul));
        using var canvas = new SKCanvas(dst);
        canvas.Clear(SKColors.Transparent);
        canvas.SetMatrix(new SKMatrix(a, b, c, d, e, f, 0, 0, 1));
        canvas.DrawBitmap(src, 0, 0);
        return dst;
    }

    private static async Task<byte[]> ReadCappedAsync(Stream input, long maxBytes, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        long total = 0;
        int read;

        while ((read = await input.ReadAsync(chunk.AsMemory(0, chunk.Length), ct)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                throw new UnprocessableException(
                    ImageErrorCodes.FileTooLarge,
                    $"File vượt quá giới hạn {maxBytes} byte.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private enum ImageKind
    {
        Jpeg,
        Png,
        WebP,
        Heic,
        Unsupported,
    }

    private static ImageKind DetectKind(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return ImageKind.Jpeg;
        }

        if (bytes.Length >= 8
            && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
            && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            return ImageKind.Png;
        }

        if (bytes.Length >= 12
            && bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'
            && bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
        {
            return ImageKind.WebP;
        }

        if (bytes.Length >= 12 && IsAscii(bytes, 4, "ftyp"))
        {
            var brand = System.Text.Encoding.ASCII.GetString(bytes, 8, 4);
            if (brand is "heic" or "heix" or "hevc" or "heim" or "heis" or "hevm" or "hevs" or "mif1" or "msf1")
            {
                return ImageKind.Heic;
            }
        }

        return ImageKind.Unsupported;
    }

    private static bool IsAscii(byte[] bytes, int offset, string expected)
    {
        for (var i = 0; i < expected.Length; i++)
        {
            if (bytes[offset + i] != (byte)expected[i])
            {
                return false;
            }
        }

        return true;
    }
}
