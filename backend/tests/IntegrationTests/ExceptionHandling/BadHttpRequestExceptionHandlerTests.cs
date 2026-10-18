using Microsoft.AspNetCore.Http;
using Vsite.Api.ExceptionHandling;

namespace Vsite.IntegrationTests.ExceptionHandling;

/// <summary>
/// Review sau T5, MEDIA-001 (issue #4): xác nhận <see cref="BadHttpRequestExceptionHandler"/> dịch
/// đúng <see cref="BadHttpRequestException"/> (413, ném bởi Kestrel khi vượt
/// <c>IHttpMaxRequestBodySizeFeature.MaxRequestBodySize</c> — set thủ công ở <c>MediaEndpoints</c>)
/// thành ProblemDetails có <c>error_code = MEDIA_FILE_TOO_LARGE</c> (#19). KHÔNG cần Docker — gọi
/// thẳng handler với <see cref="DefaultHttpContext"/>, không cần host/pipeline thật.
///
/// ⚠️ TestServer/WebApplicationFactory không được xác nhận có enforce
/// <c>IHttpMaxRequestBodySizeFeature</c> giống Kestrel thật hay không (không kiểm tra được — cần
/// Docker để dựng <c>MediaApiFactory</c> gửi request &gt;11MB thật). Test này chỉ xác nhận PHẦN
/// DỊCH LỖI → ProblemDetails là đúng, không xác nhận Kestrel/TestServer THẬT SỰ ném lỗi ở ngưỡng
/// nào — ghi ở `Docs/tasks/MEDIA-001/changelog.md` mục "Chưa làm xong" #7.
/// </summary>
public sealed class BadHttpRequestExceptionHandlerTests
{
    private readonly BadHttpRequestExceptionHandler _handler = new();

    [Fact]
    public async Task Maps_413_BadHttpRequestException_to_ProblemDetails_with_MEDIA_FILE_TOO_LARGE()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var exception = new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge);

        var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("MEDIA_FILE_TOO_LARGE", body, StringComparison.Ordinal);
        Assert.Contains("413", body, StringComparison.Ordinal);
    }

    private static async Task<string> HandleOnRouteAsync(Microsoft.AspNetCore.Http.Endpoint? endpoint)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Features.Set<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>(
            new Microsoft.AspNetCore.Diagnostics.ExceptionHandlerFeature { Endpoint = endpoint, Error = new Exception() });

        var handled = await new BadHttpRequestExceptionHandler().TryHandleAsync(
            context, new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await new StreamReader(context.Response.Body).ReadToEndAsync();
    }

    /// <summary>T10 (WEBSITE-001) — route-aware: route khai RequestBodyTooLargeErrorMetadata trả mã riêng; route khác (kể cả
    /// route có endpoint nhưng không có metadata, như mọi route Media) vẫn MEDIA_FILE_TOO_LARGE.</summary>
    [Fact]
    public async Task Route_with_metadata_gets_its_own_code_while_other_routes_keep_MEDIA_FILE_TOO_LARGE()
    {
        var website = new Microsoft.AspNetCore.Http.Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new RequestBodyTooLargeErrorMetadata("TREE_BODY_TOO_LARGE", "x")), "website");
        var media = new Microsoft.AspNetCore.Http.Endpoint(_ => Task.CompletedTask, EndpointMetadataCollection.Empty, "media");

        var websiteBody = await HandleOnRouteAsync(website);
        Assert.Contains("TREE_BODY_TOO_LARGE", websiteBody, StringComparison.Ordinal);
        Assert.DoesNotContain("MEDIA_FILE_TOO_LARGE", websiteBody, StringComparison.Ordinal);
        Assert.Contains("MEDIA_FILE_TOO_LARGE", await HandleOnRouteAsync(media), StringComparison.Ordinal);
        Assert.Contains("MEDIA_FILE_TOO_LARGE", await HandleOnRouteAsync(null), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ignores_BadHttpRequestException_with_other_status_codes()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        // 400 — malformed request khác (không phải body-too-large) — KHÔNG mang ý nghĩa
        // "MEDIA_FILE_TOO_LARGE", handler này phải bỏ qua để framework xử lý mặc định.
        var exception = new BadHttpRequestException("Malformed request.", StatusCodes.Status400BadRequest);

        var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.False(handled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode); // không bị handler đụng vào
    }

    [Fact]
    public async Task Ignores_other_exception_types()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var handled = await _handler.TryHandleAsync(context, new InvalidOperationException("unrelated"), CancellationToken.None);

        Assert.False(handled);
    }
}
