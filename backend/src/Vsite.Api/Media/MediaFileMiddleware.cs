using Microsoft.AspNetCore.Http.Features;
using Vsite.Application.Common.Imaging;

namespace Vsite.Api.Media;

/// <summary>
/// T9, MEDIA-001 (#53, #81, #83) — phục vụ <c>/media/{key}</c> trên MỌI host qua
/// <see cref="IObjectStorage"/>. Gắn bằng <c>app.Map("/media", ...)</c> TRƯỚC
/// <c>TenantResolutionMiddleware</c> trong <c>Program.cs</c>: không tra Redis, không kiểm tenant
/// (#81 — đọc ảnh luôn public), không auth. Middleware này là TERMINAL trong branch <c>/media</c> —
/// không gọi <c>next</c>, luôn tự set response.
///
/// R1 (path traversal) — luồng an toàn:
/// 1. Chỉ nhận GET/HEAD, còn lại 405 (Allow: GET, HEAD) — không chạm storage.
/// 2. Kiểm RAW request target (chưa qua decode của ASP.NET Core) có chứa <c>%2F</c>/<c>%5C</c>
///    (separator đã encode) không — có thì 404 ngay, không decode/validate tiếp. ASP.NET Core giữ
///    nguyên <c>%2F</c> không decode trong <c>Request.Path</c> (tránh nhầm lẫn path segment), nhưng
///    <c>%5C</c>/<c>%2E</c>... vẫn được decode bình thường — nên phải tự kiểm raw target, không tin
///    <see cref="HttpRequest.Path"/> cho việc này.
/// 3. Lấy key từ <see cref="HttpRequest.Path"/> đã decode (phần còn lại sau khi <c>Map("/media")</c>
///    cắt tiền tố) — decode CHỈ MỘT LẦN (đây là decode của chính ASP.NET Core, không tự
///    <c>Uri.UnescapeDataString</c> thêm lần nữa).
/// 4. <see cref="ImagePaths.ValidateKey"/> — sai (không bắt đầu <c>shops/</c>, chứa <c>..</c>,
///    <c>\</c>, <c>//</c>, ký tự điều khiển, bắt đầu <c>/</c>) → <see cref="ArgumentException"/>,
///    bắt lại thành 404, KHÔNG để lọt thành 500.
/// 5. <see cref="IObjectStorage.OpenReadAsync"/> — null (không tồn tại) → 404. Provider (LocalDisk/S3)
///    tự kiểm thêm lớp path-resolve của riêng nó (xem <c>LocalDiskObjectStorage.ResolvePath</c>).
/// </summary>
public sealed class MediaFileMiddleware
{
    private static readonly string[] AllowedMethods = ["GET, HEAD"];

    // `next` không được gọi — middleware này TERMINAL trong branch `/media` (xem doc ở trên). Tham
    // số bắt buộc phải có vì `UseMiddleware&lt;T&gt;` luôn truyền `RequestDelegate` làm ctor-arg đầu
    // tiên (convention-based middleware của ASP.NET Core).
    public MediaFileMiddleware(RequestDelegate next)
    {
        _ = next;
    }

    public async Task InvokeAsync(HttpContext context, IObjectStorage storage)
    {
        var method = context.Request.Method;
        if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method))
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            context.Response.Headers.Allow = AllowedMethods;
            return;
        }

        var rawTarget = context.Features.Get<IHttpRequestFeature>()?.RawTarget ?? context.Request.Path.Value ?? string.Empty;
        if (rawTarget.Contains("%2f", StringComparison.OrdinalIgnoreCase) ||
            rawTarget.Contains("%5c", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var key = (context.Request.Path.Value ?? string.Empty).TrimStart('/');

        StoredObject? stored;
        try
        {
            ImagePaths.ValidateKey(key);
            stored = await storage.OpenReadAsync(key, context.RequestAborted);
        }
        catch (ArgumentException)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (stored is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await using var content = stored.Content;

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = stored.ContentType;
        context.Response.ContentLength = stored.Length;
        context.Response.Headers.CacheControl = "public, max-age=3600";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";

        if (HttpMethods.IsHead(method))
        {
            return;
        }

        await content.CopyToAsync(context.Response.Body, context.RequestAborted);
    }
}
