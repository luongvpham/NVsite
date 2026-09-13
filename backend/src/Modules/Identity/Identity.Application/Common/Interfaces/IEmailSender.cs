namespace Identity.Application.Common.Interfaces;

/// <summary>
/// Chưa chọn nhà cung cấp email thật (quyết định kỹ thuật nhỏ, để dev tiếp — xem
/// `Identity.Infrastructure.Services.LoggingEmailSender` cho implementation dev/log-only hiện tại).
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string bodyHtml, CancellationToken cancellationToken = default);
}
