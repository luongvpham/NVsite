using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;

namespace Vsite.Api.ExceptionHandling;

/// <summary>
/// Review sau Gate 2, T "D1": <see cref="UnsupportedMediaTypeStatusCodeHandler"/> bản đầu coi MỌI 415
/// rỗng body là "cần multipart" — SAI. Minimal API tự gắn <c>IAcceptsMetadata</c> cho request body suy
/// luận (vd. JSON) chứ không chỉ cho endpoint khai <c>.Accepts&lt;T&gt;("multipart/form-data")</c> —
/// endpoint JSON (`POST /auth/login`, `POST .../media/library/{assetId}/clones`, `POST /shops`, `PATCH
/// /shops/{shopId}`, …) gửi sai Content-Type cũng bị routing loại và trả 415 rỗng body, và handler cũ
/// gắn nhầm <c>MEDIA_MULTIPART_REQUIRED</c> — error_code phải đúng sự thật (#19).
///
/// Không dùng <c>HttpContext.GetEndpoint()</c> sau khi routing đã trả 415 — tại thời điểm đó routing
/// đã thay bằng một endpoint tổng hợp nội bộ (không phải endpoint thật đã bị loại khỏi candidate set),
/// không mang metadata thật để đọc. Cũng không dùng prefix path — `/shops/{shopId}/media/library/{assetId}/clones`
/// (JSON, KHÔNG multipart) share tiền tố `/shops/{shopId}/media/...` với các route multipart thật
/// (`slot-uploads`, `library` POST).
///
/// Thay vào đó: quét MỘT LẦN (lazy, thread-safe qua <see cref="Lazy{T}"/>, evaluation hoãn tới lần
/// gọi đầu — tại thời điểm đó mọi <c>Map*Endpoints()</c> trong <c>Program.cs</c> đã chạy xong) toàn bộ
/// <see cref="EndpointDataSource"/> đã đăng ký (<c>WebApplication.DataSources</c>, truyền vào lúc khởi
/// động), lọc đúng những <see cref="RouteEndpoint"/> có <see cref="IAcceptsMetadata"/> chứa
/// <c>multipart/form-data</c>, rồi so khớp request hiện tại theo HTTP method + route pattern (qua
/// <see cref="TemplateMatcher"/>, dựng thẳng từ <c>RouteEndpoint.RoutePattern</c> — KHÔNG round-trip
/// qua chuỗi <c>RoutePattern.RawText</c>, vì pattern gộp từ <c>MapGroup</c> không đảm bảo có RawText
/// gốc đúng nghĩa).
/// </summary>
public sealed class MultipartRouteMatcher
{
    // ⚠️ CỐ Ý giữ nguyên tham chiếu tới collection sống của WebApplication.DataSources, KHÔNG snapshot
    // (.ToList()) ngay ở constructor — hàm này được gọi từ Program.cs TRƯỚC mọi Map*Endpoints(), lúc
    // đó DataSources còn rỗng. Nhờ Lazy<T> hoãn evaluation tới request đầu tiên (sau khi app đã chạy
    // qua hết Map*Endpoints() lúc khởi động), đọc field này lúc đó mới thấy đủ endpoint.
    private readonly IEnumerable<EndpointDataSource> _dataSources;
    private readonly Lazy<IReadOnlyList<(string HttpMethod, TemplateMatcher Matcher)>> _multipartRoutes;

    public MultipartRouteMatcher(IEnumerable<EndpointDataSource> dataSources)
    {
        _dataSources = dataSources;
        _multipartRoutes = new Lazy<IReadOnlyList<(string, TemplateMatcher)>>(
            BuildMultipartRoutes, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>True nếu request hiện tại (HTTP method + path) khớp một route THẬT đã khai
    /// <c>.Accepts&lt;T&gt;("multipart/form-data")</c> — không suy diễn từ path prefix.</summary>
    public bool TargetsMultipartEndpoint(HttpRequest request)
    {
        foreach (var (method, matcher) in _multipartRoutes.Value)
        {
            if (!HttpMethods.Equals(request.Method, method))
            {
                continue;
            }

            var values = new RouteValueDictionary();
            if (matcher.TryMatch(request.Path, values))
            {
                return true;
            }
        }

        return false;
    }

    private IReadOnlyList<(string, TemplateMatcher)> BuildMultipartRoutes()
    {
        var result = new List<(string, TemplateMatcher)>();

        foreach (var dataSource in _dataSources)
        {
            foreach (var endpoint in dataSource.Endpoints)
            {
                if (endpoint is not RouteEndpoint routeEndpoint)
                {
                    continue;
                }

                var accepts = routeEndpoint.Metadata.GetMetadata<IAcceptsMetadata>();
                if (accepts is null || !accepts.ContentTypes.Any(
                        ct => string.Equals(ct, "multipart/form-data", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var httpMethods = routeEndpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods
                    ?? (IReadOnlyList<string>)[];
                var template = new RouteTemplate(routeEndpoint.RoutePattern);
                var matcher = new TemplateMatcher(template, new RouteValueDictionary());

                foreach (var method in httpMethods)
                {
                    result.Add((method, matcher));
                }
            }
        }

        return result;
    }
}
