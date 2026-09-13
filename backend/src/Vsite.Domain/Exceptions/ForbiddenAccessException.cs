namespace Vsite.Domain.Exceptions;

/// <summary>403 — đã xác thực nhưng không có quyền cho hành động này (khác 401 chưa xác thực).</summary>
public sealed class ForbiddenAccessException(string errorCode, string message) : AppException(errorCode, message, statusCode: 403);
