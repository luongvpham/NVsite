namespace Vsite.Application.Website.Interfaces;

/// <param name="Clean">HTML đã làm sạch theo profile (bỏ thẻ/thuộc tính ngoài whitelist, scheme ngoài https/http/mailto/tel).</param>
/// <param name="Unsafe">
/// <c>true</c> khi thứ bị cắt thuộc danh sách nguy hiểm (<c>sanitize-profiles.json#dangerous</c>) — nhận biết bằng
/// SỰ KIỆN của thư viện sanitize chứ không so chuỗi trước/sau (R5).
/// </param>
public sealed record RichTextSanitizeResult(string Clean, bool Unsafe);

/// <summary>
/// Làm sạch HTML của prop <c>richText</c> (T4, WEBSITE-001, #67). Implement ở Infrastructure (thư viện ngoài).
/// Idempotent: <c>Sanitize(Sanitize(x).Clean).Clean == Sanitize(x).Clean</c>.
/// </summary>
public interface IRichTextSanitizer
{
    /// <exception cref="ArgumentException"><paramref name="profile"/> không phải profile trong sanitize-profiles.json.</exception>
    RichTextSanitizeResult Sanitize(string html, string profile);
}
