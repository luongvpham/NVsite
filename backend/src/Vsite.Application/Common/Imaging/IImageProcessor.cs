namespace Vsite.Application.Common.Imaging;

/// <summary>
/// Cổng vào duy nhất của pipeline ảnh dùng chung (`Vsite.Infrastructure.Imaging`,
/// Quyết định #53/#84/#85). Module nào cần xử lý ảnh upload (Media, sau này Listing/Product) gọi
/// qua interface này — không tự viết lại logic ImageSharp ở module riêng.
/// </summary>
public interface IImageProcessor
{
    /// <summary>
    /// Thứ tự cố định, không đổi: kiểm dung lượng (size) → magic bytes (JPEG/PNG/WebP; HEIC → lỗi
    /// riêng) → tổng pixel đọc từ header (<c>Image.IdentifyAsync</c>, CHƯA decode) → decode →
    /// AutoOrient (áp dụng EXIF Orientation rồi mới xoá) → xoá toàn bộ metadata (EXIF/ICC/XMP/IPTC).
    /// Ném <see cref="Vsite.Domain.Exceptions.UnprocessableException"/> (422) nếu bất kỳ bước nào
    /// thất bại — xem mã lỗi ở `ImageErrorCodes`.
    /// </summary>
    Task<ISourceImage> LoadAsync(Stream input, CancellationToken ct);
}

/// <summary>Ảnh nguồn đã qua <see cref="IImageProcessor.LoadAsync"/> — sẵn sàng render nhiều biến
/// thể (`ImageTransform`) mà không phải decode lại từ đầu.</summary>
public interface ISourceImage : IDisposable
{
    /// <summary>Kích thước gốc SAU AutoOrient (đúng chiều người dùng nhìn thấy).</summary>
    int Width { get; }
    int Height { get; }

    /// <summary>Render một biến thể theo <paramref name="transform"/>, luôn xuất WebP
    /// (Quyết định #53). Không bao giờ upscale — xem từng `ImageTransform` để biết luật cụ thể.</summary>
    Task<EncodedImage> RenderAsync(ImageTransform transform, CancellationToken ct);
}
