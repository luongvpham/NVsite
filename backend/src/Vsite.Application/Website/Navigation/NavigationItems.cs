using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Vsite.Application.Website.Validation;
using Vsite.Application.Website.Versioning;
using Vsite.Domain.Website.Enums;

namespace Vsite.Application.Website.Navigation;

/// <summary>
/// Một mục menu (WEBSITE-007, #113). <see cref="Key"/> là <c>"page:{pageId}"</c> hoặc <c>"custom"</c> (chuỗi, không enum — #19).
/// <see cref="Label"/> null = dùng tên trang (<c>custom</c> bắt buộc có). <see cref="Url"/> chỉ có ở <c>custom</c>.
/// <see cref="Children"/> lồng tối đa 3 cấp (cấp 1 → con → cháu); mục cấp 3 có <c>Children</c> rỗng.
/// </summary>
public sealed record NavItem(string Key, string? Label, string? Url, bool Hidden, IReadOnlyList<NavItem> Children)
{
    public const string CustomKey = "custom";
    public const string PageKeyPrefix = "page:";

    public static string PageKey(Guid pageId) => PageKeyPrefix + pageId.ToString("D");

    public bool IsCustom => Key == CustomKey;

    /// <summary>Id trang nếu <see cref="Key"/> là <c>page:{guid}</c> hợp lệ.</summary>
    public Guid? PageId =>
        Key.StartsWith(PageKeyPrefix, StringComparison.Ordinal)
        && Guid.TryParseExact(Key.AsSpan(PageKeyPrefix.Length), "D", out var id)
            ? id
            : null;
}

/// <summary>Đầu vào thuần của Merge/Validate: các trang CHƯA XOÁ của website (caller lọc, trong câu query theo tenant).</summary>
public sealed record NavPage(Guid Id, string Title, SystemType? SystemType, bool IsEnabled, int SortOrder);

/// <summary><paramref name="Path"/> là JSON Pointer vào body PUT, vd. <c>/items/2/children/0/url</c>.</summary>
public sealed record NavError(string Path, string Code, string Message);

public static class NavigationErrorCodes
{
    /// <summary>Mã cấp ProblemDetails của 422 khi lưu menu (khuôn <c>THEME_INVALID</c>); lỗi từng mục ở <c>params.errors[]</c>.</summary>
    public const string Invalid = "NAVIGATION_INVALID";

    public const string TooDeep = "NAV_TOO_DEEP";
    public const string PageNotFound = "NAV_PAGE_NOT_FOUND";
    public const string PageDuplicate = "NAV_PAGE_DUPLICATE";
    public const string PageNotAllowed = "NAV_PAGE_NOT_ALLOWED";
    public const string UrlUnsafe = "NAV_URL_UNSAFE";
    public const string LabelRequired = "NAV_LABEL_REQUIRED";
    public const string LabelTooLong = "NAV_LABEL_TOO_LONG";
    public const string TooManyItems = "NAV_TOO_MANY_ITEMS";
}

/// <summary>
/// Hàm thuần của menu shop website (WEBSITE-007): ghép (Flow 1, 3), kiểm (Flow 2), lọc cho snapshot (Flow 4) và
/// đọc/ghi tài liệu <c>{ schemaVersion, items }</c> của <c>NavigationConfig.Items</c>.
/// </summary>
public static class NavigationItems
{
    public const int MaxTopLevel = 12;
    public const int MaxChildren = 10;
    public const int MaxDepth = 3;
    public const int MaxLabelLength = 60;
    public const int MaxUrlLength = 2048;

    private static readonly IReadOnlyList<NavItem> NoChildren = [];

