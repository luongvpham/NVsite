namespace Vsite.Domain.Exceptions;

/// <summary>400 — một business rule bị vi phạm. Không có ErrorCode mặc định — bắt buộc caller tự
/// khai, để trình biên dịch bắt được lỗi chưa phân loại trước khi nó tới tay client.</summary>
public sealed class DomainException(string errorCode, string message) : AppException(errorCode, message, statusCode: 400);
