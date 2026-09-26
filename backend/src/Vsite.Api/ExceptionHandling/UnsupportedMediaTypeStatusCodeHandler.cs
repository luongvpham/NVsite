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
/// ⚠️ error_code cố định <c>MEDIA_MULTIPART_REQUIRED</c> vì hiện tại chỉ Media có endpoint multipart
/// (nên chỉ Media có thể sinh 415 kiểu này). Module khác cần 415 với ngữ nghĩa khác thì tách handler
/// theo route/path ở đây, đừng sửa cứng thêm nhánh else-if — xem cảnh báo tương tự ở
/// <see cref="BadHttpRequestExceptionHandler"/>.
/// </summary>
public static class UnsupportedMediaTypeStatusCodeHandler
{
    public static Task HandleAsync(StatusCodeContext context)
    {
        var response = context.HttpContext.Response;
        if (response.StatusCode != StatusCodes.Status415UnsupportedMediaType)
        {
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
