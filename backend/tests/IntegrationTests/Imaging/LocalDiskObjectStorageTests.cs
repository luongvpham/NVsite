using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Vsite.Application.Common.Imaging;
using Vsite.Infrastructure.Imaging;

namespace Vsite.IntegrationTests.Imaging;

/// <summary>
/// Contract test cho <see cref="LocalDiskObjectStorage"/> (T2, MEDIA-001) — thư mục temp thật trên
/// đĩa, KHÔNG cần Docker.
/// </summary>
public sealed class LocalDiskObjectStorageTests : ObjectStorageContractTests
{
    private string _tempRoot = null!;

    protected override Task<IObjectStorage> CreateStorageAsync()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "vsite-media-test-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempRoot);

        // LocalDiskRoot tuyệt đối ⇒ Path.Combine(ContentRootPath, LocalDiskRoot) trả thẳng
        // LocalDiskRoot (hành vi chuẩn của Path.Combine khi vế sau đã rooted) — ContentRootPath của
        // fake environment không quan trọng trong test này.
        var options = Options.Create(new StorageOptions { LocalDiskRoot = _tempRoot });
        IObjectStorage storage = new LocalDiskObjectStorage(options, new FakeHostEnvironment());

        return Task.FromResult(storage);
    }

    protected override Task<int> CountStoredObjectsAsync()
        => Task.FromResult(Directory.Exists(_tempRoot)
            ? Directory.GetFiles(_tempRoot, "*", SearchOption.AllDirectories).Length
            : 0);

    public override Task DisposeAsync()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }

        return Task.CompletedTask;
    }

    /// <summary>Fake tối thiểu — <see cref="LocalDiskObjectStorage"/> chỉ đọc
    /// <see cref="ContentRootPath"/>; các member khác không được chạm tới trong test này.</summary>
    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "Vsite.IntegrationTests";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public string EnvironmentName { get; set; } = "Test";
    }
}
