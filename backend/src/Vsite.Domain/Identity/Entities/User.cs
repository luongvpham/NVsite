using Vsite.Domain.Common;
using Vsite.Domain.Identity.Enums;

namespace Vsite.Domain.Identity.Entities;

/// <summary>
/// 03 §3.1 — tài khoản toàn cục của một người. KHÔNG chứa `ShopId` (platform-scoped, không kế thừa
/// TenantEntity), KHÔNG chứa cột `GoogleId`/`FacebookId`/`ZaloId` (xem <see cref="ExternalLogin"/>).
///
/// `PasswordHash` dùng <c>Microsoft.AspNetCore.Identity.PasswordHasher&lt;User&gt;</c> — định dạng
/// output của hasher này đã tự chứa salt + iteration count trong MỘT chuỗi, nên KHÔNG có cột
/// `PasswordSalt` riêng như bản vẽ ban đầu ở 03 §3.1 (lệch có chủ đích — xem CLAUDE.md module).
///
/// `PasswordHash`/`LastLoginAt` chỉ gán lúc khởi tạo, sau đó đổi qua method (REFACTOR-AUTHZ-001).
/// Các field hồ sơ (tên, email, phone…) vẫn setter public — chưa có luồng sửa hồ sơ nào để đặt
/// invariant.
/// </summary>
public sealed class User : BaseAuditableEntity
{
    private string? _passwordHash;
    private DateTimeOffset? _lastLoginAt;

    public User()
    {
    }

    public User(Guid id) : base(id)
    {
    }

    public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }

    public string? Email { get; set; }
    public string? EmailNormalized { get; set; }
    public DateTimeOffset? EmailVerifiedAt { get; set; }

    public required PrimaryIdentityKind PrimaryIdentityKind { get; set; }

    public string? Phone { get; set; }
    public DateTimeOffset? PhoneVerifiedAt { get; set; }
    public string? CCCD { get; set; }
    public DateTimeOffset? CCCDVerifiedAt { get; set; }

    public string? PasswordHash { get => _passwordHash; init => _passwordHash = value; }

    public required Guid RoleId { get; set; }

    /// <summary>Generated column ('Platform' cố định) — xem 03 §4 ràng buộc [4].</summary>
    public RoleScope RoleScope { get; private set; } = RoleScope.Platform;

    public UserStatus Status { get; set; } = UserStatus.Active;

    public DateTimeOffset? LastLoginAt { get => _lastLoginAt; init => _lastLoginAt = value; }

    /// <summary>Chỉ tài khoản `Active` được đăng nhập / làm mới token (REFACTOR-AUTHZ-001 — trước đây
    /// `Suspended` vẫn đăng nhập được vì không chỗ nào kiểm).</summary>
    public bool CanSignIn => Status == UserStatus.Active;

    public void SetPassword(string passwordHash) => _passwordHash = passwordHash;

    public void RecordLogin(DateTimeOffset at) => _lastLoginAt = at;

    public ICollection<ExternalLogin> ExternalLogins { get; init; } = [];
    public ICollection<UserShop> UserShops { get; init; } = [];
}
