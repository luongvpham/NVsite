using Microsoft.Extensions.Logging;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Identity.Interfaces;

namespace Vsite.Infrastructure.Identity;

/// <summary>
/// TODO(trước launch): thay bằng provider email thật (SES/SendGrid/...) — chưa chọn (quyết định
/// kỹ thuật nhỏ, không phải nghiệp vụ). Implementation này chỉ log ra console/log file, đủ để test
/// hết luồng verify-email/reset-password ở dev mà không cần hạ tầng email thật.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string bodyHtml, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[DEV EMAIL] To={ToEmail} Subject={Subject}\n{Body}", toEmail, subject, bodyHtml);
        return Task.CompletedTask;
    }
}
