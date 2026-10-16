using Vsite.Application.Website.Validation;
using Vsite.Domain.Exceptions;

namespace Vsite.Application.Website.Exceptions;

/// <summary>Phần <c>params</c> của ProblemDetails khi tài liệu JSON (tree/tokens) sai: danh sách lỗi {path, code, message}.</summary>
public sealed record DocumentInvalidParams(IReadOnlyList<TreeValidationError> Errors, bool Truncated);

/// <summary>
/// 422 — cây/tokens không hợp lệ (<c>TREE_INVALID</c> | <c>SCHEMA_VERSION_UNSUPPORTED</c> | <c>THEME_INVALID</c>). Danh sách lỗi có cấu trúc
/// ở <see cref="Details"/> và được trả dưới <c>params.errors[]</c> (mỗi phần tử <c>{ path, code, message }</c>, path là JSON Pointer);
/// <c>errors</c> của ProblemDetails (dạng từ điển) vẫn được điền theo path để khớp shape chung #19.
/// </summary>
public sealed class DocumentInvalidException : AppException
{
    public DocumentInvalidException(string errorCode, IReadOnlyList<TreeValidationError> errors, bool truncated)
        : base(
            errorCode,
            errorCode == "SCHEMA_VERSION_UNSUPPORTED"
                ? "Phiên bản schema của tài liệu không được hỗ trợ."
                : "Tài liệu không hợp lệ.",
            statusCode: 422,
            errors: ToDictionary(errors),
            errorParams: new DocumentInvalidParams(errors, truncated))
    {
        Details = errors;
    }

    public IReadOnlyList<TreeValidationError> Details { get; }

    private static Dictionary<string, string[]> ToDictionary(IReadOnlyList<TreeValidationError> errors) =>
        errors
            .GroupBy(e => e.Path.Length == 0 ? "$" : e.Path, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => $"{e.Code}: {e.Message}").ToArray());
}

/// <summary>Phần <c>params</c> của 409 <c>DRAFT_CONFLICT</c> (data-needs câu 4): để FE hoà giải mà không phải đoán; KHÔNG kèm tree.</summary>
public sealed record DraftConflictParams(long CurrentVersion, DateTimeOffset UpdatedAt, Guid? UpdatedByUserId);

/// <summary>409 — tab khác đã lưu draft này (version lệch, hoặc <c>xmin</c> đổi giữa lúc đọc và lúc ghi).</summary>
public sealed class DraftConflictException(DraftConflictParams info)
    : AppException(
        "DRAFT_CONFLICT",
        "Trang đã được lưu ở nơi khác. Hãy tải lại bản mới nhất.",
        statusCode: 409,
        errorParams: info)
{
    public DraftConflictParams Info { get; } = info;
}
