using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Vsite.Application.Common.Imaging;
using Vsite.Domain.Exceptions;

namespace Vsite.Infrastructure.Imaging;

/// <summary>
/// Implementation duy nhất của <see cref="IImageProcessor"/>, dựa trên SixLabors.ImageSharp 3.x
/// (Quyết định #53/#84/#85). Thứ tự xử lý CỐ ĐỊNH trong <see cref="LoadAsync"/> — đừng đổi thứ tự
/// khi sửa: dung lượng → magic bytes → pixel từ header (chưa decode) → decode → AutoOrient → xoá
/// metadata. Đảo AutoOrient và xoá metadata (R2) làm ảnh chụp dọc bị lưu nằm ngang.
/// </summary>
public sealed class ImageSharpImageProcessor(IOptions<ImageUploadOptions> options) : IImageProcessor
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

        // 3) Pixel từ header — Image.IdentifyAsync KHÔNG decode toàn bộ ảnh, tránh tốn CPU/RAM giải
        // mã một ảnh sẽ bị từ chối ngay sau đó vì quá nhiều pixel (bomb ảnh nén nhỏ, pixel khổng lồ).
        ImageInfo info;
        try
        {
            using var identifyStream = new MemoryStream(bytes, writable: false);
            info = await Image.IdentifyAsync(identifyStream, ct)
                ?? throw new UnprocessableException(ImageErrorCodes.CorruptImage, "Không đọc được thông tin ảnh.");
        }
        catch (UnprocessableException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            // Client huỷ request — không phải ảnh hỏng, phải để nguyên cho caller xử lý (vd. 499),
            // không nuốt thành 422.
            throw;
        }
        catch (Exception)
        {
            throw new UnprocessableException(ImageErrorCodes.CorruptImage, "Không đọc được thông tin ảnh.");
        }

        var pixels = (long)info.Width * info.Height;
        if (pixels > _options.MaxPixels)
        {
            throw new UnprocessableException(
                ImageErrorCodes.TooManyPixels,
                $"Ảnh có {pixels} pixel, vượt quá giới hạn {_options.MaxPixels}.");
        }

        // 4) Decode thật. `MaxFrames = 1` chặn decoder giải mã hết mọi frame của ảnh động NGAY TỪ
        // LÚC DECODE — nếu không, một WebP/GIF vài MB nhưng hàng nghìn frame rẻ tiền trên canvas
        // ~25MP (vẫn lọt qua kiểm pixel ở bước 3, vì IdentifyAsync chỉ đọc kích thước canvas của MỘT
        // frame) sẽ ép ImageSharp cấp phát hàng GB RAM trước khi vòng lặp xoá frame thừa bên dưới
        // kịp chạy. Giữ lại vòng lặp `RemoveFrame` làm lớp phòng thủ thứ hai (phòng khi decoder của
        // một format nào đó không tôn trọng `MaxFrames`).
        Image<Rgba32> image;
        try
        {
            using var decodeStream = new MemoryStream(bytes, writable: false);
            var decoderOptions = new DecoderOptions { MaxFrames = 1 };
            image = await Image.LoadAsync<Rgba32>(decoderOptions, decodeStream, ct);
        }
        catch (UnknownImageFormatException)
        {
            throw new UnprocessableException(ImageErrorCodes.UnsupportedFormat, "Định dạng ảnh không được hỗ trợ.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new UnprocessableException(ImageErrorCodes.CorruptImage, "Dữ liệu ảnh bị hỏng, không decode được.");
        }

        // R5: WebP/GIF động → chỉ lấy khung đầu tiên (fallback phòng thủ — `MaxFrames = 1` ở trên
        // đã đảm bảo việc này ngay từ lúc decode).
        while (image.Frames.Count > 1)
        {
            image.Frames.RemoveFrame(1);
        }

        // 5) AutoOrient TRƯỚC — áp dụng EXIF Orientation để ảnh đứng đúng chiều, rồi mới xoá metadata
        // (R2). Xoá metadata trước thì mất thông tin xoay, ảnh chụp dọc bị lưu nằm ngang.
        image.Mutate(x => x.AutoOrient());

        // 6) Xoá toàn bộ metadata (EXIF/ICC/XMP/IPTC) — không rò rỉ GPS hay thông tin thiết bị.
        image.Metadata.ExifProfile = null;
        image.Metadata.IccProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;

        return new SourceImage(image, _options);
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
