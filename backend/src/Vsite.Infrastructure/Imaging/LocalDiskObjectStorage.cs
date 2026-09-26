using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Vsite.Application.Common.Imaging;

namespace Vsite.Infrastructure.Imaging;

/// <summary>
/// Implementation <see cref="IObjectStorage"/> ghi thẳng lên đĩa cục bộ (T2, MEDIA-001, Quyết định
/// #83) — provider mặc định, dùng cho dev/self-host nhỏ. Content type lưu ở file sidecar
/// <c>{key}.contenttype</c> vì filesystem thô không có object metadata như S3.
/// </summary>
public sealed class LocalDiskObjectStorage : IObjectStorage
{
    private const string ContentTypeSuffix = ".contenttype";

    private readonly string _root;

    public LocalDiskObjectStorage(IOptions<StorageOptions> options, IHostEnvironment environment)
    {
        _root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.Value.LocalDiskRoot));
    }

    public async Task PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken ct)
    {
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        try
        {
            // FileMode.CreateNew: atomic — nếu file đã tồn tại, IOException, không ghi đè (#75).
            await using var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await fs.WriteAsync(content, ct);
        }
        catch (IOException) when (File.Exists(path))
        {
            throw new ObjectAlreadyExistsException(key);
        }

        await File.WriteAllTextAsync(ContentTypePath(path), contentType, ct);
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

        var contentTypePath = ContentTypePath(path);
        var contentType = File.Exists(contentTypePath)
            ? File.ReadAllText(contentTypePath)
            : "application/octet-stream";

        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<StoredObject?>(new StoredObject(stream, contentType, stream.Length));
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var path = ResolvePath(key);
        File.Delete(path);
        File.Delete(ContentTypePath(path));
        return Task.CompletedTask;
    }

    private static string ContentTypePath(string path) => path + ContentTypeSuffix;

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
