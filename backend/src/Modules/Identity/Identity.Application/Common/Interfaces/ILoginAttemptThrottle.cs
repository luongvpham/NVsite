namespace Identity.Application.Common.Interfaces;

/// <summary>
/// Quyết định #32 — lockout PHẢI theo scope (`key` = `{emailNormalized}:{audience}`), không phải
/// toàn cục: dò password ở Shop C không được khoá tài khoản ở Shop A/vsite.vn. Implementation thật
/// dùng Redis (Identity.Infrastructure) — hạ tầng đã wire từ Phase 0.
/// </summary>
public interface ILoginAttemptThrottle
{
    Task<bool> IsLockedOutAsync(string key, CancellationToken cancellationToken);

    Task RecordFailureAsync(string key, CancellationToken cancellationToken);

    Task ResetAsync(string key, CancellationToken cancellationToken);
}
