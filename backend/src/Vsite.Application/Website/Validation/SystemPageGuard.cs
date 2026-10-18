using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Vsite.Application.Website.Interfaces;

namespace Vsite.Application.Website.Validation;

/// <summary>
/// Khoá cấu trúc trang System (T5b, WEBSITE-001, #46, R6): so cây gửi lên với bản đang lưu. Phải cùng tập id, type,
/// variant, thứ tự và cha-con; prop NGOÀI <c>editableInSystemPageProps</c> của type phải bằng nhau (so JSON canonical).
/// Duyệt LẶP, không đệ quy theo độ sâu cây. Giả định cả hai cây đã qua <see cref="PageTreeValidator"/>; JSON hỏng được
/// báo thành lỗi chứ không ném.
/// </summary>
public static class SystemPageGuard
{
    public static IReadOnlyList<TreeValidationError> Compare(string stored, string incoming, ITreeRulesCatalog rules) =>
        Compare(stored, incoming, type => rules.Types.TryGetValue(type, out var rule) ? rule.EditableInSystemPageProps : []);

    public static IReadOnlyList<TreeValidationError> Compare(
        string stored, string incoming, Func<string, IReadOnlyCollection<string>> editableProps)
    {
        try
        {
            return CompareCore(stored, incoming, editableProps);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or ArgumentException)
        {
            // JsonNode đọc lười: khoá trùng chỉ ném khi truy cập node, nên bọc cả phép so chứ không riêng bước parse.
            return [Structure("Không đọc được cây để so sánh với bản đang lưu.")];
        }
    }

    private static IReadOnlyList<TreeValidationError> CompareCore(
        string stored, string incoming, Func<string, IReadOnlyCollection<string>> editableProps)
    {
        List<Flat> a, b;
        try
        {
            a = Flatten(stored);
            b = Flatten(incoming);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or ArgumentException)
        {
            return [Structure("Không đọc được cây để so sánh với bản đang lưu.")];
        }

        if (a.Count != b.Count || a.Zip(b).Any(p => Shape(p.First) != Shape(p.Second)))
        {
            return [Structure("Trang hệ thống không được thêm, xoá, đổi loại/variant hoặc đổi thứ tự/cha-con của node.")];
        }

        var errors = new List<TreeValidationError>();
        for (var i = 0; i < a.Count; i++)
        {
            var editable = editableProps(a[i].Type);
            var storedProps = a[i].Node["props"] as JsonObject;
            var incomingProps = b[i].Node["props"] as JsonObject;
            var keys = new SortedSet<string>(StringComparer.Ordinal);
            keys.UnionWith(storedProps?.Select(kv => kv.Key) ?? []);
            keys.UnionWith(incomingProps?.Select(kv => kv.Key) ?? []);

            foreach (var key in keys)
            {
                if (editable.Contains(key))
                {
                    continue;
                }

                if (Canonical(storedProps?[key]) != Canonical(incomingProps?[key]))
                {
                    errors.Add(new TreeValidationError(
                        $"{b[i].Path}/props/{key.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal)}",
                        TreeErrorCodes.SystemPagePropNotEditable,
                        $"Prop '{key}' của '{a[i].Type}' không được sửa trên trang hệ thống."));
                }
            }
        }

        return errors;
    }

    private static TreeValidationError Structure(string message) =>
        new(string.Empty, TreeErrorCodes.SystemPageStructureChanged, message);

    private static string Shape(Flat f) => $"{f.Id}|{f.Type}|{f.Variant}|{f.ParentId}|{f.Index}";

    private sealed record Flat(JsonObject Node, string Id, string Type, string Variant, string ParentId, int Index, string Path);

    private static List<Flat> Flatten(string json)
    {
        var root = JsonNode.Parse(json) as JsonObject ?? throw new InvalidOperationException("Gốc không phải object.");
        var result = new List<Flat>();
        var stack = new Stack<Flat>();
        Push(stack, root, "root", string.Empty);
        while (stack.Count > 0)
        {
            var flat = stack.Pop();
            result.Add(flat);
            Push(stack, flat.Node, flat.Id, flat.Path);
        }

        return result;
    }

    private static void Push(Stack<Flat> stack, JsonObject parent, string parentId, string parentPath)
    {
        if (parent["children"] is not JsonArray children)
        {
            return;
        }

        for (var i = children.Count - 1; i >= 0; i--)
        {
            if (children[i] is not JsonObject node)
            {
                throw new InvalidOperationException("Node không phải object.");
            }

            stack.Push(new Flat(
                node,
                node["id"]?.GetValue<string>() ?? string.Empty,
                node["type"]?.GetValue<string>() ?? string.Empty,
                node["variant"]?.GetValue<string>() ?? string.Empty,
                parentId,
                i,
                $"{parentPath}/children/{i}"));
        }
    }

    /// <summary>JSON canonical: khoá object sắp theo thứ tự ordinal, số chuẩn hoá (1 == 1.0). Độ sâu ≤ 64 do JsonNode.Parse.</summary>
    private static string Canonical(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return "null";
            case JsonObject obj:
                var sb = new StringBuilder("{");
                foreach (var (key, value) in obj.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                {
                    if (sb.Length > 1)
                    {
                        sb.Append(',');
                    }

                    sb.Append(JsonSerializer.Serialize(key)).Append(':').Append(Canonical(value));
                }

                return sb.Append('}').ToString();
            case JsonArray arr:
                return "[" + string.Join(',', arr.Select(Canonical)) + "]";
            case JsonValue val when val.GetValueKind() == JsonValueKind.Number:
                return decimal.TryParse(val.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                    ? (d / 1.000000000000000000000000000000000m).ToString(CultureInfo.InvariantCulture)
                    : val.ToJsonString();
            default:
                return node.ToJsonString();
        }
    }
}
