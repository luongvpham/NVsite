using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Vsite.Domain.Exceptions;

namespace Vsite.Api.ExceptionHandling;

/// <summary>
/// DesignIdeal/architecture-guide.md §6 — MỘT middleware duy nhất dịch mọi `AppException` (và
/// subclass: NotFound/Domain/Conflict/ForbiddenAccess/ValidationException của từng module) thành
/// ProblemDetails (RFC 7807, Quyết định #19), có `error_code` machine-readable. Dùng chung cho MỌI
/// module — không viết lại handler riêng per-module.
/// </summary>
public sealed class AppExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not AppException appException)
        {
            return false;
        }

        var problemDetails = new ProblemDetails
        {
            Status = appException.StatusCode,
            Title = TitleFor(appException.StatusCode),
            Detail = appException.Message,
            Extensions =
            {
                ["error_code"] = appException.ErrorCode,
                ["errors"] = appException.Errors,
            },
        };

        if (appException.ErrorParams is not null)
        {
            problemDetails.Extensions["params"] = appException.ErrorParams;
        }

        httpContext.Response.StatusCode = appException.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    private static string TitleFor(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        422 => "Validation Failed",
        _ => "Error",
    };
}
