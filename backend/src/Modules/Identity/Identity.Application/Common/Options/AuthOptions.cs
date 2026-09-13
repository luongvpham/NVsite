namespace Identity.Application.Common.Options;

/// <summary>Bind từ config section "Auth" (appsettings). Đăng ký ở Infrastructure DI.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>03 §5 — link verify LUÔN trỏ về host cố định này, không phải domain shop.</summary>
    public required string ApiBaseUrl { get; init; }
}
