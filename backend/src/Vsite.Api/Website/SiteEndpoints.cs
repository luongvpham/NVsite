using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using Vsite.Application.Website.Queries.GetPublishedSite;

namespace Vsite.Api.Website;

/// <summary>
/// WEBSITE-004 T6 (#108, #109) — <c>GET /api/site</c>: khách vãng lai đọc bản đang xuất bản của shop sở hữu Host.
/// Không token, không <c>shopId</c>/<c>host</c> ở path/query/body: <c>TenantResolutionMiddleware</c> đã suy <c>ShopId</c> từ Host
/// và handler đọc nó qua <c>ITenantContext</c>. Query string bị bỏ qua hoàn toàn (không có tham số nào được bind).
/// </summary>
public static class SiteEndpoints
{
    public static IEndpointRouteBuilder MapSiteEndpoints(this IEndpointRouteBuilder app)
    {
        var site = app.MapGroup("/site").WithTags("Site").WithGroupName("website");

        site.MapGet("", async (HttpContext http, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetPublishedSiteQuery(http.Request.Headers.IfNoneMatch.ToString()), ct);

            // no-cache = bắt hỏi lại (cho phép 304), không phải "đừng lưu" (#109: cache dữ liệu, không cache HTML).
            http.Response.Headers.CacheControl = "no-cache";
            if (result.ETag is not null)
            {
                http.Response.Headers[HeaderNames.ETag] = result.ETag;
            }

            return result.NotModified ? Results.StatusCode(StatusCodes.Status304NotModified) : Results.Ok(result.Body);
        })
            .WithName("getPublishedSite")
            .AllowAnonymous()
            .Produces<PublishedSiteDto>()
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
