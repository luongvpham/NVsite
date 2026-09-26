using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Vsite.Api.Media;
using Vsite.Application.Common.Imaging;
using Vsite.Infrastructure.Imaging;

namespace Vsite.IntegrationTests.Media;

/// <summary>
/// T9, MEDIA-001 (#53, #81, #83) — test <see cref="MediaFileMiddleware"/> trực tiếp qua
/// <see cref="DefaultHttpContext"/> + <see cref="LocalDiskObjectStorage"/> THẬT trên một thư mục
/// temp — KHÔNG cần Docker (không Postgres/Redis, khác <c>MediaFileServingTests</c> chạy qua HTTP
/// thật với <c>MediaApiFactory</c>). Đây là nơi verify R1 (path traversal) không cần Docker.
///
/// Đặt sentinel file NGAY NGOÀI storage root để chứng minh không request traversal nào đọc được ra
/// ngoài root.
/// </summary>
public sealed class MediaFileMiddlewareTests : IAsyncLifetime
{
    private string _storageRoot = null!;
    private string _parentDir = null!;
    private const string SentinelContent = "SENTINEL-SECRET-OUTSIDE-ROOT";
    private LocalDiskObjectStorage _storage = null!;
    private MediaFileMiddleware _middleware = null!;

    public Task InitializeAsync()
    {
        _parentDir = Path.Combine(Path.GetTempPath(), "vsite-media-mw-test-" + Guid.NewGuid());
        _storageRoot = Path.Combine(_parentDir, "storage");
        Directory.CreateDirectory(_storageRoot);

        // Sentinel NGOÀI root (cùng cấp với "storage/") — traversal thành công sẽ đọc được file này.
        File.WriteAllText(Path.Combine(_parentDir, "appsettings.json"), SentinelContent);

        var options = Options.Create(new StorageOptions { LocalDiskRoot = _storageRoot });
        _storage = new LocalDiskObjectStorage(options, new FakeHostEnvironment());
        _middleware = new MediaFileMiddleware(_ => Task.CompletedTask);

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_parentDir))
        {
            Directory.Delete(_parentDir, recursive: true);
        }

        return Task.CompletedTask;
    }

    // ---- happy path ----

    [Fact]
    public async Task GET_existing_key_returns_200_with_bytes_and_headers()
    {
        byte[] content = [1, 2, 3, 4, 5];
        const string key = "shops/s1/website/2026/09/photo.webp";
        await _storage.PutAsync(key, content, "image/webp", CancellationToken.None);

        var context = BuildContext("GET", "/" + key);
        await _middleware.InvokeAsync(context, _storage);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("image/webp", context.Response.ContentType);
        Assert.Equal(content.Length, context.Response.ContentLength);
        Assert.Equal("public, max-age=3600", context.Response.Headers.CacheControl.ToString());
        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());

        var bodyBytes = ((MemoryStream)context.Response.Body).ToArray();
        Assert.Equal(content, bodyBytes);
    }

    [Fact]
    public async Task HEAD_existing_key_returns_200_with_headers_but_no_body()
    {
        byte[] content = [1, 2, 3, 4, 5];
        const string key = "shops/s1/website/2026/09/head.webp";
        await _storage.PutAsync(key, content, "image/webp", CancellationToken.None);

        var context = BuildContext("HEAD", "/" + key);
        await _middleware.InvokeAsync(context, _storage);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(content.Length, context.Response.ContentLength);
        Assert.Equal(0, ((MemoryStream)context.Response.Body).Length);
    }

    [Fact]
    public async Task POST_returns_405_with_Allow_header()
    {
        const string key = "shops/s1/website/2026/09/photo.webp";

        var context = BuildContext("POST", "/" + key);
        await _middleware.InvokeAsync(context, _storage);

        Assert.Equal(StatusCodes.Status405MethodNotAllowed, context.Response.StatusCode);
        Assert.Equal("GET, HEAD", context.Response.Headers.Allow.ToString());
    }

    [Fact]
    public async Task Missing_key_returns_404()
    {
        var context = BuildContext("GET", "/shops/s1/website/2026/09/does-not-exist.webp");
        await _middleware.InvokeAsync(context, _storage);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task No_auth_required_context_has_no_authenticated_user()
    {
        byte[] content = [9];
        const string key = "shops/s1/website/2026/09/anon.webp";
        await _storage.PutAsync(key, content, "image/webp", CancellationToken.None);

        var context = BuildContext("GET", "/" + key);
        // DefaultHttpContext mặc định trả ClaimsIdentity ẩn danh (IsAuthenticated=false), KHÔNG có
        // AuthenticationMiddleware nào chạy trước — đúng thực tế vì `/media` branch off pipeline
        // TRƯỚC `UseAuthentication()` trong Program.cs.
        Assert.False(context.User.Identity?.IsAuthenticated ?? false);

        await _middleware.InvokeAsync(context, _storage);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    // ---- R1: path traversal ----

    [Theory]
    [InlineData("/../appsettings.json", "/../appsettings.json")]
    [InlineData("/shops/%2e%2e/%2e%2e/appsettings.json", "/shops/../../appsettings.json")]
    [InlineData("/shops%5c..%5cx", "/shops%5c..%5cx")]
    [InlineData("//etc/passwd", "//etc/passwd")]
    public async Task Traversal_variants_return_404_and_never_read_outside_root(string rawTarget, string decodedPath)
    {
        var context = BuildContext("GET", decodedPath, rawTarget: "/media" + rawTarget);
        await _middleware.InvokeAsync(context, _storage);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Equal(0, ((MemoryStream)context.Response.Body).Length);
    }

    [Fact]
    public async Task Encoded_forward_slash_separator_returns_404()
    {
        // "%2F" giữ nguyên không decode trong Request.Path theo ASP.NET Core — mô phỏng bằng cách
        // đặt raw target chứa %2f trong khi decoded path (giả lập) không chứa traversal rõ ràng, để
        // chứng minh middleware tự kiểm raw target chứ không chỉ tin Path đã decode.
        var context = BuildContext("GET", "/shops/s1/website/2026/09/photo.webp", rawTarget: "/media/shops%2f..%2fx");
        await _middleware.InvokeAsync(context, _storage);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task Key_outside_shops_prefix_returns_404()
    {
        var context = BuildContext("GET", "/outside/prefix/key.webp");
        await _middleware.InvokeAsync(context, _storage);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task Sentinel_file_outside_storage_root_is_never_served()
    {
        // Chứng minh trực tiếp bằng cách gọi storage.OpenReadAsync qua middleware với key trỏ đúng
        // "shops/../appsettings.json" (sau khi ValidateKey chặn) — đồng thời xác nhận qua file thật
        // rằng middleware never trả về nội dung sentinel dù bất kỳ path nào ở trên.
        var context = BuildContext("GET", "/shops/../../appsettings.json");
        await _middleware.InvokeAsync(context, _storage);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        var bodyBytes = ((MemoryStream)context.Response.Body).ToArray();
        Assert.DoesNotContain(SentinelContent, System.Text.Encoding.UTF8.GetString(bodyBytes));
    }

    // ---- helpers ----

    private static DefaultHttpContext BuildContext(string method, string decodedPath, string? rawTarget = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = decodedPath;
        context.Response.Body = new MemoryStream();
        context.Features.Set<IHttpRequestFeature>(new HttpRequestFeature
        {
            Method = method,
            Path = decodedPath,
            RawTarget = rawTarget ?? "/media" + decodedPath,
        });
        return context;
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "Vsite.IntegrationTests";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public string EnvironmentName { get; set; } = "Test";
    }
}
