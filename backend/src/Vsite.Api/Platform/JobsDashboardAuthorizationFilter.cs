using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;
using Vsite.Api.Tenancy;
using Vsite.Domain.Abstractions;

namespace Vsite.Api.Platform;

/// <summary>
/// JOBS-001 T4 / #114 mục 8 — cổng vào dashboard <c>/hangfire</c>: chỉ cho qua khi request đi vào bằng host
/// admin (<c>TenantContext.AudienceKind == Portal</c>, R2 — cookie chép sang host shop/api không mở được) VÀ
/// cookie dashboard còn hạn, giải mã đúng. Mọi lỗi ⇒ false (Hangfire trả 401), không bao giờ ném.
/// <c>TenantResolutionMiddleware</c> chạy trước mọi endpoint nên <c>/hangfire</c> cũng đã có tenant.
/// </summary>
public sealed class JobsDashboardAuthorizationFilter : IDashboardAsyncAuthorizationFilter
{
    public Task<bool> AuthorizeAsync(DashboardContext context)
    {
        try
        {
            if (context is not AspNetCoreDashboardContext aspNet)
            {
                return Task.FromResult(false);
            }

            var http = aspNet.HttpContext;
            var tenant = http.RequestServices.GetService(typeof(TenantContext)) as TenantContext;
            if (tenant is null || tenant.AudienceKind != TenantAudienceKind.Portal)
            {
                return Task.FromResult(false);
            }

            var cookie = http.RequestServices.GetService(typeof(JobsDashboardCookie)) as JobsDashboardCookie;
            return Task.FromResult(cookie is not null && cookie.TryValidate(http.Request, out _));
        }
        catch (Exception)
        {
            return Task.FromResult(false);
        }
    }
}
