using Vsite.Domain.Common;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Website.Enums;

namespace Vsite.Domain.Website.Entities;

/// <summary>
/// `05` §2 — gốc của module Website, đúng một website cho mỗi shop `Hosted` (UNIQUE <c>shop_id</c>).
/// Tên entity trùng tên module → nơi dùng phải alias <c>using WebsiteEntity = Vsite.Domain.Website.Entities.Website;</c>
/// (cùng cách <c>ShopEntity</c>).
///
/// Chưa có (ngoài phạm vi 5a, thêm additive sau): <c>CurrentPublicationId</c> (8b), <c>SeedTemplateId</c> (Bước 11).
///
/// Mã locale có nằm trong <c>config/locales.json</c> hay không là việc của Application (catalog); entity
/// chỉ giữ invariant <c>DefaultLocale ∈ Locales</c> (cũng là CHECK ở DB).
/// </summary>
public sealed class Website : TenantAuditableEntity
{
    public const int MaxLocaleLength = 10;
    public const int MaxSeoTitleLength = 160;
    public const int MaxSeoDescriptionLength = 320;

    // Backing field riêng — EF map thẳng cột `locales text[]` (xem WebsiteConfiguration).
    private List<string> _locales = [];

    private Website()
    {
        DefaultLocale = null!;
        TrackingSnippets = "{}";
    }

    private Website(Guid id) : base(id)
    {
        DefaultLocale = null!;
        TrackingSnippets = "{}";
    }

    /// <summary>Website mới ở trạng thái <see cref="WebsiteStatus.Draft"/>, mặc định <c>vi</c>/<c>{vi}</c>.</summary>
    public static Website Create(
        Guid shopId,
        string defaultLocale = "vi",
        IReadOnlyList<string>? locales = null,
        Guid? id = null)
    {
        var website = new Website(id ?? Guid.NewGuid()) { ShopId = shopId };
        website.SetLocales(locales ?? [defaultLocale], defaultLocale);
        return website;
    }

    public WebsiteStatus Status { get; private set; } = WebsiteStatus.Draft;

    public string DefaultLocale { get; private set; }

    /// <summary>Ngôn ngữ đang bật (không rỗng, không trùng, chứa <see cref="DefaultLocale"/>).</summary>
    public IReadOnlyList<string> Locales => _locales;

    /// <summary>Bản của <see cref="DefaultLocale"/>; fallback khi <c>Page.SeoTitle</c> rỗng.</summary>
    public string? DefaultSeoTitle { get; private set; }

    public string? DefaultSeoDescription { get; private set; }

    /// <summary>Bản Library. FK ghép <c>(DefaultOgImageId, ShopId) → media_asset (Id, ShopId)</c> (#76).</summary>
    public Guid? DefaultOgImageId { get; private set; }

    /// <summary>Bản Library. FK ghép như trên (#76).</summary>
    public Guid? FaviconAssetId { get; private set; }

    /// <summary>JSON <c>{ ga4?, metaPixel? }</c> (jsonb). Whitelist ID — validate shape/regex ở Application
    /// (chưa có API sửa trong 5a); entity chỉ chặn rỗng.</summary>
    public string TrackingSnippets { get; private set; }

    /// <summary>Đổi tập locale và locale mặc định cùng lúc để invariant <c>DefaultLocale ∈ Locales</c> không bao
    /// giờ bị vi phạm giữa chừng. Lỗi → trạng thái cũ giữ nguyên.</summary>
    public void SetLocales(IReadOnlyList<string> locales, string defaultLocale)
    {
        if (locales is null || locales.Count == 0
            || locales.Any(l => string.IsNullOrWhiteSpace(l) || l.Length > MaxLocaleLength)
            || locales.Distinct(StringComparer.OrdinalIgnoreCase).Count() != locales.Count)
        {
            throw new DomainException(
                "WEBSITE_LOCALES_INVALID",
                "Danh sách ngôn ngữ không được rỗng, không trùng, mã không rỗng và tối đa 10 ký tự.");
        }

        if (!locales.Contains(defaultLocale, StringComparer.Ordinal))
        {
            throw new DomainException(
                "WEBSITE_DEFAULT_LOCALE_NOT_ENABLED",
                "Ngôn ngữ mặc định phải nằm trong danh sách ngôn ngữ đang bật.");
        }

        _locales = [.. locales];
        DefaultLocale = defaultLocale;
    }

    public void SetSeoDefaults(string? title, string? description)
    {
        if ((title?.Length ?? 0) > MaxSeoTitleLength || (description?.Length ?? 0) > MaxSeoDescriptionLength)
        {
            throw new DomainException(
                "WEBSITE_SEO_TOO_LONG",
                $"SEO title tối đa {MaxSeoTitleLength} ký tự, description tối đa {MaxSeoDescriptionLength} ký tự.");
        }

        DefaultSeoTitle = title;
        DefaultSeoDescription = description;
    }

    public void SetDefaultOgImage(Guid? assetId) => DefaultOgImageId = assetId;

    public void SetFavicon(Guid? assetId) => FaviconAssetId = assetId;

    public void SetTrackingSnippets(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new DomainException("WEBSITE_TRACKING_INVALID", "TrackingSnippets không được rỗng (dùng \"{}\").");
        }

        TrackingSnippets = json;
    }
}
