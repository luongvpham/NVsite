namespace Vsite.Domain.Exceptions;

/// <summary>409 — request mâu thuẫn với trạng thái hiện tại của resource (vd. "đã dispense rồi").
/// Khác 400 vì cách sửa đúng là "tải lại state", không phải "gửi lại request khác".</summary>
public sealed class ConflictException(string errorCode, string message) : AppException(errorCode, message, statusCode: 409);
