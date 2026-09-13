namespace Vsite.Domain.Exceptions;

/// <summary>429 — vượt ngưỡng rate-limit/lockout (Quyết định #32: lockout theo scope, không phải
/// toàn cục — dò password ở Shop C không được khoá tài khoản ở Shop A).</summary>
public sealed class TooManyRequestsException(string errorCode, string message) : AppException(errorCode, message, statusCode: 429);
