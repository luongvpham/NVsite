using System.Text.RegularExpressions;
using Vsite.Domain.Common;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Website.Enums;

namespace Vsite.Domain.Website.Entities;

/// <summary>
/// `05` §4 — metadata của một trang. Hai loại (#33): <see cref="PageKind.Composable"/> (shop dựng bằng cây
/// component, <c>SystemType = null</c>) và <see cref="PageKind.System"/> (vsite định nghĩa, <c>SystemType ≠ null</c>).
/// Chỉ tạo qua <see cref="NewComposable"/>/<see cref="NewSystem"/> nên không dựng được tổ hợp Kind/SystemType sai.
///
/// <c>Slug</c>: <c>""</c> = trang chủ (chỉ <c>Composable</c>); <c>null</c> ⟺ <c>SystemType = NotFound</c> (không có
/// route công khai — lệch `05` §4, plan §4); còn lại khớp <c>^[a-z0-9]+(-[a-z0-9]+)*$</c>, ≤120. Slug có trùng
/// locale / route System / <c>reservedShopSitePaths</c> hay không là việc của Application (T8, SlugRules) vì cần catalog.
/// </summary>
public sealed partial class Page : TenantAuditableEntity
{
    public const int MaxSlugLength = 120;
    public const int MaxTitleLength = 160;
    public const int MaxSeoTitleLength = 160;
    public const int MaxSeoDescriptionLength = 320;

    [GeneratedRegex(@"\A[a-z0-9]+(-[a-z0-9]+)*\z")]
    private static partial Regex SlugPattern();

    private Page()
    {
        Title = null!;
    }

    private Page(Guid id) : base(id)
    {
        Title = null!;
    }

    /// <summary>Trang shop tự dựng. <paramref name="slug"/> <c>""</c> = trang chủ.</summary>
    public static Page NewComposable(Guid shopId, Guid websiteId, string slug, string title, int sortOrder, Guid? id = null)
    {
        ValidateSlug(slug, allowEmpty: true, allowNull: false);
        ValidateTitle(title);

        return new Page(id ?? Guid.NewGuid())
        {
            ShopId = shopId,
            WebsiteId = websiteId,
            Kind = PageKind.Composable,
            SystemType = null,
            Slug = slug,
            Title = title,
            SortOrder = sortOrder,
        };
    }

    /// <summary>Trang hệ thống. <see cref="SystemType.NotFound"/> bắt buộc <paramref name="slug"/> = <c>null</c> (và
    /// <c>NoIndex = true</c>); các loại khác bắt buộc slug khác rỗng.</summary>
    public static Page NewSystem(Guid shopId, Guid websiteId, SystemType systemType, string? slug, string title, int sortOrder, Guid? id = null)
    {
        if (systemType == Enums.SystemType.NotFound)
        {
            if (slug is not null)
            {
                throw SlugInvalid("Trang NotFound không có route công khai: Slug phải là null.");
            }
        }
        else
        {
            ValidateSlug(slug, allowEmpty: false, allowNull: false);
        }

        ValidateTitle(title);

        return new Page(id ?? Guid.NewGuid())
        {
            ShopId = shopId,
            WebsiteId = websiteId,
            Kind = PageKind.System,
            SystemType = systemType,
            Slug = slug,
            Title = title,
            SortOrder = sortOrder,
            NoIndex = systemType == Enums.SystemType.NotFound,
        };
    }

    public Guid WebsiteId { get; private init; }

    public PageKind Kind { get; private set; }

    public SystemType? SystemType { get; private set; }

    /// <summary>Bản của <c>Website.DefaultLocale</c>. Xem doc của class về <c>""</c> / <c>null</c>.</summary>
    public string? Slug { get; private set; }

    public string Title { get; private set; }

    public string? SeoTitle { get; private set; }

    public string? SeoDescription { get; private set; }

    /// <summary>Bản Library. FK ghép <c>(OgImageId, ShopId) → media_asset (Id, ShopId)</c> (#76, #92).</summary>
    public Guid? OgImageId { get; private set; }

    public bool NoIndex { get; private set; }

