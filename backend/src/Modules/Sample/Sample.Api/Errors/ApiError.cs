using Microsoft.AspNetCore.Http;

namespace Sample.Api.Errors;

/// <summary>
/// Lỗi trả về luôn là ProblemDetails (RFC 7807) có <c>error_code</c> machine-readable
/// (Quyết định #19). Không throw text thô, không dùng shape lỗi thứ hai.
/// </summary>
public static class ApiError
{
    public static IResult NotFound(string errorCode, string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            detail: detail,
            extensions: new Dictionary<string, object?> { ["error_code"] = errorCode });
}
