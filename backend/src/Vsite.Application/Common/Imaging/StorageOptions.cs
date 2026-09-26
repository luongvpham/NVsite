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
    /// MinIO / S3-compatible endpoint. Null → dùng endpoint AWS S3 mặc định theo region.
    /// Nếu trỏ vào MinIO: bản server PHẢI hỗ trợ conditional write (<c>If-None-Match</c>), thêm ở
    /// MinIO ngay sau <c>RELEASE.2023-01-31</c> (PR minio/minio#16551, merge 2023-02-07) — dùng bản
    /// <c>RELEASE.2023-02-xx</c> trở lên. <c>RELEASE.2023-01-31</c> (default cũ của
    /// <c>Testcontainers.Minio</c>) im lặng bỏ qua <c>IfNoneMatch</c> và sẽ làm no-overwrite (#75)
    /// không hoạt động — xem <c>S3ObjectStorageTests</c> để biết bản đang pin cho test.
    /// </summary>
    public string? ServiceUrl { get; init; }

    public string Region { get; init; } = "ap-southeast-1";

    public bool ForcePathStyle { get; init; }

    /// <summary>Null → dùng credential chain mặc định của AWS SDK.</summary>
    public string? AccessKey { get; init; }

    public string? SecretKey { get; init; }
}
