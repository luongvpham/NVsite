using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Vsite.Api.ExceptionHandling;

/// <summary>
/// Review sau T5, MEDIA-001 (#19 — mọi lỗi phải là ProblemDetails có <c>error_code</c>). Vượt giới
/// hạn dung lượng request (<c>IHttpMaxRequestBodySizeFeature.MaxRequestBodySize</c>, set thủ công ở
/// <c>MediaEndpoints</c>) khiến Kestrel ném <see cref="BadHttpRequestException"/> NGAY TRONG
/// <c>HttpRequest.ReadFormAsync</c> — đây KHÔNG phải <c>AppException</c> nên
/// <see cref="AppExceptionHandler"/> bỏ qua, và mặc định ASP.NET Core trả 413 KHÔNG có body/error_code.
///
/// Chỉ bắt đúng case body-too-large (<see cref="BadHttpRequestException.StatusCode"/> = 413) —
/// <see cref="BadHttpRequestException"/> còn được ném cho nhiều lỗi request malformed khác (400),
/// những case đó KHÔNG có error_code riêng (giữ hành vi mặc định của framework), chỉ 413 mới mang ý
/// nghĩa nghiệp vụ rõ ràng "file quá lớn".
///
/// ⚠️ Dùng chung cho MỌI module set <c>IHttpMaxRequestBodySizeFeature</c> giống <c>MediaEndpoints</c>
/// — hiện tại chỉ Media dùng, nên <c>error_code</c> mang tên <c>MEDIA_FILE_TOO_LARGE</c>. Module khác
/// cần limit tương tự với ngữ nghĩa khác thì đổi handler này generic hơn lúc đó, đừng copy-paste.
/// </summary>
public sealed class BadHttpRequestExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException badRequest || badRequest.StatusCode != StatusCodes.Status413PayloadTooLarge)
        {
            return false;
        }

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status413PayloadTooLarge,
            Title = "Payload Too Large",
            Detail = "File vượt quá giới hạn dung lượng cho phép.",
            Extensions = { ["error_code"] = "MEDIA_FILE_TOO_LARGE" },
        };

        httpContext.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
