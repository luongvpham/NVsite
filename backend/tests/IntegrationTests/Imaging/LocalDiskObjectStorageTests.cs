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

    /// <summary>
    /// Regression cho bug sidecar đã bỏ (review sau T2): <c>shops/.../a.webp.contenttype</c> là một
    /// key HỢP LỆ độc lập, không phải file phụ của <c>shops/.../a.webp</c>. Bản có sidecar sẽ ghi đè
    /// lặng lẽ (nếu put webp trước) hoặc ném false-positive <see cref="ObjectAlreadyExistsException"/>
    /// (nếu put ngược) tuỳ thứ tự — test cả hai thứ tự.
    /// </summary>
    [Fact]
    public async Task Webp_key_and_key_shaped_like_old_sidecar_stay_independent_when_webp_put_first()
    {
        const string webpKey = "shops/s1/library/a.webp";
        const string sidecarShapedKey = "shops/s1/library/a.webp.contenttype";
        byte[] webpContent = [1, 2, 3];
        byte[] otherContent = [9, 9];

        await Storage.PutAsync(webpKey, webpContent, "image/webp", CancellationToken.None);
        await Storage.PutAsync(sidecarShapedKey, otherContent, "application/octet-stream", CancellationToken.None);

        await AssertIndependentContent(webpKey, webpContent, "image/webp");
        await AssertIndependentContent(sidecarShapedKey, otherContent, "application/octet-stream");
    }

    [Fact]
    public async Task Webp_key_and_key_shaped_like_old_sidecar_stay_independent_when_sidecar_shaped_put_first()
    {
        const string webpKey = "shops/s1/library/b.webp";
        const string sidecarShapedKey = "shops/s1/library/b.webp.contenttype";
        byte[] webpContent = [4, 5, 6];
        byte[] otherContent = [7, 7];

        await Storage.PutAsync(sidecarShapedKey, otherContent, "application/octet-stream", CancellationToken.None);
        await Storage.PutAsync(webpKey, webpContent, "image/webp", CancellationToken.None);

        await AssertIndependentContent(webpKey, webpContent, "image/webp");
        await AssertIndependentContent(sidecarShapedKey, otherContent, "application/octet-stream");
    }

    private async Task AssertIndependentContent(string key, byte[] expectedContent, string expectedContentType)
    {
        var result = await Storage.OpenReadAsync(key, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(expectedContentType, result!.ContentType);

        using var ms = new MemoryStream();
        await result.Content.CopyToAsync(ms);
        await result.Content.DisposeAsync();
        Assert.Equal(expectedContent, ms.ToArray());
    }

    /// <summary>
    /// Review sau T2: write phải qua file tạm rồi <c>File.Move</c>, không ghi thẳng vào key thật —
    /// cancel giữa chừng không được để lại object thiếu ở key, không được để lại rác trong
    /// <c>.tmp/</c>, và key phải "sạch" (put lại thành công) sau đó.
    /// </summary>
    [Fact]
    public async Task Cancelled_put_leaves_no_partial_object_and_no_temp_file_and_key_stays_puttable()
    {
        const string key = "shops/s1/library/cancelled-put.webp";
        byte[] content = [1, 2, 3, 4, 5];
        var cancelledToken = new CancellationToken(canceled: true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Storage.PutAsync(key, content, "image/webp", cancelledToken));

        Assert.False(await Storage.ExistsAsync(key, CancellationToken.None));
        Assert.Equal(0, await CountStoredObjectsAsync());

        // Key không bị "poison" bởi lần put lỗi — put lại (token bình thường) phải thành công.
        await Storage.PutAsync(key, content, "image/webp", CancellationToken.None);
        Assert.True(await Storage.ExistsAsync(key, CancellationToken.None));
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
