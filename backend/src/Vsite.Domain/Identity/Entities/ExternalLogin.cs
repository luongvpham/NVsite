using Vsite.Domain.Common;

namespace Vsite.Domain.Identity.Entities;

/// <summary>
/// 03 §3.2 — child entity trong aggregate <see cref="User"/>, KHÔNG phải aggregate riêng. Identity
/// ngoài = `Provider + ProviderUserId`, không bao giờ là email.
/// </summary>
public sealed class ExternalLogin : BaseAuditableEntity
{
    public ExternalLogin()
    {
    }

    public ExternalLogin(Guid id) : base(id)
    {
    }

    public required Guid UserId { get; init; }

    public required string Provider { get; init; }
    public required string ProviderUserId { get; init; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public User? User { get; init; }
}
