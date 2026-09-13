using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace Api.ExceptionHandling;

/// <summary>
/// Mặc định ASP.NET Core trả 401/403 KHÔNG có body ProblemDetails khi authorization fail — vi phạm
/// Quyết định #19 ("lỗi trả ProblemDetails có error_code"). Handler này thay thế default, áp dụng
/// cho MỌI policy authorization thất bại (không chỉ Identity) — dùng chung mọi module.
/// </summary>
public sealed class ProblemDetailsAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
        {
            await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        var isAuthenticated = context.User.Identity?.IsAuthenticated == true;

        var problemDetails = isAuthenticated
            ? new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = "Token hiện tại không đủ quyền cho thao tác này.",
                Extensions = { ["error_code"] = "INSUFFICIENT_SCOPE" },
            }
            : new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "Yêu cầu đăng nhập.",
                Extensions = { ["error_code"] = "UNAUTHORIZED" },
            };

        context.Response.StatusCode = problemDetails.Status!.Value;
        await context.Response.WriteAsJsonAsync(problemDetails);
    }
}
