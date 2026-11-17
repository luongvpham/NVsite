using Vsite.Domain.Exceptions;

namespace Vsite.Application.Website.Publishing;

/// <summary>
/// Một lỗi chặn publish (Flow 1, data-needs "Portal — nút Xuất bản"). <paramref name="PageId"/>/<paramref name="PageTitle"/> =
/// <c>null</c> khi lỗi thuộc website (ảnh OG mặc định / favicon). <paramref name="Path"/> là JSON Pointer trong tree của trang
/// (<c>""</c> = cả trang, vd. ảnh tham chiếu hỏng — validator Media không nói id nào sai).
/// </summary>
public sealed record PublishPageError(Guid? PageId, string? PageTitle, string Path, string Code, string Message);

/// <summary>Phần <c>params</c> của 422 <c>PUBLISH_BLOCKED</c>: tối đa <see cref="PublishBlockedException.MaxErrors"/> mục.</summary>
public sealed record PublishBlockedParams(IReadOnlyList<PublishPageError> Errors, bool Truncated);

/// <summary>422 <c>PUBLISH_BLOCKED</c> — ít nhất một trang đang bật (hoặc ảnh cấp website) không hợp lệ; KHÔNG ghi gì.</summary>
public sealed class PublishBlockedException : AppException
{
    public const int MaxErrors = 100;

    public PublishBlockedException(IReadOnlyList<PublishPageError> errors, bool truncated)
        : base(
            "PUBLISH_BLOCKED",
            "Không thể xuất bản: một số trang chưa hợp lệ.",
            statusCode: 422,
            errorParams: new PublishBlockedParams(errors, truncated))
    {
    }
}
