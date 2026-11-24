using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vsite.Application.Common.Interfaces;
using Vsite.Domain.Authorization;

namespace Vsite.Api.Platform;

/// <summary>
/// Quyết định #114 mục 6 / #115 — nhóm route <c>/platform</c>: policy <see cref="AuthPolicies.PlatformAdmin"/>
/// gắn ở CẤP NHÓM (không gắn từng endpoint); <c>PlatformRoutePolicyTests</c> khẳng định mọi route
/// <c>/api/platform/*</c> mang policy này.
/// </summary>
public static class PlatformEndpoints
{
    public static IEndpointRouteBuilder MapPlatformEndpoints(this IEndpointRouteBuilder app)
    {
        var platform = app.MapGroup("/platform")
            .WithTags("Platform")
            .WithGroupName("platform")
            .RequireAuthorization(AuthPolicies.PlatformAdmin);

        platform.MapPost("/jobs/dashboard-session", (HttpContext http, ICurrentUserContext user, JobsDashboardCookie cookie) =>
            {
                cookie.Issue(http.Response, user.UserId);
                return Results.NoContent();
            })
            .WithName("createJobsDashboardSession")
            .WithSummary("Đổi JWT PlatformAdmin lấy cookie vào dashboard Hangfire (/hangfire).")
            .WithDescription("Không body. Set cookie httpOnly `vsite_jobs_dashboard` (Path=/hangfire, SameSite=Strict, sống 15 phút). (#114, #115)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }
}
