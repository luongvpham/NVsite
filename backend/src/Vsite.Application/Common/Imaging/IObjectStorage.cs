namespace Vsite.Application.Common.Imaging;

/// <summary>
/// Cổng vào duy nhất để đọc/ghi object (ảnh) trên storage — LocalDisk hoặc S3 tuỳ
/// <see cref="StorageOptions.Provider"/> (T2, MEDIA-001, Quyết định #83). Mọi key đều được validate
/// qua <see cref="ImagePaths.ValidateKey"/> trước khi chạm backend thật.
/// </summary>
public interface IObjectStorage
{
    /// <summary>Không ghi đè (Quyết định #75): key đã tồn tại → <see cref="ObjectAlreadyExistsException"/>.</summary>
    Task PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken ct);

    Task<bool> ExistsAsync(string key, CancellationToken ct);

    /// <summary>Null nếu key không tồn tại.</summary>
    Task<StoredObject?> OpenReadAsync(string key, CancellationToken ct);

    /// <summary>Chỉ dùng cho rollback best-effort (R4) và soft-delete không đụng file. Bước 4 không
    /// gọi từ luồng nghiệp vụ.</summary>
    Task DeleteAsync(string key, CancellationToken ct);
}

public sealed record StoredObject(Stream Content, string ContentType, long Length);
