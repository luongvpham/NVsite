using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Vsite.Application.Website.Interfaces;
using Vsite.Application.Website.Validation;

namespace Vsite.Application.Website.Publishing;

/// <summary>
/// Kiểm prop bắt buộc theo variant khi publish (Bước 8b, WEBSITE-004). Chạy trên <c>CleanTree</c> do
/// <see cref="PageTreeValidator"/> trả — cây đã hợp lệ (vỏ đúng, richText đã sạch) nên checker không lặp lại việc kiểm cú pháp/vỏ.
/// KHÔNG gọi ở <c>PUT draft</c>: draft dở dang được phép thiếu prop. Mã lỗi <see cref="TreeErrorCodes.RequiredPropMissing"/>,
/// <c>path = /…/props/{tên}</c>. Walker LẶP (stack tường minh) — không đệ quy theo độ sâu cây.
/// </summary>
/// <remarks>
/// Định nghĩa "thiếu" (plan §4): key vắng hoặc <c>null</c>; <c>text</c> rỗng sau trim; <c>richText</c> không còn ký tự
/// nào sau khi bỏ thẻ; <c>image</c> không có <c>imageId</c> chuỗi khác rỗng; <c>list</c> có 0 phần tử; <c>binding</c>
/// không có <c>source</c>; kind khác chỉ tính vắng/<c>null</c>.
/// </remarks>
public sealed class RequiredPropsChecker(ITreeRulesCatalog rules)
{
    // Chỉ dùng cho richText: bỏ thẻ để đo "còn ký tự nào không". Cây đã qua sanitizer nên HTML lành.
    private static readonly Regex HtmlTags = new("<[^>]*>", RegexOptions.Compiled);

    public IReadOnlyList<TreeValidationError> Check(string cleanTreeJson)
    {
        var errors = new List<TreeValidationError>();
        if (JsonNode.Parse(cleanTreeJson) is not JsonObject root)
        {
            return errors;
        }

        var stack = new Stack<(JsonObject Node, string Path)>();
        PushChildren(stack, root, string.Empty);
        while (stack.Count > 0)
        {
            var (node, path) = stack.Pop();
            CheckNode(node, path, errors);
            PushChildren(stack, node, path);
        }

        return errors;
    }

    private static void PushChildren(Stack<(JsonObject Node, string Path)> stack, JsonObject parent, string parentPath)
    {
        if (parent["children"] is not JsonArray children)
        {
            return;
        }

        // Đẩy ngược để pop theo thứ tự tài liệu (thứ tự lỗi ổn định).
        for (var i = children.Count - 1; i >= 0; i--)
        {
            if (children[i] is JsonObject child)
            {
                stack.Push((child, $"{parentPath}/children/{i}"));
            }
        }
    }

    private void CheckNode(JsonObject node, string path, List<TreeValidationError> errors)
    {
        if (node["type"] is not JsonValue typeNode || !typeNode.TryGetValue<string>(out var type)
            || !rules.Types.TryGetValue(type, out var rule))
        {
            return;
        }

        if (node["variant"] is not JsonValue variantNode || !variantNode.TryGetValue<string>(out var variant)
            || !rule.RequiresProps.TryGetValue(variant, out var required))
        {
            return;
        }

        var props = node["props"] as JsonObject;
        foreach (var name in required)
        {
            // kind lấy từ luật prop của type; variant không được khai prop lạ nên bình thường luôn có.
            var kind = rule.Props.TryGetValue(name, out var propRule) ? propRule.Kind : null;
            if (IsMissing(props?[name], kind))
            {
                errors.Add(new TreeValidationError(
                    $"{path}/props/{Escape(name)}", TreeErrorCodes.RequiredPropMissing, $"Prop bắt buộc '{name}' bị thiếu."));
            }
        }
    }

    private static bool IsMissing(JsonNode? value, string? kind)
    {
        // JsonObject indexer trả null cho cả "khoá vắng" lẫn "giá trị JSON null".
        if (value is null)
        {
            return true;
        }

        return kind switch
        {
            "text" => !(value is JsonValue t && t.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s)),
            "richText" => !(value is JsonValue r && r.TryGetValue<string>(out var html) && HasVisibleText(html)),
            "image" => !(value is JsonObject img && img["imageId"] is JsonValue iv
                && iv.TryGetValue<string>(out var id) && !string.IsNullOrEmpty(id)),
            "list" => !(value is JsonArray arr && arr.Count > 0),
            "binding" => !(value is JsonObject b && b["source"] is JsonValue sv
                && sv.TryGetValue<string>(out var src) && !string.IsNullOrEmpty(src)),
            _ => false, // kind khác: có mặt và khác null là đủ
        };
    }

    private static bool HasVisibleText(string html) => HtmlTags.Replace(html, string.Empty).Trim().Length > 0;

    private static string Escape(string key) =>
        key.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
}
