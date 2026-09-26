using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Vsite.Application.Common.Imaging;
using Vsite.Domain.Exceptions;
using Vsite.Infrastructure.Imaging;

namespace Vsite.IntegrationTests.Imaging;

/// <summary>
/// Test cho <see cref="ImageSharpImageProcessor"/> (T1, MEDIA-001, Quyết định #53/#84/#85). Không
/// cần container — gọi thẳng processor với ảnh trong bộ nhớ hoặc ảnh mẫu thật ở
/// `TestAssets/` (EXIF/multi-frame phải là file thật, không sinh được lúc chạy test).
/// </summary>
public sealed class ImageSharpImageProcessorTests
{
    private static readonly string AssetsDir = Path.Combine(AppContext.BaseDirectory, "TestAssets");

    private static ImageSharpImageProcessor CreateProcessor(ImageUploadOptions? options = null)
        => new(Options.Create(options ?? new ImageUploadOptions()));

    private static byte[] ReadAsset(string fileName) => File.ReadAllBytes(Path.Combine(AssetsDir, fileName));

    private static byte[] EncodeJpeg(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        image.Mutate(x => x.Fill(Color.SeaGreen));
        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms);
        return ms.ToArray();
    }

    /// <summary>JPEG nhiễu ngẫu nhiên — ảnh phẳng một màu nén xuống còn vài trăm byte, không đủ để
    /// test giới hạn dung lượng nhỏ (<see cref="Over_max_bytes_is_rejected"/>).</summary>
    private static byte[] EncodeNoisyJpeg(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        var rng = new Random(42);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    row[x] = new Rgba32((byte)rng.Next(256), (byte)rng.Next(256), (byte)rng.Next(256));
                }
            }
        });
        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms);
        return ms.ToArray();
    }

    private static byte[] EncodePng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        image.Mutate(x => x.Fill(Color.SeaGreen));
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    /// <summary>Nửa trái đỏ, nửa phải xanh — dùng cho <see cref="Focal_point_shifts_crop"/>.</summary>
    private static byte[] EncodeSplitColorPng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        image.Mutate(x =>
        {
            x.Fill(Color.Red, new Rectangle(0, 0, width / 2, height));
            x.Fill(Color.Blue, new Rectangle(width / 2, 0, width - (width / 2), height));
        });
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    // ---- Test bắt buộc 1: magic bytes, không tin extension ----

    [Fact]
    public async Task Jpg_renamed_to_png_is_detected_by_magic_bytes()
    {
        var processor = CreateProcessor();
        var jpgBytes = EncodeJpeg(64, 64);

        using var source = await processor.LoadAsync(new MemoryStream(jpgBytes), CancellationToken.None);

        Assert.Equal(64, source.Width);
        Assert.Equal(64, source.Height);
    }

    [Fact]
    public async Task Garbage_bytes_named_png_is_rejected()
    {
        var processor = CreateProcessor();
        var garbage = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => processor.LoadAsync(new MemoryStream(garbage), CancellationToken.None));

        Assert.Equal(ImageErrorCodes.UnsupportedFormat, ex.ErrorCode);
    }

    // ---- Test bắt buộc 2: xoá EXIF (bao gồm GPS) ----

    [Fact]
    public async Task Exif_gps_is_stripped()
    {
        var processor = CreateProcessor();
        var bytes = ReadAsset("exif-gps.jpg");

        using var source = await processor.LoadAsync(new MemoryStream(bytes), CancellationToken.None);
        var encoded = await source.RenderAsync(new ImageTransform.LongEdge(1600), CancellationToken.None);

        var info = Image.Identify(encoded.Bytes);
        Assert.Null(info.Metadata.ExifProfile);
    }

    // ---- R2: AutoOrient trước khi xoá EXIF ----

    [Fact]
    public async Task Orientation_is_applied_before_strip()
    {
        var processor = CreateProcessor();
        // Lưu ngang 400x300, Orientation=6 → hiển thị đúng phải đứng (Height > Width).
        var bytes = ReadAsset("rotated-6.jpg");

        using var source = await processor.LoadAsync(new MemoryStream(bytes), CancellationToken.None);

        Assert.True(source.Height > source.Width, $"Kỳ vọng ảnh đứng sau AutoOrient, thực tế {source.Width}x{source.Height}.");
    }

    // ---- HEIC bị từ chối với mã lỗi riêng ----

    [Fact]
    public async Task Heic_is_rejected_with_specific_code()
    {
        var processor = CreateProcessor();
        var bytes = ReadAsset("sample.heic");

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => processor.LoadAsync(new MemoryStream(bytes), CancellationToken.None));

        Assert.Equal(ImageErrorCodes.HeicUnsupported, ex.ErrorCode);
    }

    // ---- GIF bị từ chối ----

    [Fact]
    public async Task Gif_is_rejected()
    {
        var processor = CreateProcessor();
        var bytes = ReadAsset("animated.gif");

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => processor.LoadAsync(new MemoryStream(bytes), CancellationToken.None));

        Assert.Equal(ImageErrorCodes.UnsupportedFormat, ex.ErrorCode);
    }

    // ---- R5: WebP động chỉ lấy khung đầu ----

    [Fact]
    public async Task Animated_webp_takes_first_frame()
    {
        var processor = CreateProcessor();
        var bytes = ReadAsset("animated.webp");

        using var source = await processor.LoadAsync(new MemoryStream(bytes), CancellationToken.None);
        var encoded = await source.RenderAsync(new ImageTransform.LongEdge(1600), CancellationToken.None);

        using var output = Image.Load(encoded.Bytes);
        Assert.Equal(1, output.Frames.Count);
    }

    /// <summary>
    /// Chứng minh `DecoderOptions.MaxFrames = 1` thực sự chặn decode ở TẦNG DECODER, không phải chỉ
    /// xoá frame thừa SAU khi đã decode hết (khác với <see cref="Animated_webp_takes_first_frame"/>,
    /// vốn pass dù có hay không `MaxFrames`). `animated-frame2-corrupt.webp` có frame 1 hợp lệ và
    /// payload VP8 của frame 2 bị phá (74 byte cuối file ghi đè 0xFF) — nếu decoder đọc frame 2 thì
    /// bắt buộc lỗi. `LoadAsync` phải thành công vì không bao giờ chạm tới byte của frame 2.
    /// </summary>
    [Fact]
    public async Task Animated_webp_decode_never_touches_frame_2()
    {
        var processor = CreateProcessor();
        var bytes = ReadAsset("animated-frame2-corrupt.webp");

        using var source = await processor.LoadAsync(new MemoryStream(bytes), CancellationToken.None);

        Assert.Equal(64, source.Width);
        Assert.Equal(64, source.Height);
    }

    // ---- Huỷ request không được báo thành ảnh hỏng ----

    [Fact]
    public async Task Cancelled_token_propagates_as_operation_canceled_not_corrupt_image()
    {
        var processor = CreateProcessor();
        var bytes = ReadAsset("exif-gps.jpg");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => processor.LoadAsync(new MemoryStream(bytes), cts.Token));
    }

    // ---- Giới hạn dung lượng ----

    [Fact]
    public async Task Over_max_bytes_is_rejected()
    {
        var processor = CreateProcessor(new ImageUploadOptions { MaxBytes = 1024 });
        var bytes = EncodeNoisyJpeg(64, 64); // nhiễu ngẫu nhiên → nén ra chắc chắn > 1024 byte

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => processor.LoadAsync(new MemoryStream(bytes), CancellationToken.None));

        Assert.Equal(ImageErrorCodes.FileTooLarge, ex.ErrorCode);
    }

    // ---- Giới hạn pixel, kiểm TRƯỚC decode ----

    [Fact]
    public async Task Over_max_pixels_is_rejected_before_decode()
    {
        var processor = CreateProcessor(new ImageUploadOptions { MaxPixels = 100 });
        var bytes = EncodeJpeg(64, 64); // 4096 pixel > 100

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => processor.LoadAsync(new MemoryStream(bytes), CancellationToken.None));

        Assert.Equal(ImageErrorCodes.TooManyPixels, ex.ErrorCode);
    }

    // ---- R3: Cover crop, không upscale ----

    [Fact]
    public async Task Cover_crops_to_exact_size()
    {
        var processor = CreateProcessor();
        var bytes = EncodeJpeg(2000, 1500);
        using var source = await processor.LoadAsync(new MemoryStream(bytes), CancellationToken.None);

        var encoded = await source.RenderAsync(new ImageTransform.Cover(1600, 900, FocalPoint.Center), CancellationToken.None);

        Assert.Equal(1600, encoded.Width);
        Assert.Equal(900, encoded.Height);
    }

    [Fact]
    public async Task Cover_never_upscales()
    {
        var processor = CreateProcessor();
        var bytes = EncodeJpeg(400, 300);
        using var source = await processor.LoadAsync(new MemoryStream(bytes), CancellationToken.None);

        var encoded = await source.RenderAsync(new ImageTransform.Cover(1600, 900, FocalPoint.Center), CancellationToken.None);

        Assert.Equal(400, encoded.Width);
        Assert.Equal(225, encoded.Height);
    }

    [Fact]
    public async Task Inside_keeps_ratio()
    {
        var processor = CreateProcessor();
        var bytes = EncodeJpeg(2000, 500);
        using var source = await processor.LoadAsync(new MemoryStream(bytes), CancellationToken.None);

        var encoded = await source.RenderAsync(new ImageTransform.Inside(320, 96), CancellationToken.None);

        Assert.Equal(320, encoded.Width);
        Assert.Equal(80, encoded.Height);
    }

    [Fact]
    public async Task LongEdge_caps_at_1600()
    {
        var processor = CreateProcessor();
        var bytes = EncodeJpeg(3200, 1600);
        using var source = await processor.LoadAsync(new MemoryStream(bytes), CancellationToken.None);

        var encoded = await source.RenderAsync(new ImageTransform.LongEdge(1600), CancellationToken.None);

        Assert.Equal(1600, encoded.Width);
        Assert.Equal(800, encoded.Height);
    }

    [Fact]
    public async Task Focal_point_shifts_crop()
    {
        var processor = CreateProcessor();
        // 200x100 — nửa trái đỏ, nửa phải xanh.
        var bytes = EncodeSplitColorPng(200, 100);
        using var source = await processor.LoadAsync(new MemoryStream(bytes), CancellationToken.None);

        // Focal lệch hẳn sang phải (0.9) → crop 100x100 phải rơi vào nửa phải (xanh).
        var encoded = await source.RenderAsync(new ImageTransform.Cover(100, 100, new FocalPoint(0.9f, 0.5f)), CancellationToken.None);

        using var output = Image.Load<Rgba32>(encoded.Bytes);
        var centerPixel = output[output.Width / 2, output.Height / 2];

        Assert.True(centerPixel.B > centerPixel.R, $"Kỳ vọng pixel giữa thiên về xanh, thực tế R={centerPixel.R} G={centerPixel.G} B={centerPixel.B}.");
    }

    // ---- R5: PNG alpha giữ nguyên khi xuất WebP ----

    [Fact]
    public async Task Png_alpha_is_preserved()
    {
        var processor = CreateProcessor();
        var bytes = ReadAsset("alpha.png");
        using var source = await processor.LoadAsync(new MemoryStream(bytes), CancellationToken.None);

        var encoded = await source.RenderAsync(new ImageTransform.LongEdge(1600), CancellationToken.None);

        using var output = Image.Load<Rgba32>(encoded.Bytes);
        var transparentPixel = output[output.Width - 1, output.Height / 2];

        Assert.True(transparentPixel.A < 50, $"Kỳ vọng vùng trong suốt còn alpha thấp, thực tế A={transparentPixel.A}.");
    }

    [Fact]
    public async Task Output_is_webp()
    {
        var processor = CreateProcessor();
        var bytes = EncodePng(64, 64);
        using var source = await processor.LoadAsync(new MemoryStream(bytes), CancellationToken.None);

        var encoded = await source.RenderAsync(new ImageTransform.LongEdge(1600), CancellationToken.None);

        Assert.Equal("image/webp", encoded.MimeType);
        Assert.True(encoded.Bytes.Length > 12);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(encoded.Bytes, 0, 4));
        Assert.Equal("WEBP", System.Text.Encoding.ASCII.GetString(encoded.Bytes, 8, 4));
    }
}