    /// <summary>
    /// Ghép menu đã lưu với danh sách trang hiện có. <paramref name="stored"/> null ⇒ tự sinh (mọi trang trừ
    /// <c>NotFound</c>, theo <c>SortOrder</c> rồi <c>Title</c>, không ẩn). Ngược lại: bỏ mục trỏ trang không còn (kèm con
    /// nếu là cha), rồi nối trang chưa có vào cuối cấp 1 với <c>hidden</c>.
    /// </summary>
    public static IReadOnlyList<NavItem> Merge(IReadOnlyList<NavItem>? stored, IReadOnlyList<NavPage> pages)
    {
        var ordered = pages
            .Where(p => p.SystemType != SystemType.NotFound)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Title, StringComparer.Ordinal)
            .ToList();

        if (stored is null)
        {
            return ordered.Select(p => PageItem(p.Id, hidden: false)).ToList();
        }

        var existing = pages.Select(p => p.Id).ToHashSet();
        var inMenu = new HashSet<Guid>();
        var result = new List<NavItem>();
        foreach (var item in stored)
        {
            if (MergeItem(item, existing, inMenu, depth: 1) is { } merged)
            {
                result.Add(merged);
            }
        }

        foreach (var p in ordered.Where(p => !inMenu.Contains(p.Id)))
        {
            result.Add(PageItem(p.Id, hidden: true));
        }

