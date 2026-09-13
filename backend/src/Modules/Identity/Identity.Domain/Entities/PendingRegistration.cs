using Shared.Domain;

namespace Identity.Domain.Entities;

/// <summary>
/// 03 §5 — `User` chỉ được tạo sau khi email đã verify xong (Quyết định #30). Trong lúc chờ, dữ
/// liệu đăng ký nằm ở bảng staging này, KHÔNG dùng `User.Status = PendingVerification` (vi phạm
/// ràng buộc CHECK [2] ở §4 + mở đường email squatting).
/// </summary>
public sealed class PendingRegistration : BaseEntity
{
    public PendingRegistration()
    {
    }

    public PendingRegistration(Guid id) : base(id)
    {
    }

    public required string Email { get; init; }
    public required string EmailNormalized { get; init; }
    public required string PasswordHash { get; init; }
    public string? FullName { get; init; }

    /// <summary>Context đăng ký — null = vsite.vn, có giá trị = domain shop (03 §5).</summary>
    public Guid? ShopId { get; init; }

    /// <summary>HASH của token, không lưu raw (03 §5).</summary>
    public required string TokenHash { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
