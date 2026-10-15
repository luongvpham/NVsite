using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Vsite.Application.Website.Interfaces;
using Vsite.Application.Website.Options;
using Vsite.Application.Website.Versioning;
using Vsite.Domain.Website.Enums;

namespace Vsite.Application.Website.Validation;

/// <summary>
/// Validate TOÀN Component Tree (T4, WEBSITE-001, #60, #67; plan Flow 2 b→g). Thứ tự: kích thước body → cú pháp JSON +
/// độ sâu JSON → giới hạn độ sâu/số node → phiên bản (upcaster, #94) → vỏ (JSON Schema sinh từ Zod, #97) → từng node
/// (type/variant/id/pageKind/children/maxPerPage/props/i18n) → binding và richText ở mọi độ sâu.
/// Walker LẶP (stack tường minh) — không đệ quy theo độ sâu cây (R8). Trả KẾT QUẢ, không ném lỗi lần đầu thấy.
/// Bước (h) ảnh và (i) so System page cần DB/bản đang lưu nên ở T7/T5b.
/// </summary>
public sealed class PageTreeValidator
{
    private readonly ITreeRulesCatalog _rules;
    private readonly IBindingSourceCatalog _bindingSources;
    private readonly IWebsiteSchemaProvider _schemas;
    private readonly IRichTextSanitizer _sanitizer;
    private readonly PageTreeLimits _limits;
    private readonly DocumentVersioning _versioning;

    public PageTreeValidator(
        ITreeRulesCatalog rules,
        IBindingSourceCatalog bindingSources,
        IWebsiteSchemaProvider schemas,
        IRichTextSanitizer sanitizer,
        PageTreeLimits limits,
        DocumentVersioning versioning)
    {
        _rules = rules;
        _bindingSources = bindingSources;
        _schemas = schemas;
        _sanitizer = sanitizer;
        _limits = limits;
        _versioning = versioning;
    }

    /// <summary>Trần JSON lồng nhau — khớp mặc định System.Text.Json/JsonSchema.Net (xem <see cref="PageTreeLimits.MaxDepth"/>).</summary>
    private const int MaxJsonDepth = 64;

    public TreeValidationResult Validate(string treeJson, PageKind pageKind)
    {
        var sink = new ErrorSink(_limits.MaxErrors);

        // 1. Kích thước. UTF-16 → UTF-8 không bao giờ ngắn đi, nên Length > Max là đủ kết luận mà không đếm byte.
        if (treeJson is null || treeJson.Length > _limits.MaxBodyBytes || Encoding.UTF8.GetByteCount(treeJson) > _limits.MaxBodyBytes)
        {
            sink.Add("", TreeErrorCodes.BodyTooLarge, $"Cây vượt quá {_limits.MaxBodyBytes} byte.");
            return sink.ToResult();
        }

        // 2. Cú pháp + độ sâu JSON (Utf8JsonReader không đệ quy).
        var syntax = CheckSyntax(treeJson);
        if (syntax is not null)
        {
            sink.Add("", syntax.Value.Code, syntax.Value.Message);
            return sink.ToResult();
        }

        var root = JsonNode.Parse(treeJson);

        // 3. Giới hạn độ sâu / số node — trước mọi thứ đệ quy (JSON Schema).
        var limitError = CheckLimits(root);
        if (limitError is not null)
        {
            sink.Add("", limitError.Value.Code, limitError.Value.Message);
            return sink.ToResult();
        }

        // 4. Phiên bản (#94, R12).
        var upcast = _versioning.Upcast(DocumentKind.Tree, root);
        string text = treeJson;
        switch (upcast.Status)
        {
            case UpcastStatus.Malformed:
                sink.Add("", TreeErrorCodes.EnvelopeInvalid, upcast.Message ?? "Vỏ cây không hợp lệ.");
                return sink.ToResult();

            case UpcastStatus.Unsupported:
                // README: chỉ báo "phiên bản không hỗ trợ" khi vỏ hợp lệ; vỏ hỏng thì báo lỗi vỏ.
                var violations = _schemas.ValidateTreeEnvelope(treeJson);
                if (violations.Count == 0)
                {
                    sink.Add("/schemaVersion", TreeErrorCodes.SchemaVersionUnsupported, upcast.Message ?? "Phiên bản schema không được hỗ trợ.");
                }
                else
                {
                    AddEnvelopeErrors(sink, violations);
                }

                return sink.ToResult();

            case UpcastStatus.Upcasted:
                root = upcast.Document;
                text = root!.ToJsonString();
                break;
        }

        // 5. Vỏ.
        var envelope = _schemas.ValidateTreeEnvelope(text);
        if (envelope.Count > 0)
        {
            AddEnvelopeErrors(sink, envelope);
            return sink.ToResult();
        }

        // 6. Từng node (cây đã qua vỏ nên các trường bắt buộc có đúng kiểu).
        var tree = (JsonObject)root!;
        var walk = new Walk(this, sink, pageKind);
        walk.Run(tree);

        return sink.ToResult(sink.Count == 0 ? tree.ToJsonString() : null, walk.ImageIds, walk.Sanitized);
    }

