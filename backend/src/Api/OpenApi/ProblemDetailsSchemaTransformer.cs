using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace Api.OpenApi;

/// <summary>
/// <c>error_code</c> được gắn vào ProblemDetails qua <c>Extensions</c> lúc runtime (Shared.Errors.ApiError,
/// Api.ExceptionHandling.ValidationExceptionHandler) — Microsoft.AspNetCore.OpenApi không tự suy ra field này
/// từ reflection vì nó không phải property tĩnh trên <see cref="ProblemDetails"/>. Thiếu transformer này thì
/// contract commit KHÔNG khớp response thật (Quyết định #19: error_code là field bắt buộc, machine-readable).
/// Đăng ký cho MỌI document OpenAPI (mọi module) — xem backend/CLAUDE.md.
/// </summary>
public sealed class ProblemDetailsSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (typeof(ProblemDetails).IsAssignableFrom(context.JsonTypeInfo.Type))
        {
            schema.Properties["error_code"] = new OpenApiSchema { Type = "string", Nullable = true };
        }

        return Task.CompletedTask;
    }
}