        return result;
    }

    /// <summary>Trim nhãn/url; nhãn rỗng sau trim ⇒ null; <c>page</c> không mang url. Dùng TRƯỚC khi lưu (R4).</summary>
    public static IReadOnlyList<NavItem> Normalize(IReadOnlyList<NavItem> items) =>
        items.Select(NormalizeItem).ToList();

    /// <summary>Kiểm menu (đã hoặc chưa <see cref="Normalize"/>) — rỗng nghĩa là hợp lệ. Thứ tự lỗi theo thứ tự duyệt.</summary>
    public static IReadOnlyList<NavError> Validate(IReadOnlyList<NavItem> items, IReadOnlyList<NavPage> pages)
    {
        var errors = new List<NavError>();
        var byId = pages.ToDictionary(p => p.Id);
        var seen = new HashSet<Guid>();

        if (items.Count > MaxTopLevel)
        {
            errors.Add(new NavError("/items", NavigationErrorCodes.TooManyItems, $"Tối đa {MaxTopLevel} mục ở cấp 1."));
        }

        ValidateLevel(items, "/items", depth: 1, byId, seen, errors);
        return errors;
    }

    private static void ValidateLevel(
        IReadOnlyList<NavItem> items, string basePath, int depth, Dictionary<Guid, NavPage> byId, HashSet<Guid> seen, List<NavError> errors)
    {
        for (var i = 0; i < items.Count; i++)
        {
            var path = $"{basePath}/{i.ToString(CultureInfo.InvariantCulture)}";
            ValidateOne(NormalizeOne(items[i]), path, byId, seen, errors);

            var children = items[i].Children;
            if (depth >= MaxDepth)
            {
                if (children.Count > 0)
                {
                    errors.Add(new NavError(path + "/children", NavigationErrorCodes.TooDeep, $"Menu chỉ lồng tối đa {MaxDepth} cấp."));
                }

                continue;
            }

            if (children.Count > MaxChildren)
            {
                errors.Add(new NavError(path + "/children", NavigationErrorCodes.TooManyItems, $"Tối đa {MaxChildren} mục con mỗi mục."));
            }

            ValidateLevel(children, path + "/children", depth + 1, byId, seen, errors);
        }
    }

    /// <summary>
    /// Mục đưa vào snapshot công khai: bỏ <c>hidden</c>, bỏ mục trỏ trang không có trong <paramref name="publishedPageIds"/>
    /// (tắt/xoá); cha bị bỏ ⇒ bỏ cả nhóm (R5). Nhãn/url giữ nguyên.
    /// </summary>
    public static IReadOnlyList<NavItem> ForSnapshot(IReadOnlyList<NavItem> items, IReadOnlySet<Guid> publishedPageIds)
    {
        return items
            .Where(i => Visible(i, publishedPageIds))
            .Select(i => i with { Children = ForSnapshot(i.Children, publishedPageIds) })
            .ToList();
    }

    /// <summary>Đọc <c>NavigationConfig.Items</c> (nâng schemaVersion nếu cần). Dữ liệu do chính ta ghi nên hỏng ⇒ ném.</summary>
    public static IReadOnlyList<NavItem> Parse(string json, DocumentVersioning versioning)
    {
        var upcast = versioning.Upcast(DocumentKind.Navigation, JsonNode.Parse(json));
        if (upcast.Document is not { } doc)
        {
            throw new InvalidOperationException($"Tài liệu menu không đọc được ({upcast.Status}): {upcast.Message}");
        }

        if (doc["items"] is not JsonArray arr)
        {
            throw new InvalidOperationException("Tài liệu menu thiếu mảng 'items'.");
        }

        return ReadArray(arr, depth: 1);
    }

    /// <summary>Ghi tài liệu <c>{ schemaVersion, items }</c> (camelCase) ở schemaVersion hiện hành.</summary>
    public static string Serialize(IReadOnlyList<NavItem> items, DocumentVersioning versioning)
    {
        var doc = new JsonObject
        {
            ["schemaVersion"] = versioning.Current(DocumentKind.Navigation),
            ["items"] = ToJsonArray(items),
        };
        return doc.ToJsonString();
    }

    /// <summary>
    /// Mảng <c>items</c> cho <c>snapshot.navigation.items</c> (T4): <c>{ key, label, url?, children? }</c> — KHÔNG có
    /// <c>hidden</c> (mục ẩn đã bị <see cref="ForSnapshot"/> loại); <c>children</c> bị bỏ khi rỗng; <c>label</c> null vẫn ghi.
    /// </summary>
    public static JsonArray ToSnapshotJsonArray(IReadOnlyList<NavItem> items)
    {
        var arr = new JsonArray();
        foreach (var item in items)
        {
            arr.Add(ToJson(item, depth: 1, forSnapshot: true));
        }

        return arr;
    }

    /// <summary>Mảng <c>items</c> của tài liệu lưu (có <c>hidden</c>, <c>children</c> luôn có ở cấp 1). Snapshot dùng <see cref="ToSnapshotJsonArray"/>.</summary>
    public static JsonArray ToJsonArray(IReadOnlyList<NavItem> items)
    {
        var arr = new JsonArray();
        foreach (var item in items)
        {
            arr.Add(ToJson(item, depth: 1));
        }

        return arr;
    }

    private static NavItem PageItem(Guid id, bool hidden) => new(NavItem.PageKey(id), null, null, hidden, NoChildren);

    private static bool IsLive(NavItem item, HashSet<Guid> existing) =>
        item.IsCustom || (item.PageId is { } id && existing.Contains(id));

    private static void Track(NavItem item, HashSet<Guid> inMenu)
    {
        if (item.PageId is { } id)
        {
            inMenu.Add(id);
        }
    }

    private static bool Visible(NavItem item, IReadOnlySet<Guid> published) =>
        !item.Hidden && (item.IsCustom || (item.PageId is { } id && published.Contains(id)));

    /// <summary>Đệ quy mọi cấp: bỏ mục (cùng cả cây con) trỏ trang không còn; ghi nhận trang còn trong menu.</summary>
    private static NavItem? MergeItem(NavItem item, HashSet<Guid> existing, HashSet<Guid> inMenu, int depth)
    {
        if (!IsLive(item, existing))
        {
            return null;
        }

        Track(item, inMenu);
        var children = new List<NavItem>();
        if (depth < MaxDepth)
        {
            foreach (var c in item.Children)
            {
                if (MergeItem(c, existing, inMenu, depth + 1) is { } m)
                {
                    children.Add(m);
                }
            }
        }

        // Cấp sâu hơn MaxDepth (chỉ có thể do dữ liệu lưu hỏng) bị bỏ khi đọc: Children của cấp 3 luôn rỗng.
        return item with { Children = children };
    }

    private static NavItem NormalizeItem(NavItem item) => NormalizeOne(item) with { Children = item.Children.Select(NormalizeItem).ToList() };

    private static NavItem NormalizeOne(NavItem item)
    {
        var label = item.Label?.Trim();
        if (string.IsNullOrEmpty(label))
        {
            label = null;
        }

        return item with { Label = label, Url = item.IsCustom ? item.Url?.Trim() : null };
    }

    private static void ValidateOne(NavItem item, string path, Dictionary<Guid, NavPage> byId, HashSet<Guid> seen, List<NavError> errors)
    {
        if (item.IsCustom)
        {
            if (item.Label is null)
            {
                errors.Add(new NavError(path + "/label", NavigationErrorCodes.LabelRequired, "Mục liên kết tuỳ chỉnh bắt buộc có nhãn."));
            }

            if (item.Url is null || item.Url.Length > MaxUrlLength || !LinkUrlRules.IsSafe(item.Url))
            {
                errors.Add(new NavError(path + "/url", NavigationErrorCodes.UrlUnsafe, "Liên kết không hợp lệ hoặc không an toàn."));
            }
        }
        else if (item.PageId is not { } id || !byId.TryGetValue(id, out var page))
        {
            errors.Add(new NavError(path + "/key", NavigationErrorCodes.PageNotFound, "Trang không tồn tại."));
        }
        else if (page.SystemType == SystemType.NotFound)
        {
            errors.Add(new NavError(path + "/key", NavigationErrorCodes.PageNotAllowed, "Trang này không đưa vào menu được."));
        }
        else if (!seen.Add(id))
        {
            errors.Add(new NavError(path + "/key", NavigationErrorCodes.PageDuplicate, "Mỗi trang chỉ xuất hiện một lần trong menu."));
        }

        if (item.Label is { Length: > MaxLabelLength })
        {
            errors.Add(new NavError(path + "/label", NavigationErrorCodes.LabelTooLong, $"Nhãn tối đa {MaxLabelLength} ký tự."));
        }
    }

    private static JsonObject ToJson(NavItem item, int depth, bool forSnapshot = false)
    {
        var o = new JsonObject
        {
            ["key"] = item.Key,
            ["label"] = item.Label,
        };
        if (item.Url is not null)
        {
            o["url"] = item.Url;
        }

        if (!forSnapshot)
        {
            o["hidden"] = item.Hidden;
        }

        if (depth < MaxDepth && (!forSnapshot || item.Children.Count > 0))
        {
            var children = new JsonArray();
            foreach (var c in item.Children)
            {
                children.Add(ToJson(c, depth + 1, forSnapshot));
            }

            o["children"] = children;
        }

        return o;
    }

    private static List<NavItem> ReadArray(JsonArray arr, int depth)
    {
        var list = new List<NavItem>();
        foreach (var node in arr)
        {
            if (node is not JsonObject o || o["key"]?.GetValueKind() != JsonValueKind.String)
            {
                throw new InvalidOperationException("Mục menu hỏng: thiếu 'key'.");
            }

            // Cấp 3 không có children; thứ gì sâu hơn trong tài liệu lưu hỏng bị bỏ khi đọc (không ném).
            var children = depth < MaxDepth && o["children"] is JsonArray c ? ReadArray(c, depth + 1) : [];
            list.Add(new NavItem(
                o["key"]!.GetValue<string>(),
                o["label"]?.GetValueKind() == JsonValueKind.String ? o["label"]!.GetValue<string>() : null,
                o["url"]?.GetValueKind() == JsonValueKind.String ? o["url"]!.GetValue<string>() : null,
                o["hidden"]?.GetValueKind() == JsonValueKind.True,
                children));
        }

        return list;
    }
}
