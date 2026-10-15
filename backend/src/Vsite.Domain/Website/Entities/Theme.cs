using Vsite.Domain.Common;
using Vsite.Domain.Exceptions;

namespace Vsite.Domain.Website.Entities;

/// <summary>
/// `05` §3 — theme của website (1:1). PK = <c>WebsiteId</c>: entity dùng <c>Id</c> của base làm khoá và
/// <see cref="WebsiteId"/> là bí danh của nó (cột DB tên <c>website_id</c>, xem ThemeConfiguration).
///
/// <see cref="Tokens"/> giữ là chuỗi JSON (jsonb) — shape/whitelist validate ở Application bằng
/// <c>theme-tokens.schema.json</c> + <c>theme-tokens.json</c> (#97), không ở entity. Không có concurrency
/// token (G7: một người/lần, ghi cuối thắng).
/// </summary>
public sealed class Theme : TenantAuditableEntity
{
    public const int MaxPresetKeyLength = 50;

    private Theme()
    {
        Tokens = null!;
    }

    private Theme(Guid websiteId) : base(websiteId)
    {
        Tokens = null!;
    }

    public static Theme Create(Guid websiteId, Guid shopId, string tokensJson, string? presetKey = null)
    {
        var theme = new Theme(websiteId) { ShopId = shopId };
        theme.Replace(tokensJson, presetKey);
        return theme;
    }

    /// <summary>Bí danh của <see cref="BaseEntity.Id"/> — theme là 1:1 với website.</summary>
    public Guid WebsiteId => Id;

    /// <summary>Preset gốc đã chọn (<c>spa-lavender</c>…) để hiển thị trong UI.</summary>
    public string? PresetKey { get; private set; }

    public string Tokens { get; private set; }

    /// <summary>Ghi cả tài liệu tokens (không patch từng phần).</summary>
    public void Replace(string tokensJson, string? presetKey)
    {
        if (string.IsNullOrWhiteSpace(tokensJson))
        {
            throw new DomainException("THEME_TOKENS_REQUIRED", "Tokens của theme không được rỗng.");
        }

        if ((presetKey?.Length ?? 0) > MaxPresetKeyLength)
        {
            throw new DomainException("THEME_PRESET_TOO_LONG", $"PresetKey tối đa {MaxPresetKeyLength} ký tự.");
        }

        Tokens = tokensJson;
        PresetKey = presetKey;
    }
}
