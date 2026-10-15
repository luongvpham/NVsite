using Vsite.Domain.Common;
using Vsite.Domain.Exceptions;

namespace Vsite.Domain.Website.Entities;

/// <summary>
/// `05` §5 — bản đang sửa của một trang (1:1). PK = <c>PageId</c> (bí danh của <c>Id</c> base, cột
/// <c>page_id</c>). Tách khỏi <see cref="Page"/> vì tree là cột nặng.
///
/// <see cref="Tree"/> là chuỗi JSON (jsonb); validate toàn cây ở Application (T4), không ở entity.
/// <see cref="Version"/> ánh xạ cột hệ thống Postgres <c>xmin</c> làm concurrency token (Npgsql
/// <c>IsRowVersion</c>) — chỉ đọc, DB tự đổi mỗi lần UPDATE; đi trong contract (`GET` trả, `PUT` gửi lại).
/// </summary>
public sealed class PageDraft : TenantAuditableEntity
{
    private PageDraft()
    {
        Tree = null!;
    }

    private PageDraft(Guid pageId) : base(pageId)
    {
        Tree = null!;
    }

    /// <summary>Draft mới luôn <c>IsDirty = true</c> (G11): chưa có publish nên "khác bản publish" luôn đúng.</summary>
    public static PageDraft Create(Guid shopId, Guid pageId, string treeJson)
    {
        var draft = new PageDraft(pageId) { ShopId = shopId };
        draft.Replace(treeJson);
        return draft;
    }

    /// <summary>Bí danh của <see cref="BaseEntity.Id"/> — draft là 1:1 với page. ⚠️ Chỉ để đọc trong code
    /// nghiệp vụ; KHÔNG dùng trong LINQ-to-EF (không map) — query theo <c>d.Id</c>.</summary>
    public Guid PageId => Id;

    public string Tree { get; private set; }

    /// <summary><c>true</c> khi khác bản đã publish gần nhất; 5a đặt <c>true</c> ngay từ seed và sau mỗi lần ghi (G11).</summary>
    public bool IsDirty { get; private set; }

    /// <summary>Giá trị <c>xmin</c> lúc đọc. Opaque với FE (G5). Không set được từ code nghiệp vụ.</summary>
    public uint Version { get; private set; }

    /// <summary>Ghi đè toàn bộ tree (autosave không patch từng phần).</summary>
    public void Replace(string treeJson)
    {
        if (string.IsNullOrWhiteSpace(treeJson))
        {
            throw new DomainException("PAGE_DRAFT_TREE_REQUIRED", "Tree của draft không được rỗng.");
        }

        Tree = treeJson;
        IsDirty = true;
    }
}
