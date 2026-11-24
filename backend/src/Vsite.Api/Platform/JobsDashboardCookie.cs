using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

namespace Vsite.Api.Platform;

/// <summary>
/// #114 mục 8 / #115 — cookie ngắn hạn cho dashboard Hangfire (trình duyệt không gửi Bearer). Payload
/// <c>{ userId, expiresAt }</c> bảo vệ bằng <see cref="IDataProtector"/> purpose <see cref="Purpose"/>;
/// <c>HttpOnly</c>, <c>SameSite=Strict</c>, <c>Path=/hangfire</c>, host-only, <c>Secure</c> ngoài Development.
/// Một lớp giữ cả hai chiều: <see cref="Issue"/> (endpoint dashboard-session) và
/// <see cref="TryValidate(string?, out Guid)"/> (filter dashboard, JOBS-001 T4) — không bao giờ ném:
/// sai/sửa/purpose khác/hết hạn đều trả false. Không log giá trị cookie.
/// </summary>
public sealed class JobsDashboardCookie
{
    public const string Name = "vsite_jobs_dashboard";
    public const string CookiePath = "/hangfire";
    public const string Purpose = "Vsite.Platform.JobsDashboard.v1";

    /// <summary>Hằng, ≤ TTL access token (15 phút).</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private readonly IDataProtector _protector;
    private readonly TimeProvider _time;
    private readonly bool _isDevelopment;

    public JobsDashboardCookie(IDataProtectionProvider dataProtection, TimeProvider time, bool isDevelopment)
    {
        _protector = dataProtection.CreateProtector(Purpose);
        _time = time;
        _isDevelopment = isDevelopment;
    }

    /// <summary>Phát cookie cho <paramref name="userId"/>, hết hạn sau <see cref="Lifetime"/>.</summary>
    public void Issue(HttpResponse response, Guid userId)
    {
        var expiresAt = _time.GetUtcNow() + Lifetime;
        var payload = JsonSerializer.Serialize(new Payload(userId, expiresAt));
        response.Cookies.Append(Name, _protector.Protect(payload), new CookieOptions
        {
            HttpOnly = true,
            Secure = !_isDevelopment,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
            MaxAge = Lifetime,
        });
    }

    /// <summary>Đọc cookie từ request; false nếu thiếu hoặc không hợp lệ.</summary>
    public bool TryValidate(HttpRequest request, out Guid userId) =>
        TryValidate(request.Cookies.TryGetValue(Name, out var value) ? value : null, out userId);

    /// <summary>Kiểm giá trị cookie (đã giải mã URL). Không ném với mọi đầu vào.</summary>
    public bool TryValidate(string? value, out Guid userId)
    {
        userId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<Payload>(_protector.Unprotect(value));
            if (payload is null || payload.UserId == Guid.Empty || _time.GetUtcNow() >= payload.ExpiresAt)
            {
                return false;
            }

            userId = payload.UserId;
            return true;
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException or FormatException)
        {
            return false;
        }
    }

    private sealed record Payload(Guid UserId, DateTimeOffset ExpiresAt);
}
