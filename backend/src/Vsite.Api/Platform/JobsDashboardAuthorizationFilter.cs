using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;
using Vsite.Api.Tenancy;
using Vsite.Domain.Abstractions;

namespace Vsite.Api.Platform;

/// <summary>
/// JOBS-001 T4 / #114 mục 8 — cổng vào dashboard <c>/hangfire</c>: chỉ cho qua khi request đi vào bằng host
/// admin (<c>TenantContext.AudienceKind == Portal</c>, R2 — cookie chép sang host shop/api không mở được) VÀ
/// cookie dashboard còn hạn, giải mã đúng. Mọi lỗi ⇒ false (Hangfire trả 401), không bao giờ ném.
/// <para>
/// JOBS-001-D1 (W1) — chống CSRF cho thao tác ghi: với method không an toàn (khác GET/HEAD/OPTIONS) còn phải
/// cùng origin. <c>SameSite=Strict</c> không đủ vì <c>{slug}.vsite.vn</c> và <c>admin.vsite.vn</c> cùng site,
/// còn Hangfire chỉ kiểm token khi có <c>IAntiforgery</c> (repo không đăng ký). Có <c>Origin</c> ⇒ host[:port]
/// của nó phải bằng <c>Host</c> của request; không có <c>Origin</c> ⇒ chỉ nhận <c>Sec-Fetch-Site: same-origin</c>;
/// còn lại (kể cả <c>Origin</c> hỏng/"null") ⇒ từ chối. Client không phải trình duyệt không gửi Origin cũng bị
/// từ chối — có chủ đích.
/// </para>
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
            if (cookie is null || !cookie.TryValidate(http.Request, out _))
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(IsSafeMethod(http.Request.Method) || IsSameOrigin(http.Request));
        }
        catch (Exception)
        {
            return Task.FromResult(false);
        }
    }

    private static bool IsSafeMethod(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);

    private static bool IsSameOrigin(HttpRequest request)
    {
        var origin = request.Headers.Origin.ToString();
        if (string.IsNullOrEmpty(origin))
        {
            return string.Equals(request.Headers["Sec-Fetch-Site"].ToString(), "same-origin", StringComparison.OrdinalIgnoreCase);
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(uri.Host))
        {
            return false;
        }

        var host = request.Host;
        if (!host.HasValue)
        {
            return false;
        }

        // So host[:port], không so scheme (sau reverse proxy scheme nội bộ có thể khác scheme của trình duyệt).
        var requestPort = host.Port ?? uri.Port;
        return string.Equals(uri.Host, host.Host, StringComparison.OrdinalIgnoreCase) && uri.Port == requestPort;
    }
}
