namespace Shared.Pagination;

/// <summary>
/// Shape bắt buộc cho mọi endpoint phân trang (Quyết định #19) — { items, total, page, pageSize }.
/// Không tạo shape thứ hai cho module khác.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
