using System.Security.Cryptography;
using System.Text.Json;
using Vsite.Application.Website.Interfaces;
using Vsite.Application.Website.Validation;
using Vsite.Domain.Website.Entities;
using Vsite.Domain.Website.Enums;

namespace Vsite.Application.Website.Seeding;

/// <summary>Id node do server sinh (plan T6): <c>c_</c> + 5 ký tự a-z0-9 (khớp pattern vỏ <c>^c_[A-Za-z0-9_-]{5,}$</c>).</summary>
public static class NodeIds
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    public static string New() => "c_" + RandomNumberGenerator.GetString(Alphabet, 5);
}

public sealed record SeededPages(IReadOnlyList<Page> Pages, IReadOnlyList<PageDraft> Drafts);

/// <summary>
/// Seed trang cho website (T6, WEBSITE-001, #46, #98). Mọi cây seed đều chạy qua <see cref="PageTreeValidator"/> đúng
/// <see cref="PageKind"/> trước khi lưu — seed sai là lỗi lập trình (<see cref="InvalidOperationException"/>), không phải lỗi người dùng.
/// <see cref="EnsureSystemPages"/> idempotent: chỉ thêm loại <c>seedOnCreate</c> còn thiếu, không đụng loại đã có (dùng lại khi sau này
/// thêm System page mới). <c>SortOrder</c> do server cấp, tăng dần (G12); draft <c>IsDirty = true</c> ngay từ seed (G11).
/// </summary>
public sealed class WebsiteSeeder(ISystemPageCatalog catalog, PageTreeValidator validator)
{
    private static readonly IReadOnlyDictionary<SystemType, (string Title, string Content)> Defaults =
        new Dictionary<SystemType, (string, string)>
        {
            [SystemType.Contact] = (
                "Liên hệ",
                "<p>Hãy liên hệ với chúng tôi để được tư vấn. Cập nhật địa chỉ, số điện thoại và giờ mở cửa của bạn tại đây.</p>"),
            [SystemType.NotFound] = (
                "Không tìm thấy trang",
                "<p>Trang bạn tìm không tồn tại hoặc đã được chuyển đi. Vui lòng quay lại trang chủ.</p>"),
        };

    /// <summary>Cây rỗng hợp lệ (gốc Page, không con) — trang chủ và mọi trang Composable mới.</summary>
    public string EmptyTree() => Validated("""{"schemaVersion":1,"id":"root","type":"Page","children":[]}""", PageKind.Composable);

    /// <summary>Trang chủ: Composable, <c>Slug = ""</c>, cây rỗng.</summary>
    public (Page Page, PageDraft Draft) CreateHome(Guid shopId, Guid websiteId, int sortOrder)
    {
        var page = Page.NewComposable(shopId, websiteId, "", "Trang chủ", sortOrder);
        return (page, PageDraft.Create(shopId, page.Id, EmptyTree()));
    }

    /// <summary>Thêm các System page <c>seedOnCreate</c> mà <paramref name="existing"/> chưa có; trang thứ i nhận <c>startSortOrder + i</c>.</summary>
    public SeededPages EnsureSystemPages(
        Guid shopId, Guid websiteId, IReadOnlyCollection<SystemType> existing, int startSortOrder)
    {
        var pages = new List<Page>();
        var drafts = new List<PageDraft>();
        var sortOrder = startSortOrder;

        foreach (var type in Enum.GetValues<SystemType>()) // thứ tự enum: ổn định, không phụ thuộc thứ tự khoá trong file
        {
            if (existing.Contains(type)
                || !catalog.Pages.TryGetValue(type.ToString(), out var definition)
                || !definition.SeedOnCreate)
            {
                continue;
            }

            var (title, content) = Defaults.TryGetValue(type, out var known)
                ? known
                : (type.ToString(), "<p>Nội dung đang được cập nhật.</p>"); // loại tương lai chưa có nội dung riêng
            var slug = definition.Routes.Count > 0 ? definition.Routes[0] : null;

            var page = Page.NewSystem(shopId, websiteId, type, slug, title, sortOrder++);
            var tree = Validated(
                "{\"schemaVersion\":1,\"id\":\"root\",\"type\":\"Page\",\"children\":[{\"id\":\"" + NodeIds.New()
                + "\",\"type\":\"RichText\",\"variant\":\"RichText01\",\"props\":{\"content\":" + JsonSerializer.Serialize(content) + "}}]}",
                PageKind.System);

            pages.Add(page);
            drafts.Add(PageDraft.Create(shopId, page.Id, tree));
        }

        return new SeededPages(pages, drafts);
    }

    private string Validated(string tree, PageKind kind)
    {
        var result = validator.Validate(tree, kind);
        return result.IsValid
            ? result.CleanTree!
            : throw new InvalidOperationException(
                "Cây seed không qua PageTreeValidator: " + string.Join("; ", result.Errors.Select(e => $"{e.Path} {e.Code}")));
    }
}
