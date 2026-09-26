namespace Vsite.Application.Common.Imaging;

/// <summary>Mã lỗi machine-readable ném kèm <see cref="Vsite.Domain.Exceptions.UnprocessableException"/>
/// bởi <see cref="IImageProcessor.LoadAsync"/> — tập trung một chỗ để caller (Media module, sau này
/// Listing/Product) so sánh bằng hằng số thay vì chuỗi tay.</summary>
public static class ImageErrorCodes
{
    public const string FileTooLarge = "MEDIA_FILE_TOO_LARGE";
    public const string UnsupportedFormat = "MEDIA_UNSUPPORTED_FORMAT";
    public const string HeicUnsupported = "MEDIA_HEIC_UNSUPPORTED";
    public const string TooManyPixels = "MEDIA_TOO_MANY_PIXELS";
    public const string CorruptImage = "MEDIA_CORRUPT_IMAGE";
}
