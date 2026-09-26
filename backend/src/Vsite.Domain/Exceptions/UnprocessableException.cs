namespace Vsite.Domain.Exceptions;

/// <summary>422 — nội dung request hợp lệ về hình thức nhưng không xử lý được (vd. ảnh upload vượt
/// giới hạn kích thước/pixel, sai định dạng, hoặc dữ liệu ảnh hỏng). Dùng cho pipeline ảnh
/// (`Vsite.Infrastructure.Imaging`, Quyết định #53/#84/#85) — khác 400 vì request tự nó không sai
/// cú pháp, chỉ là nội dung không "chế biến" được.</summary>
public sealed class UnprocessableException(string errorCode, string message) : AppException(errorCode, message, statusCode: 422);
