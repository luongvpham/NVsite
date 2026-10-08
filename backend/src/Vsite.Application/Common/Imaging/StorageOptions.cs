namespace Vsite.Application.Common.Imaging;

/// <summary>
/// Cấu hình <see cref="Vsite.Application.Common.Imaging.IObjectStorage"/> (T2, MEDIA-001, Quyết
/// định #83). Đọc từ config section <see cref="Section"/>, đăng ký qua
/// <c>services.Configure&lt;StorageOptions&gt;</c> trong <c>AddInfrastructure()</c>.
/// </summary>
public sealed class StorageOptions
{
    public const string Section = "Storage";

    public StorageProvider Provider { get; init; } = StorageProvider.LocalDisk;

    /// <summary>Tương đối với ContentRoot (backend/src/Vsite.Api) → <c>&lt;repo&gt;/.media</c>.</summary>
    public string LocalDiskRoot { get; init; } = "../../../.media";

    public S3StorageOptions S3 { get; init; } = new();
}

public enum StorageProvider
{
    LocalDisk,
    S3,
}

public sealed class S3StorageOptions
{
    public string BucketName { get; init; } = "";

    /// <summary>
    /// Endpoint S3-compatible tự host (MinIO, LocalStack, …). Null → dùng endpoint AWS S3 mặc định
    /// theo region.
    ///
    /// ⚠️ Bất kể server nào: PHẢI hỗ trợ conditional write qua header <c>If-None-Match: *</c> — đây
    /// là cơ chế no-overwrite (#75) của <see cref="S3StorageOptions"/>, <c>PutAsync</c> ném
    /// <c>ObjectAlreadyExistsException</c> khi server trả 412. MinIO hỗ trợ từ ngay sau
    /// <c>RELEASE.2023-01-31</c> (PR minio/minio#16551) — bản <c>RELEASE.2023-01-31</c> im lặng bỏ
    /// qua <c>IfNoneMatch</c>. Test pin/xác nhận thật (2026-09-27, MEDIA-001): minio/minio không còn
    /// publish image public (Docker Hub và quay.io đều từ chối pull) — contract test
    /// (<c>S3ObjectStorageTests</c>, module <c>backend/tests/IntegrationTests/Imaging/</c>) hiện
    /// chạy qua <c>localstack/localstack:4.0.3</c> (Testcontainers.LocalStack), đã verify hỗ trợ
    /// conditional write đúng. Đổi provider/pin bản khác → chạy lại đúng bộ test này để xác nhận
    /// trước khi trỏ production vào.
    /// </summary>
    public string? ServiceUrl { get; init; }

    public string Region { get; init; } = "ap-southeast-1";

    public bool ForcePathStyle { get; init; }

    /// <summary>Null → dùng credential chain mặc định của AWS SDK.</summary>
    public string? AccessKey { get; init; }

    public string? SecretKey { get; init; }
}
