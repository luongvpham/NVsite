namespace Identity.Application.Common.Interfaces;

/// <summary>Danh tính người gọi hiện tại — đọc từ JWT đã xác thực (claims `sub`/`aud`).
/// Implementation thật (đọc `HttpContext.User`) sống ở `Identity.Api`, Application chỉ thấy
/// interface (không reference ASP.NET Core trực tiếp).</summary>
public interface ICurrentUserContext
{
    Guid UserId { get; }

    /// <summary>Nguyên văn claim `aud` của token đang dùng — `vsite-main`/`vsite-portal`/
    /// `shop:{shopId}` (Quyết định #27).</summary>
    string Audience { get; }
}
