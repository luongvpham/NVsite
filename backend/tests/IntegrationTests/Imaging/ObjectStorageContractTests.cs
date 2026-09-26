using Vsite.Application.Common.Imaging;

namespace Vsite.IntegrationTests.Imaging;

/// <summary>
/// Contract dùng chung cho mọi <see cref="IObjectStorage"/> (T2, MEDIA-001, Quyết định #75/#83) —
/// mỗi provider (LocalDisk, S3) phải thoả cùng bộ hành vi này. Lớp con set up backend cụ thể qua
/// <see cref="CreateStorageAsync"/> và cung cấp cách đếm object thật sự đã ghi qua
/// <see cref="CountStoredObjectsAsync"/> (dùng để xác nhận key không hợp lệ không hề chạm đĩa/bucket
/// — không đi qua <see cref="IObjectStorage"/> vì bản thân nó cũng validate key).
/// </summary>
public abstract class ObjectStorageContractTests : IAsyncLifetime
{
    protected IObjectStorage Storage { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Storage = await CreateStorageAsync();
    }

    public virtual Task DisposeAsync() => Task.CompletedTask;

    protected abstract Task<IObjectStorage> CreateStorageAsync();

    protected abstract Task<int> CountStoredObjectsAsync();

    [Fact]
    public async Task Put_then_open_returns_correct_bytes_and_content_type()
    {
        const string key = "shops/s1/library/put-then-open.webp";
        byte[] content = [1, 2, 3, 4, 5];

        await Storage.PutAsync(key, content, "image/webp", CancellationToken.None);

        var result = await Storage.OpenReadAsync(key, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("image/webp", result!.ContentType);
        Assert.Equal(content.Length, result.Length);

        using var ms = new MemoryStream();
        await result.Content.CopyToAsync(ms);
        await result.Content.DisposeAsync();
        Assert.Equal(content, ms.ToArray());
    }

    [Fact]
    public async Task Put_twice_same_key_throws_and_keeps_original_content()
    {
        const string key = "shops/s1/library/put-twice.webp";
        byte[] original = [9, 9, 9];
        byte[] second = [1, 1];

        await Storage.PutAsync(key, original, "image/webp", CancellationToken.None);

        await Assert.ThrowsAsync<ObjectAlreadyExistsException>(
            () => Storage.PutAsync(key, second, "image/webp", CancellationToken.None));

        var result = await Storage.OpenReadAsync(key, CancellationToken.None);
        Assert.NotNull(result);

        using var ms = new MemoryStream();
        await result!.Content.CopyToAsync(ms);
        await result.Content.DisposeAsync();
        Assert.Equal(original, ms.ToArray());
    }

    [Fact]
    public async Task Open_nonexistent_key_returns_null()
    {
        var result = await Storage.OpenReadAsync("shops/s1/library/does-not-exist.webp", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Exists_reflects_put_state()
    {
        const string key = "shops/s1/library/exists-check.webp";

        Assert.False(await Storage.ExistsAsync(key, CancellationToken.None));

        byte[] content = [1, 2, 3];
        await Storage.PutAsync(key, content, "image/webp", CancellationToken.None);

        Assert.True(await Storage.ExistsAsync(key, CancellationToken.None));
    }

    [Theory]
    [InlineData("shops/../x")]
    [InlineData("shops/a/../../etc/passwd")]
    [InlineData("/etc/passwd")]
    [InlineData("shops\\a")]
    [InlineData("other/a")]
    [InlineData("shops//a")]
    public async Task Put_with_invalid_key_throws_and_does_not_touch_backend(string key)
    {
        byte[] content = [1];
        await Assert.ThrowsAsync<ArgumentException>(
            () => Storage.PutAsync(key, content, "image/webp", CancellationToken.None));

        Assert.Equal(0, await CountStoredObjectsAsync());
    }

    /// <summary>
    /// Cần cho rollback best-effort (R4, T5 sẽ gọi <c>DeleteAsync</c> cho các key vừa ghi khi một
    /// bước sau đó trong cùng request fail) — DeleteAsync KHÔNG được ném khi key chưa từng tồn tại,
    /// kể cả khi thư mục cha (LocalDisk) hay object (S3) chưa từng được tạo.
    /// </summary>
    [Fact]
    public async Task Delete_on_never_stored_key_does_not_throw()
    {
        var key = $"shops/s1/library/never-stored-{Guid.NewGuid():N}.webp";

        var exception = await Record.ExceptionAsync(
            () => Storage.DeleteAsync(key, CancellationToken.None));

        Assert.Null(exception);
    }
}
