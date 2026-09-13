using Shared.Domain;

namespace Identity.Domain.Entities;

/// <summary>
/// Quyết định #3 (refresh token rotation, lưu DB, revoke được) + Quyết định #27/#31 (audience theo
/// scope). Không kế thừa `ShopEntity` — `ShopId` ở đây NULLABLE (null cho audience `vsite-main`/
/// `vsite-portal`), khác ngữ nghĩa "entity thuộc về một shop cụ thể" mà `ShopEntity` yêu cầu.
///
/// `TokenHash` — không lưu token thô, cùng nguyên tắc với `PendingRegistration.TokenHash` (03 §5).
/// </summary>
public sealed class RefreshToken : BaseEntity
{
    public RefreshToken()
    {
    }

    public RefreshToken(Guid id) : base(id)
    {
    }

    public required Guid UserId { get; init; }

    /// <summary>`vsite-main` | `shop:{shopId}` | `vsite-portal` (Quyết định #27/#31).</summary>
    public required string Audience { get; init; }

    /// <summary>Chỉ có giá trị khi <see cref="Audience"/> = `shop:{shopId}` — dùng để revoke/lockout
    /// theo scope (Quyết định #32: dò password ở Shop C không được khoá tài khoản ở Shop A).</summary>
    public Guid? ShopId { get; init; }

    public required string TokenHash { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Rotation: token cũ trỏ sang token mới thay thế nó khi refresh (03 §6.4 style — phát
    /// hiện reuse-attack nếu token đã revoke vẫn bị đem đi refresh).</summary>
    public Guid? ReplacedByTokenId { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