    private static void AddEnvelopeErrors(ErrorSink sink, IReadOnlyList<SchemaViolation> violations)
    {
        foreach (var violation in violations)
        {
            sink.Add(violation.Path, TreeErrorCodes.EnvelopeInvalid, violation.Message);
        }
    }

    private (string Code, string Message)? CheckSyntax(string json)
    {
        try
        {
            var reader = new Utf8JsonReader(
                Encoding.UTF8.GetBytes(json),
                new JsonReaderOptions { MaxDepth = int.MaxValue });
            var depth = 0;
            var any = false;
            while (reader.Read())
            {
                any = true;
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject or JsonTokenType.StartArray:
                        depth++;
                        if (depth > MaxJsonDepth)
                        {
                            return (TreeErrorCodes.TooDeep, $"JSON lồng quá {MaxJsonDepth} tầng.");
                        }

                        break;
                    case JsonTokenType.EndObject or JsonTokenType.EndArray:
                        depth--;
                        break;
                }
            }

            return any ? null : (TreeErrorCodes.EnvelopeInvalid, "Tài liệu rỗng.");
        }
        catch (JsonException ex)
        {
            return (TreeErrorCodes.EnvelopeInvalid, $"Không phải JSON hợp lệ: {ex.Message}");
        }
    }

    /// <summary>Đếm node và độ sâu bằng stack tường minh, dừng ngay khi vượt giới hạn.</summary>
    private (string Code, string Message)? CheckLimits(JsonNode? root)
    {
        if (root is not JsonObject top || top["children"] is not JsonArray topChildren)
        {
            return null; // vỏ sẽ báo
        }

        var nodes = 0;
        var stack = new Stack<(JsonArray Children, int Depth)>();
        stack.Push((topChildren, 1));
        while (stack.Count > 0)
        {
            var (children, depth) = stack.Pop();
            if (depth > _limits.MaxDepth)
            {
                if (children.Count > 0)
                {
                    return (TreeErrorCodes.TooDeep, $"Cây sâu quá {_limits.MaxDepth} tầng.");
                }

                continue;
            }

            foreach (var child in children)
            {
                if (++nodes > _limits.MaxNodes)
                {
                    return (TreeErrorCodes.TooManyNodes, $"Cây có quá {_limits.MaxNodes} node.");
                }

                if (child is JsonObject obj && obj["children"] is JsonArray grandChildren)
                {
                    stack.Push((grandChildren, depth + 1));
                }
            }
        }

        return null;
    }

    private sealed class ErrorSink(int max)
    {
        private readonly List<TreeValidationError> _errors = [];

        public int Count => _errors.Count;

        public bool Full => _errors.Count >= max;

        public bool Truncated { get; private set; }

        /// <summary>Đã dừng sớm vì đủ lỗi trong khi còn việc chưa làm — có thể còn lỗi chưa báo.</summary>
        public void MarkTruncated() => Truncated = true;

        public void Add(string path, string code, string message)
        {
            if (Full)
            {
                Truncated = true;
                return;
            }

            _errors.Add(new TreeValidationError(path, code, message));
        }

        public TreeValidationResult ToResult(string? cleanTree = null, IReadOnlyList<string>? imageIds = null, bool sanitized = false) =>
            new(_errors, imageIds ?? [], cleanTree, sanitized, Truncated);
    }

    private sealed class Walk(PageTreeValidator owner, ErrorSink sink, PageKind pageKind)
    {
        private readonly HashSet<string> _seenIds = new(StringComparer.Ordinal) { "root" };
        private readonly Dictionary<string, int> _perType = new(StringComparer.Ordinal);
        private readonly HashSet<string> _imageSet = new(StringComparer.Ordinal);
        private readonly List<string> _imageIds = [];

        public IReadOnlyList<string> ImageIds => _imageIds;

        public bool Sanitized { get; private set; }

        public void Run(JsonObject root)
        {
            var stack = new Stack<(JsonObject Node, string Path)>();
            PushChildren(stack, root, string.Empty);
            while (stack.Count > 0 && !sink.Full)
            {
                var (node, path) = stack.Pop();
                VisitNode(node, path);
                PushChildren(stack, node, path);
            }

            if (stack.Count > 0)
            {
                sink.MarkTruncated(); // dừng vì đủ lỗi trong khi còn node chưa duyệt
            }
        }

        private static void PushChildren(Stack<(JsonObject Node, string Path)> stack, JsonObject parent, string parentPath)
        {
            if (parent["children"] is not JsonArray children)
            {
                return;
            }

            // Đẩy ngược để pop theo thứ tự tài liệu (lỗi "đầu tiên" ổn định: trùng id, vượt maxPerPage).
            for (var i = children.Count - 1; i >= 0; i--)
            {
                if (children[i] is JsonObject child)
                {
                    stack.Push((child, $"{parentPath}/children/{i}"));
                }
            }
        }

        private void VisitNode(JsonObject node, string path)
        {
            var id = node["id"]!.GetValue<string>();
            if (!_seenIds.Add(id))
            {
                sink.Add($"{path}/id", TreeErrorCodes.NodeIdDuplicate, $"Id '{id}' bị trùng trong cây.");
            }

            var type = node["type"]!.GetValue<string>();
            if (node.ContainsKey("i18n"))
            {
                sink.Add($"{path}/i18n", TreeErrorCodes.I18nNotEnabled, "Bản dịch theo node (i18n) chưa được bật.");
            }

            if (!owner._rules.Types.TryGetValue(type, out var rule))
            {
                sink.Add($"{path}/type", TreeErrorCodes.NodeTypeUnknown, $"Loại component '{type}' không tồn tại.");
                return;
            }

            if (node["variant"] is { } variantNode && variantNode.GetValue<string>() is { } variant && !rule.Variants.Contains(variant))
            {
                sink.Add($"{path}/variant", TreeErrorCodes.NodeVariantUnknown, $"Variant '{variant}' không thuộc '{type}'.");
            }

            if (!rule.AllowedInPageKinds.Contains(pageKind.ToString()))
            {
                sink.Add($"{path}/type", TreeErrorCodes.NodeNotAllowedInPageKind, $"'{type}' không được đặt trong trang {pageKind}.");
            }

            var count = _perType.GetValueOrDefault(type) + 1;
            _perType[type] = count;
            if (rule.MaxPerPage is { } max && count == max + 1)
            {
                sink.Add(path, TreeErrorCodes.NodeMaxPerPageExceeded, $"Mỗi trang chỉ được tối đa {max} '{type}'.");
            }

            CheckChildren(node, path, type, rule);

            if (node["props"] is JsonObject props)
            {
                CheckProps(props, path, type, rule);
            }
        }

        private void CheckChildren(JsonObject node, string path, string type, TreeTypeRule rule)
        {
            if (node["children"] is not JsonArray { Count: > 0 } children)
            {
                return;
            }

            if (!rule.AcceptsChildren)
            {
                sink.Add($"{path}/children", TreeErrorCodes.NodeChildrenNotAccepted, $"'{type}' không chứa node con.");
                return;
            }

            if (rule.AllowedChildTypes is null)
            {
                return;
            }

            for (var i = 0; i < children.Count; i++)
            {
                if (children[i] is JsonObject child
                    && child["type"] is { } childType
                    && !rule.AllowedChildTypes.Contains(childType.GetValue<string>()))
                {
                    sink.Add($"{path}/children/{i}/type", TreeErrorCodes.NodeChildTypeNotAllowed,
                        $"'{type}' không chứa '{childType.GetValue<string>()}'.");
                }
            }
        }

        private void CheckProps(JsonObject props, string path, string type, TreeTypeRule rule)
        {
            var propsPath = $"{path}/props";
            if (owner._schemas.HasPropsSchema(type))
            {
                var violations = owner._schemas.ValidateProps(type, props.ToJsonString());
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var violation in violations)
                {
                    // Bỏ lỗi gộp ở tổ tiên khi đã có lỗi cụ thể ở con (cùng một sai, nhiều keyword).
                    var hasMoreSpecific = violations.Any(v => v.Path.Length > violation.Path.Length
                        && v.Path.StartsWith(violation.Path + "/", StringComparison.Ordinal));
                    if (hasMoreSpecific || !seen.Add(violation.Path))
                    {
                        continue;
                    }

                    sink.Add(propsPath + violation.Path, TreeErrorCodes.PropInvalid, violation.Message);
                }
            }

            VisitProps(props, rule.Props, propsPath);
        }

        private void VisitProps(JsonObject root, IReadOnlyDictionary<string, TreePropRule> rootRule, string rootPath)
        {
            var stack = new Stack<(JsonObject Value, IReadOnlyDictionary<string, TreePropRule> Rule, string Path)>();
            stack.Push((root, rootRule, rootPath));
            while (stack.Count > 0 && !sink.Full)
            {
                var (value, ruleMap, path) = stack.Pop();
                foreach (var name in value.Select(kv => kv.Key).ToList()) // sao chép khoá: richText ghi đè giá trị khi duyệt
                {
                    var propPath = $"{path}/{Escape(name)}";
                    if (!ruleMap.TryGetValue(name, out var propRule))
                    {
                        sink.Add(propPath, TreeErrorCodes.PropUnknown, $"Prop '{name}' không tồn tại.");
                        continue;
                    }

                    if (value[name] is not { } propValue)
                    {
                        continue;
                    }

                    switch (propRule.Kind)
                    {
                        case "image":
                            CollectImage(propValue);
                            break;
                        case "binding":
                            CheckBinding(propValue, propRule, propPath);
                            break;
                        case "richText":
                            CheckRichText(value, name, propValue, propRule, propPath);
                            break;
                        case "group":
                            if (propValue is JsonObject group && propRule.ItemProps is not null)
                            {
                                stack.Push((group, propRule.ItemProps, propPath));
                            }

                            break;
                        case "list":
                            if (propValue is JsonArray list && propRule.ItemProps is not null)
                            {
                                for (var i = list.Count - 1; i >= 0; i--)
                                {
                                    if (list[i] is JsonObject item)
                                    {
                                        stack.Push((item, propRule.ItemProps, $"{propPath}/{i}"));
                                    }
                                }
                            }

                            break;
                    }
                }
            }
        }

        private void CollectImage(JsonNode propValue)
        {
            if (propValue is JsonObject image
                && image["imageId"] is JsonValue idValue
                && idValue.TryGetValue<string>(out var imageId)
                && _imageSet.Add(imageId))
            {
                _imageIds.Add(imageId);
            }
        }

        private void CheckBinding(JsonNode propValue, TreePropRule rule, string propPath)
        {
            if (propValue is not JsonObject binding
                || !binding.TryGetPropertyValue("source", out var sourceNode)
                || sourceNode is null)
            {
                return;
            }

            var source = sourceNode is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
            if (source is null || !owner._bindingSources.Contains(source) || rule.Sources?.Contains(source) != true)
            {
                sink.Add($"{propPath}/source", TreeErrorCodes.BindingSourceNotAllowed,
                    $"Nguồn dữ liệu '{source ?? sourceNode.ToJsonString()}' không được phép ở prop này.");
            }
        }

        private void CheckRichText(JsonObject owningObject, string name, JsonNode propValue, TreePropRule rule, string propPath)
        {
            if (rule.Profile is null || propValue is not JsonValue v || !v.TryGetValue<string>(out var html))
            {
                return;
            }

            var result = owner._sanitizer.Sanitize(html, rule.Profile);
            if (result.Unsafe)
            {
                sink.Add(propPath, TreeErrorCodes.RichTextUnsafe, "Nội dung chứa thẻ/thuộc tính/liên kết không an toàn.");
            }
            else if (!string.Equals(result.Clean, html, StringComparison.Ordinal))
            {
                owningObject[name] = result.Clean;
                Sanitized = true;
            }
        }

        private static string Escape(string key) => key.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
    }
}
