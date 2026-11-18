using Vsite.Domain.Common;
using Vsite.Domain.Exceptions;

namespace Vsite.Domain.Website.Entities;

/// <summary>
/// `05` §7 (8b) — một bản xuất bản **bất biến** của toàn website. Không có setter công khai, không soft-delete,
/// không audit trail (<see cref="TenantEntity"/>): bản đã ghi là bất biến; job prune 20 bản (8d) xoá cứng.
///
/// <see cref="Snapshot"/> là chuỗi JSON (jsonb) đóng băng toàn site tại thời điểm publish (shape ở
/// <c>Docs/tasks/WEBSITE-004/plan.md</c> §4). Đọc công khai chỉ chạm bảng này, không bao giờ chạm
/// <c>page_draft</c>/<c>page</c>/<c>theme</c>.
///
/// Ràng buộc ở DB (xem <c>SitePublicationConfiguration</c>): UNIQUE <c>(website_id, version)</c>; alternate key
/// <c>(id, website_id)</c> là đích FK ghép của <c>website.current_publication_id</c> (#92); FK ghép
/// <c>(website_id, shop_id) → website (id, shop_id)</c> chặn trỏ sang shop khác.
/// </summary>
public sealed class SitePublication : TenantEntity
{
    public const int MaxNoteLength = 200;

    private SitePublication()
    {
        Snapshot = null!;
    }

    private SitePublication(Guid id) : base(id)
    {
        Snapshot = null!;
    }

    public static SitePublication Create(
        Guid shopId,
        Guid websiteId,
        int version,
        string snapshotJson,
        string? note,
        Guid? publishedByUserId,
        DateTimeOffset publishedAt,
        Guid? id = null)
    {
        if (version <= 0)
        {
            throw new DomainException("SITE_PUBLICATION_VERSION_INVALID", "Version của bản publish phải là số dương.");
        }

        if (string.IsNullOrWhiteSpace(snapshotJson))
        {
            throw new DomainException("SITE_PUBLICATION_SNAPSHOT_REQUIRED", "Snapshot của bản publish không được rỗng.");
        }

        if ((note?.Length ?? 0) > MaxNoteLength)
        {
            throw new DomainException("SITE_PUBLICATION_NOTE_TOO_LONG", $"Ghi chú tối đa {MaxNoteLength} ký tự.");
        }

        return new SitePublication(id ?? Guid.NewGuid())
        {
            ShopId = shopId,
            WebsiteId = websiteId,
            Version = version,
            Snapshot = snapshotJson,
            Note = note,
            PublishedByUserId = publishedByUserId,
            PublishedAt = publishedAt,
        };
    }

    public Guid WebsiteId { get; private set; }

    public int Version { get; private set; }

    /// <summary>Snapshot bất biến (jsonb). Đọc công khai trả nguyên văn chuỗi này.</summary>
    public string Snapshot { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset PublishedAt { get; private set; }

    /// <summary>Người bấm publish (<see cref="Vsite.Domain.Common.IShopScoped"/> không đủ — cần user cụ thể).
    /// NULL khi publish ngoài ngữ cảnh HTTP (test, job). FK <c>app_user</c> <c>Restrict</c>.</summary>
    public Guid? PublishedByUserId { get; private set; }
}
