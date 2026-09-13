using Shared.Domain;

namespace Identity.Domain.Entities;

/// <summary>
/// 03 §6.4 — quên mật khẩu LUÔN scoped theo context: reset ở `spa-abc.com` chỉ đổi
/// `UserShop.PasswordHash` của shop đó, không đụng `vsite.vn` hay shop khác. `TokenHash` — không
/// lưu raw, cùng nguyên tắc `PendingRegistration`/`RefreshToken`.
/// </summary>
public sealed class PasswordResetToken : BaseEntity
{
    public PasswordResetToken()
    {
    }

    public PasswordResetToken(Guid id) : base(id)
    {
    }

    public required Guid UserId { get; init; }

    /// <summary>`vsite-main` | `shop:{shopId}` | `vsite-portal` — token chỉ dùng đúng scope này.</summary>
    public required string Audience { get; init; }

    public Guid? ShopId { get; init; }

    public required string TokenHash { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
