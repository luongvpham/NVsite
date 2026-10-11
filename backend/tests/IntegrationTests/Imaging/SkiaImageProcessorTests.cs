using Microsoft.Extensions.Options;
using Vsite.Application.Common.Imaging;
using Vsite.Domain.Exceptions;
using Vsite.Infrastructure.Imaging;

namespace Vsite.IntegrationTests.Imaging;

/// <summary>
/// Test cho <see cref="SkiaImageProcessor"/> (T1, MEDIA-001, Quyết định #53/#84/#85). Không
/// cần container — gọi thẳng processor với ảnh trong bộ nhớ hoặc ảnh mẫu thật ở
/// `TestAssets/` (EXIF/multi-frame phải là file thật, không sinh được lúc chạy test).
/// </summary>
public sealed class SkiaImageProcessorTests
{
    private static readonly string AssetsDir = Path.Combine(AppContext.BaseDirectory, "TestAssets");

    private static SkiaImageProcessor CreateProcessor(ImageUploadOptions? options = null)
        => new(Options.Create(options ?? new ImageUploadOptions()));

    private static byte[] ReadAsset(string fileName) => File.ReadAllBytes(Path.Combine(AssetsDir, fileName));

    private static byte[] EncodeJpeg(int width, int height) => TestImages.Jpeg(width, height);

    private static byte[] EncodeNoisyJpeg(int width, int height) => TestImages.NoisyJpeg(width, height);

    private static byte[] EncodePng(int width, int height) => TestImages.Png(width, height);

    private static byte[] EncodeSplitColorPng(int width, int height) => TestImages.SplitColorPng(width, height);

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

        Assert.False(TestImages.WebpHasMetadataChunk(encoded.Bytes), "WebP đầu ra không được có chunk EXIF/XMP/ICCP.");
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

        Assert.True(TestImages.FrameCount(encoded.Bytes) <= 1, "WebP đầu ra phải tĩnh (Skia báo 0 hoặc 1 khung cho ảnh tĩnh).");
    }

    /// <summary>
    /// Khác ImageSharp (lấy được khung 1 rồi bỏ qua khung 2 hỏng): demuxer WebP của Skia kiểm tra
    /// header MỌI khung ngay lúc `SKCodec.Create`, nên một file WebP động có khung sau bị phá bị từ
    /// chối sớm là `CorruptImage` (422) — không bao giờ tới bước decode. Chấp nhận được: đây là file
    /// hỏng, và từ chối sớm còn an toàn hơn. (`animated-frame2-corrupt.webp`: khung 1 hợp lệ, 74 byte
    /// cuối — payload VP8 của khung 2 — bị ghi đè 0xFF.)
    /// </summary>
    [Fact]
    public async Task Animated_webp_with_corrupt_later_frame_is_rejected_as_corrupt()
    {
        var processor = CreateProcessor();
        var bytes = ReadAsset("animated-frame2-corrupt.webp");

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => processor.LoadAsync(new MemoryStream(bytes), CancellationToken.None));

        Assert.Equal(ImageErrorCodes.CorruptImage, ex.ErrorCode);
    }

    // ---- Huỷ request không được báo thành ảnh hỏng ----

    /// <summary>
    /// Cancel NGAY TỪ ĐẦU sẽ ném <see cref="OperationCanceledException"/> ngay trong
    /// `ReadCappedAsync` (bước 1) — nơi KHÔNG có try/catch và chưa từng có bug. Test đó sẽ pass dù
    /// có xoá cả hai khối `catch (OperationCanceledException)` đã fix, nên không chứng minh được gì.
    /// Stream này chỉ cancel token SAU KHI `ReadCappedAsync` đọc hết input (lần `ReadAsync` trả về
    /// 0 — EOF) — token bị huỷ đúng lúc thực thi đi vào bước 3 (`SKCodec.Create`), là khối
    /// try/catch thật sự được sửa.
    /// </summary>
    private sealed class CancelOnEofStream(Stream inner, CancellationTokenSource cts) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadAsync(buffer, CancellationToken.None);
            if (read == 0)
            {
                await cts.CancelAsync();
            }

            return read;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    [Fact]
    public async Task Cancelled_after_read_propagates_as_operation_canceled_not_corrupt_image()
    {
        var processor = CreateProcessor();
        var bytes = ReadAsset("exif-gps.jpg");
        using var cts = new CancellationTokenSource();
        using var stream = new CancelOnEofStream(new MemoryStream(bytes), cts);

        // Token còn sống lúc gọi — chỉ bị cts.Cancel() từ BÊN TRONG stream, đúng lúc
        // `ReadCappedAsync` đọc xong (EOF), TRƯỚC khi chạm `SKCodec.Create`.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => processor.LoadAsync(stream, cts.Token));
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

    [Theory]
    [InlineData(1, 400, 1600, 600)] // 1px rộng + preset ngang 2.67:1 → cropHeight làm tròn ra 0 (trước fix: 500)
    [InlineData(400, 1, 400, 1000)] // 1px cao + preset dọc 2:5 → cropWidth làm tròn ra 0 (trước fix: 500)
    [InlineData(1, 400, 800, 1000)]
    [InlineData(400, 1, 1600, 900)]
    [InlineData(1, 1, 96, 96)]
    public async Task Cover_on_one_pixel_image_does_not_throw_and_yields_valid_image(int w, int h, int targetW, int targetH)
    {
        using var source = await CreateProcessor().LoadAsync(new MemoryStream(EncodePng(w, h)), CancellationToken.None);

        var encoded = await source.RenderAsync(new ImageTransform.Cover(targetW, targetH, FocalPoint.Center), CancellationToken.None);

        Assert.True(encoded.Width >= 1 && encoded.Height >= 1);
        Assert.True(encoded.Width <= targetW && encoded.Height <= targetH);
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

        var centerPixel = TestImages.CenterPixel(encoded.Bytes);

        Assert.True(centerPixel.Blue > centerPixel.Red, $"Kỳ vọng pixel giữa thiên về xanh, thực tế R={centerPixel.Red} G={centerPixel.Green} B={centerPixel.Blue}.");
    }

    // ---- R5: PNG alpha giữ nguyên khi xuất WebP ----

    [Fact]
    public async Task Png_alpha_is_preserved()
    {
        var processor = CreateProcessor();
        var bytes = ReadAsset("alpha.png");
        using var source = await processor.LoadAsync(new MemoryStream(bytes), CancellationToken.None);

        var encoded = await source.RenderAsync(new ImageTransform.LongEdge(1600), CancellationToken.None);

        var transparentPixel = TestImages.RightEdgePixel(encoded.Bytes);

        Assert.True(transparentPixel.Alpha < 50, $"Kỳ vọng vùng trong suốt còn alpha thấp, thực tế A={transparentPixel.Alpha}.");
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
