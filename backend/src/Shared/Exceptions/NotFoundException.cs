namespace Shared.Exceptions;

/// <summary>404 — entity lookup thất bại. ErrorCode luôn tự sinh `{ENTITY}_NOT_FOUND`, không truyền tay.</summary>
public sealed class NotFoundException(string entityName, object key)
    : AppException(
        errorCode: $"{entityName.ToUpperInvariant()}_NOT_FOUND",
        message: $"{entityName} với id '{key}' không tồn tại.",
        statusCode: 404,
        logDetail: $"{entityName} lookup failed for key={key}");
