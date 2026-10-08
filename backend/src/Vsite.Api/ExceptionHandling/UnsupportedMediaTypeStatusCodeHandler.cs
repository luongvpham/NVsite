using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Vsite.Api.ExceptionHandling;

/// <summary>
/// Root cause tìm được lúc chạy full-suite lần đầu trên máy có Docker (Docs/DOCKER-TEST-DEBT.md,
/// MEDIA-001): endpoint multipart khai <c>.Accepts&lt;T&gt;("multipart/form-data")</c> để OpenAPI ra
/// đúng requestBody — nhưng cùng lúc gắn <c>IAcceptsMetadata</c> mà routing (nội bộ
/// <c>ConsumesMatcherPolicy</c>) dùng để LOẠI endpoint khỏi candidate set khi Content-Type request
/// không khớp. Khi tất cả candidate bị loại vì content-type, routing tự trả 415 **trước khi endpoint
/// chạy** — request không bao giờ tới được <c>MediaEndpoints.RequireMultipart</c>, nên không có
/// ProblemDetails/<c>error_code</c> (vi phạm #19). Đây KHÔNG phải exception (routing chỉ set status
/// code rồi short-circuit) nên <see cref="AppExceptionHandler"/> (chạy trên <c>IExceptionHandler</c>,
/// cần exception) không bắt được.
///
/// Đã cân nhắc bỏ <c>.Accepts&lt;T&gt;()</c> để routing không enforce nữa — bị loại vì phải tự viết
/// lại phần OpenAPI requestBody generation (rủi ro đổi contract đã duyệt Gate 1). Chọn cách ít xâm
/// lấn hơn: <c>UseStatusCodePages</c> — đúng middleware cho case "status code đã set, body rỗng, chưa
/// ai ghi response" — CHỈ can thiệp khi status = 415, mọi status code khác (401/403/404/…, của MỌI
/// module) đi qua nguyên vẹn, không đổi hành vi.
///
/// ⚠️ Review Gate 2: bản đầu gắn <c>MEDIA_MULTIPART_REQUIRED</c> cho MỌI 415 rỗng body — SAI, vì
/// Minimal API cũng tự gắn <c>IAcceptsMetadata</c> cho endpoint JSON suy luận (vd. `POST /auth/login`,
/// `POST .../media/library/{assetId}/clones` — JSON, không multipart), gửi sai Content-Type cũng ra
/// 415 rỗng body theo đúng cơ chế trên, nhưng KHÔNG phải lỗi "thiếu multipart". Gắn cứng error_code
/// cho mọi 415 là phát biểu sai sự thật (#19). Sửa bằng <see cref="MultipartRouteMatcher"/> — chỉ gắn
/// <c>MEDIA_MULTIPART_REQUIRED</c> khi request hiện tại thật sự khớp (method + route pattern) một
/// endpoint đã khai <c>multipart/form-data</c>; 415 của mọi route khác đi qua nguyên vẹn, rỗng body
/// như trước (không đổi hành vi module khác).
/// </summary>
public static class UnsupportedMediaTypeStatusCodeHandler
{
    public static Task HandleAsync(StatusCodeContext context, MultipartRouteMatcher multipartRoutes)
    {
        var response = context.HttpContext.Response;
        if (response.StatusCode != StatusCodes.Status415UnsupportedMediaType)
        {
            return Task.CompletedTask;
        }

        if (!multipartRoutes.TargetsMultipartEndpoint(context.HttpContext.Request))
        {
            // 415 của route KHÔNG multipart (vd. content-type sai cho endpoint JSON) — giữ nguyên
            // hành vi mặc định của framework (rỗng body), không suy diễn error_code nào cả.
            return Task.CompletedTask;
        }

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status415UnsupportedMediaType,
            Title = "Unsupported Media Type",
            Detail = "Endpoint chỉ nhận Content-Type multipart/form-data.",
            Extensions = { ["error_code"] = "MEDIA_MULTIPART_REQUIRED" },
        };

        response.ContentType = "application/problem+json";
        return response.WriteAsJsonAsync(problemDetails);
    }
}
