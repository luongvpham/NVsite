using Microsoft.AspNetCore.Http;
using Vsite.Api.ExceptionHandling;
using Vsite.Domain.Exceptions;

namespace Vsite.IntegrationTests.ExceptionHandling;

/// <summary>
/// Review sau T5, MEDIA-001 (issue #5): xác nhận <see cref="AppExceptionHandler"/> dịch đúng
/// <see cref="UnsupportedMediaTypeException"/> (415 — <c>MediaEndpoints.RequireMultipart</c> ném khi
/// request không phải <c>multipart/form-data</c>) thành ProblemDetails có
/// <c>error_code = MEDIA_MULTIPART_REQUIRED</c>. KHÔNG cần Docker.
/// </summary>
public sealed class AppExceptionHandlerTests
{
    private readonly AppExceptionHandler _handler = new();

    [Fact]
    public async Task Maps_UnsupportedMediaTypeException_to_415_with_error_code()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var exception = new UnsupportedMediaTypeException("MEDIA_MULTIPART_REQUIRED", "Endpoint chỉ nhận multipart/form-data.");

        var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("MEDIA_MULTIPART_REQUIRED", body, StringComparison.Ordinal);
    }
}
