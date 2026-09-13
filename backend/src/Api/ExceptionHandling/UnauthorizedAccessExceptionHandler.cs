using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Api.ExceptionHandling;

/// <summary>architecture-guide.md §6 — UnauthorizedAccessException → 401/"UNAUTHORIZED" (credential
/// sai, chưa đăng nhập...). Dùng chung cho mọi module, không phải AppException vì đây là ngoại lệ
/// built-in .NET (không cần ErrorCode tự khai — luôn cùng nghĩa "chưa xác thực được").</summary>
public sealed class UnauthorizedAccessExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not UnauthorizedAccessException unauthorizedException)
        {
            return false;
        }

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = unauthorizedException.Message,
            Extensions = { ["error_code"] = "UNAUTHORIZED" },
        };

        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
