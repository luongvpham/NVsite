namespace Vsite.Domain.Exceptions;

/// <summary>415 — request đúng cú pháp nhưng sai Content-Type (vd. endpoint chỉ nhận
/// <c>multipart/form-data</c> mà request không phải multipart). Review sau T5, MEDIA-001.</summary>
public sealed class UnsupportedMediaTypeException(string errorCode, string message) : AppException(errorCode, message, statusCode: 415);
