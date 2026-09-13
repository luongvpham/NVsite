namespace Shared.Exceptions;

/// <summary>
/// DesignIdeal/architecture-guide.md §2. Base type cho MỌI exception domain/application được xử
/// lý — mang `ErrorCode` machine-readable + `StatusCode` HTTP, để một middleware duy nhất
/// (Bước sau, Api layer) dịch nó thành ProblemDetails (RFC 7807, Quyết định #19) mà không cần biết
/// chi tiết từng loại lỗi nghiệp vụ.
/// </summary>
public abstract class AppException : Exception
{
    public string ErrorCode { get; }
    public int StatusCode { get; }
    public IDictionary<string, string[]> Errors { get; }
    public object? ErrorParams { get; }

    /// <summary>Chi tiết nội bộ an toàn để log server-side — KHÔNG BAO GIỜ trả về client (khác
    /// <see cref="Exception.Message"/>, vốn là text an toàn để hiển thị).</summary>
    public string? LogDetail { get; }

    protected AppException(
        string errorCode,
        string message,
        int statusCode,
        IDictionary<string, string[]>? errors = null,
        object? errorParams = null,
        string? logDetail = null)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
        Errors = errors ?? new Dictionary<string, string[]>();
        ErrorParams = errorParams;
        LogDetail = logDetail;
    }
}
