namespace Vsite.Application.Common.Imaging;

/// <summary>
/// Ném bởi <see cref="IObjectStorage.PutAsync"/> khi key đã tồn tại (Quyết định #75 — ảnh bất biến,
/// không ghi đè). Đây là tín hiệu ở tầng infrastructure, KHÔNG phải <c>AppException</c>/lỗi HTTP —
/// caller (Media module và các module dùng chung pipeline ảnh) tự quyết định dịch nó thành lỗi
/// nghiệp vụ nào (thường là 409/retry với key mới).
/// </summary>
public sealed class ObjectAlreadyExistsException(string key)
    : Exception($"Object với key '{key}' đã tồn tại.")
{
    public string Key { get; } = key;
}
