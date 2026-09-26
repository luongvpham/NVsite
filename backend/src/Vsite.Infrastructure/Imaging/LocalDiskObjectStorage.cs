using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Vsite.Application.Common.Imaging;

namespace Vsite.Infrastructure.Imaging;

/// <summary>
/// Implementation <see cref="IObjectStorage"/> ghi thẳng lên đĩa cục bộ (T2, MEDIA-001, Quyết định
/// #83) — provider mặc định, dùng cho dev/self-host nhỏ.
///
/// KHÔNG dùng file sidecar cho content type (bản đầu của task này có, đã bỏ sau review): một key
/// hợp lệ như <c>shops/a/b.webp.contenttype</c> sẽ trùng tên với sidecar của
/// <c>shops/a/b.webp</c> — vỡ tính bất biến #75 (ghi đè lặng lẽ / false-positive
/// <see cref="ObjectAlreadyExistsException"/> tuỳ thứ tự put) và có thể bị đọc nhầm thành object thật
/// qua `/media/*`. Content type giờ suy ra từ **đuôi file của key** qua bảng cố định nhỏ
/// (<see cref="ContentTypeFromExtension"/>) — không lưu trạng thái phụ nào trên đĩa.
/// </summary>
public sealed class LocalDiskObjectStorage : IObjectStorage
{
    private const string TempDirName = ".tmp";

    private readonly string _root;

    public LocalDiskObjectStorage(IOptions<StorageOptions> options, IHostEnvironment environment)
    {
        _root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.Value.LocalDiskRoot));
    }

    public async Task PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken ct)
    {
        var path = ResolvePath(key);
        ValidateContentType(key, contentType);

        // .tmp/ nằm ngay dưới root, NGOÀI cây "shops/" — mọi key hợp lệ (ImagePaths.ValidateKey) đều
        // bắt đầu bằng "shops/" nên không thể trùng đường dẫn với file tạm ở đây, dù trước hay sau
        // khi file tạm bị xoá.
        var tempDir = Path.Combine(_root, TempDirName);
        Directory.CreateDirectory(tempDir);
        var tempPath = Path.Combine(tempDir, Guid.NewGuid().ToString("N"));

        try
        {
            // FileMode.CreateNew trên file TẠM — không thể va key khác, nên lỗi ở bước này luôn là
            // lỗi ghi thật (disk-full, cancel, ...), KHÔNG BAO GIỜ bị hiểu nhầm thành "đã tồn tại".
            await using (var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await fs.WriteAsync(content, ct);
                await fs.FlushAsync(ct);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            try
            {
                // Move không ghi đè: nếu key đã tồn tại, ném IOException — đây là chỗ DUY NHẤT dịch
                // sang ObjectAlreadyExistsException (#75). Move trong cùng volume là atomic.
                File.Move(tempPath, path, overwrite: false);
            }
            catch (IOException) when (File.Exists(path))
            {
                throw new ObjectAlreadyExistsException(key);
            }
        }
        finally
        {
            // Còn sót lại chỉ khi write/move phía trên fail theo cách khác 409 (disk-full, cancel,
            // crash tiến trình) — dọn best-effort, không để rác vĩnh viễn trong .tmp/.
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct)
    {
        var path = ResolvePath(key);
        return Task.FromResult(File.Exists(path));
    }

    public Task<StoredObject?> OpenReadAsync(string key, CancellationToken ct)
    {
        var path = ResolvePath(key);
        if (!File.Exists(path))
        {
            return Task.FromResult<StoredObject?>(null);
        }

        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<StoredObject?>(new StoredObject(stream, ContentTypeFromExtension(key), stream.Length));
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var path = ResolvePath(key);

        // Idempotent (cần cho rollback best-effort R4/T5): key chưa từng tồn tại — kể cả khi thư mục
        // cha cũng chưa từng được tạo — là no-op, không phải lỗi. File.Delete thẳng sẽ ném
        // DirectoryNotFoundException khi thư mục cha thiếu; kiểm File.Exists trước để tránh.
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <summary>Bảng cố định nhỏ, đủ dùng cho Bước 4 (MimeType sau xử lý luôn là <c>image/webp</c>).
    /// Mở rộng khi có định dạng lưu trữ khác — đừng suy luận content type từ nội dung file.</summary>
    private static string ContentTypeFromExtension(string key)
        => Path.GetExtension(key).Equals(".webp", StringComparison.OrdinalIgnoreCase)
            ? "image/webp"
            : "application/octet-stream";

    /// <summary>Phòng thủ: contentType caller truyền vào phải khớp với những gì
    /// <see cref="OpenReadAsync"/> sẽ trả lại sau này qua <see cref="ContentTypeFromExtension"/> —
    /// không cho lưu một object mà lần đọc lại sẽ thấy content type khác lúc ghi.</summary>
    private static void ValidateContentType(string key, string contentType)
    {
        var expected = ContentTypeFromExtension(key);
        if (!string.Equals(contentType, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"contentType '{contentType}' không khớp với đuôi file của key (mong đợi '{expected}').",
                nameof(contentType));
        }
    }

    /// <summary>Validate key (ImagePaths) rồi kiểm thêm: đường dẫn thật sau khi Combine phải nằm
    /// dưới root — phòng thủ hai lớp, không chỉ tin string check ở ValidateKey.</summary>
    private string ResolvePath(string key)
    {
        ImagePaths.ValidateKey(key);

        var combined = Path.GetFullPath(Path.Combine(_root, key));
        var rootWithSeparator = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;

        if (combined != _root && !combined.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new ArgumentException("Key thoát khỏi storage root.", nameof(key));
        }

        return combined;
    }
}
