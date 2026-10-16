using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Vsite.Application.Common.Persistence;

namespace Vsite.Api.ExceptionHandling;

/// <summary>
/// T9 (WEBSITE-001, R15, đóng REFACTOR-DB-001 #1/#2) — hai lỗi persistence trước đây rơi xuống 500:
/// <list type="bullet">
/// <item><c>DbUpdateConcurrencyException</c> (concurrency token như <c>xmin</c> của PageDraft lệch) → 409 <c>CONCURRENCY_CONFLICT</c>;</item>
/// <item>SQLSTATE <c>23505</c> từ BẤT KỲ unique nào (race mà kiểm-trước không bắt được) → 409 <c>CONFLICT_UNIQUE</c>.</item>
/// </list>
/// <c>detail</c> luôn là câu chung — KHÔNG đưa tên constraint, tên bảng/cột, giá trị khoá hay SQL ra client (R15). Chi tiết thật nằm ở log
/// (middleware ghi exception). Handler nghiệp vụ muốn mã riêng (vd. <c>DRAFT_CONFLICT</c>, <c>PAGE_SLUG_TAKEN</c>) tự bắt trước, đây chỉ là lưới an toàn.
/// Không động tới <c>AppException</c> (<see cref="AppExceptionHandler"/> lo), và không nhận SQLSTATE khác (23503/23514… vẫn 500 — lỗi lập trình).
/// </summary>
public sealed class ConcurrencyAndUniqueExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (code, detail) = Classify(exception);
        if (code is null)
        {
            return false;
        }

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Conflict",
            Detail = detail,
            Extensions =
            {
                ["error_code"] = code,
                ["errors"] = new Dictionary<string, string[]>(), // cùng shape với AppExceptionHandler
            },
        };

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    private static (string? Code, string Detail) Classify(Exception exception)
    {
        if (exception is Vsite.Domain.Exceptions.AppException)
        {
            return (null, string.Empty);
        }

        if (UniqueViolation.IsConcurrencyConflict(exception))
        {
            return ("CONCURRENCY_CONFLICT", "Dữ liệu đã bị thay đổi ở nơi khác. Hãy tải lại rồi thử lại.");
        }

        if (UniqueViolation.IsUniqueViolation(exception))
        {
            return ("CONFLICT_UNIQUE", "Dữ liệu này đã tồn tại.");
        }

        return (null, string.Empty);
    }
}
