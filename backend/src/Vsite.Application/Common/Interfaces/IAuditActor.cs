namespace Vsite.Application.Common.Interfaces;

/// <summary>
/// Ai đang ghi dữ liệu — để `AppDbContext` đóng dấu `CreatedByUserId`/`UpdatedByUserId`
/// (REFACTOR-BE-001; trước đó hai cột này luôn NULL). Khác <see cref="ICurrentUserContext"/>:
/// KHÔNG throw khi chưa đăng nhập (vd. Register, ForgotPassword, job nền) — trả `null`.
/// </summary>
public interface IAuditActor
{
    Guid? UserId { get; }
}