    /// <summary>Tắt = không render, không lên menu, giữ nguyên dữ liệu.</summary>
    public bool IsEnabled { get; private set; } = true;

    /// <summary>Thứ tự trong danh sách quản lý (không phải thứ tự menu). Server cấp (G12).</summary>
    public int SortOrder { get; private set; }

    /// <summary>Trang chủ = trang Composable có Slug <c>""</c> (không có cột <c>IsHomePage</c>, `05` §4).</summary>
    public bool IsHomePage => Kind == PageKind.Composable && Slug == "";

    /// <summary>Chỉ trang Composable không phải trang chủ mới xoá được (`05` §4).</summary>
    public bool CanBeDeleted => Kind == PageKind.Composable && !IsHomePage;

    public void Rename(string title)
    {
        ValidateTitle(title);
        Title = title;
    }

    /// <summary>Chỉ trang Composable đổi slug được — route của trang System cố định.</summary>
    public void ChangeSlug(string slug)
    {
        if (Kind == PageKind.System)
        {
            throw new DomainException("PAGE_SYSTEM_SLUG_FIXED", "Không đổi được slug của trang hệ thống.");
        }

        if (IsHomePage)
        {
            throw new DomainException("PAGE_HOME_SLUG_FIXED", "Không đổi được slug của trang chủ.");
        }

        if (slug == "")
        {
            throw new DomainException("PAGE_HOME_SLUG_FIXED", "Slug rỗng chỉ dành cho trang chủ.");
        }

        ValidateSlug(slug, allowEmpty: false, allowNull: false);
        Slug = slug;
    }

    public void SetSeo(string? seoTitle, string? seoDescription)
    {
        if ((seoTitle?.Length ?? 0) > MaxSeoTitleLength || (seoDescription?.Length ?? 0) > MaxSeoDescriptionLength)
        {
            throw new DomainException(
                "PAGE_SEO_TOO_LONG",
                $"SEO title tối đa {MaxSeoTitleLength} ký tự, description tối đa {MaxSeoDescriptionLength} ký tự.");
        }

        SeoTitle = seoTitle;
        SeoDescription = seoDescription;
    }

    public void SetOgImage(Guid? assetId) => OgImageId = assetId;

    public void SetNoIndex(bool noIndex)
    {
        if (!noIndex && SystemType == Enums.SystemType.NotFound)
        {
            throw new DomainException("PAGE_NOINDEX_REQUIRED", "Trang NotFound luôn NoIndex.");
        }

        NoIndex = noIndex;
    }

    public void SetEnabled(bool enabled) => IsEnabled = enabled;

    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;

    /// <summary>Xoá mềm — GỌI method này (lớp chặn thứ hai sau handler, trả 409 <c>PAGE_NOT_DELETABLE</c>); không
    /// <c>Remove()</c> để khỏi qua cascade fix-up của EF. <c>DeletedAt</c> do <c>AppDbContext</c> đóng dấu.</summary>
    public void SoftDelete()
    {
        if (!CanBeDeleted)
        {
            throw new DomainException("PAGE_NOT_DELETABLE", "Chỉ xoá được trang Composable không phải trang chủ.");
        }

        IsDeleted = true;
    }

    private static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > MaxTitleLength)
        {
            throw new DomainException("PAGE_TITLE_INVALID", $"Title bắt buộc, tối đa {MaxTitleLength} ký tự.");
        }
    }

    private static void ValidateSlug(string? slug, bool allowEmpty, bool allowNull)
    {
        if (slug is null)
        {
            if (!allowNull)
            {
                throw SlugInvalid("Slug không được null (trừ trang NotFound).");
            }

            return;
        }

        if (slug.Length == 0)
        {
            if (!allowEmpty)
            {
                throw SlugInvalid("Slug rỗng chỉ dành cho trang chủ (Composable).");
            }

            return;
        }

        if (slug.Length > MaxSlugLength || !SlugPattern().IsMatch(slug))
        {
            throw SlugInvalid($"Slug phải khớp ^[a-z0-9]+(-[a-z0-9]+)*$ và tối đa {MaxSlugLength} ký tự.");
        }
    }

    private static DomainException SlugInvalid(string message) => new("PAGE_SLUG_INVALID", message);
}
