using Vsite.Domain.Common;
using Vsite.Domain.Exceptions;

namespace Vsite.Domain.Website.Entities;

/// <summary>
/// WEBSITE-007 — menu của website (1:1). PK = <c>WebsiteId</c> (bí danh của <c>Id</c> base, cột <c>website_id</c>).
///
/// <see cref="Items"/> là chuỗi JSON (jsonb) dạng tài liệu <c>{ "schemaVersion": 1, "items": [...] }</c> (#94); shape/validate
/// ở Application, không ở entity. <see cref="Version"/> ánh xạ cột hệ thống <c>xmin</c> làm concurrency token (khuôn
/// <see cref="PageDraft"/>).
/// </summary>
public sealed class NavigationConfig : TenantAuditableEntity
{
    private NavigationConfig()
    {
        Items = null!;
    }

    private NavigationConfig(Guid websiteId) : base(websiteId)
    {
        Items = null!;
    }

    /// <summary>Menu mới luôn <c>IsDirty = true</c>: chưa có publish nào chứa nó (như <see cref="PageDraft"/>).</summary>
    public static NavigationConfig Create(Guid websiteId, Guid shopId, string itemsJson)
    {
        var nav = new NavigationConfig(websiteId) { ShopId = shopId };
        nav.Replace(itemsJson);
        return nav;
    }

    /// <summary>Bí danh của <see cref="BaseEntity.Id"/> — menu là 1:1 với website. ⚠️ Chỉ để đọc trong code
    /// nghiệp vụ; KHÔNG dùng trong LINQ-to-EF (không map) — query theo <c>n.Id</c>.</summary>
    public Guid WebsiteId => Id;

    /// <summary>Tài liệu menu (jsonb), có <c>schemaVersion</c> ở gốc.</summary>
    public string Items { get; private set; }

    /// <summary><c>true</c> khi khác bản đã publish gần nhất.</summary>
    public bool IsDirty { get; private set; }

    /// <summary>Giá trị <c>xmin</c> lúc đọc. Không set được từ code nghiệp vụ.</summary>
    public uint Version { get; private set; }

    /// <summary>Ghi đè toàn bộ tài liệu menu (không patch từng phần).</summary>
    public void Replace(string itemsJson)
    {
        if (string.IsNullOrWhiteSpace(itemsJson))
        {
            throw new DomainException("NAVIGATION_ITEMS_REQUIRED", "Tài liệu menu không được rỗng.");
        }

        Items = itemsJson;
        IsDirty = true;
    }

    /// <summary>
    /// Job dọn mục mồ côi (WEBSITE-008, #118): đổi <see cref="Items"/> nhưng KHÔNG đổi <see cref="IsDirty"/> — snapshot công khai
    /// vốn đã lọc mục trỏ trang không còn (<c>ForSnapshot</c>) nên bản publish không khác đi.
    /// </summary>
    public void PruneOrphans(string itemsJson)
    {
        if (string.IsNullOrWhiteSpace(itemsJson))
        {
            throw new DomainException("NAVIGATION_ITEMS_REQUIRED", "Tài liệu menu không được rỗng.");
        }

        Items = itemsJson;
    }

    /// <summary>Publish: menu giờ khớp bản đang chạy → <c>IsDirty = false</c>. Không đụng <see cref="Items"/>.</summary>
    public void MarkPublished() => IsDirty = false;

    /// <summary>Rollback: menu giờ khác bản đang chạy → <c>IsDirty = true</c>. Không đụng <see cref="Items"/>.</summary>
    public void MarkDirty() => IsDirty = true;
}
