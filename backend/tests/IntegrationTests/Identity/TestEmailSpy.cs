using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Identity.Interfaces;

namespace Vsite.IntegrationTests;

/// <summary>Thay `LoggingEmailSender` thật trong test — bắt lại nội dung email để trích token
/// verify-email/reset-password (không có cách nào khác đọc raw token, vì nó chỉ tồn tại trong
/// email, DB chỉ lưu hash — đúng thiết kế 03 §5).</summary>
public sealed class TestEmailSpy : IEmailSender
{
    private readonly ConcurrentBag<(string To, string Body)> _sent = [];

    public Task SendAsync(string toEmail, string subject, string bodyHtml, CancellationToken cancellationToken = default)
    {
        _sent.Add((toEmail, bodyHtml));
        return Task.CompletedTask;
    }

    public string ExtractLastTokenFor(string toEmail)
    {
        var body = _sent.Where(e => e.To == toEmail).Select(e => e.Body).LastOrDefault()
            ?? throw new InvalidOperationException($"Không có email nào gửi tới {toEmail}.");

        var match = Regex.Match(body, "token=([^\"&]+)");
        if (!match.Success)
        {
            throw new InvalidOperationException($"Không tìm thấy token trong email gửi tới {toEmail}.");
        }

        return Uri.UnescapeDataString(match.Groups[1].Value);
    }
}
