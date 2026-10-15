using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Vsite.Application.Website.Versioning;

public enum DocumentKind
{
    Tree,
    Tokens,
}

/// <summary>Phiên bản schema hiện hành của tài liệu JSON (#94). Đổi khi thêm upcaster tương ứng.</summary>
public static class DocumentSchemaVersions
{
    public const int Tree = 1;
    public const int Tokens = 1;
}

/// <summary>
/// Một bước nâng tài liệu từ <see cref="FromVersion"/> lên <c>FromVersion + 1</c> (#94). Chỉ biến đổi nội dung —
/// <c>schemaVersion</c> do <see cref="DocumentVersioning"/> đặt.
/// </summary>
public interface IDocumentUpcaster
{
    DocumentKind Kind { get; }

    int FromVersion { get; }

    JsonObject Upcast(JsonObject document);
}

public enum UpcastStatus
{
    /// <summary>Đã đúng phiên bản hiện hành — không đổi.</summary>
    Current,

    /// <summary>Bản cũ đã được nâng lên hiện hành.</summary>
    Upcasted,

    /// <summary><c>schemaVersion</c> &gt; hiện hành → 422 SCHEMA_VERSION_UNSUPPORTED.</summary>
    Unsupported,

    /// <summary>Thiếu / không phải số nguyên / không có bước nâng → lỗi vỏ (R12), không 500.</summary>
    Malformed,
}

public sealed record UpcastResult(UpcastStatus Status, JsonObject? Document, string? Message);

/// <summary>
/// Khung nâng phiên bản cho <c>Tree</c> và <c>Tokens</c> (T5, WEBSITE-001, #94, R12). Đọc: nâng mọi bản cũ lên hiện
/// hành; ghi: lưu hiện hành; <c>&gt; hiện hành</c> → <see cref="UpcastStatus.Unsupported"/>. Hiện chưa có bước nâng nào
/// (hiện hành = 1) — chỉ có khung.
/// </summary>
public sealed class DocumentVersioning
{
    private readonly Dictionary<(DocumentKind Kind, int From), IDocumentUpcaster> _upcasters = [];
    private readonly int _treeCurrent;
    private readonly int _tokensCurrent;

    public DocumentVersioning(
        IEnumerable<IDocumentUpcaster> upcasters,
        int treeCurrent = DocumentSchemaVersions.Tree,
        int tokensCurrent = DocumentSchemaVersions.Tokens)
    {
        _treeCurrent = treeCurrent;
        _tokensCurrent = tokensCurrent;
        foreach (var upcaster in upcasters)
        {
            if (!_upcasters.TryAdd((upcaster.Kind, upcaster.FromVersion), upcaster))
            {
                throw new InvalidOperationException(
                    $"Hai IDocumentUpcaster cùng nâng {upcaster.Kind} từ v{upcaster.FromVersion}.");
            }
        }
    }

    public int Current(DocumentKind kind) => kind == DocumentKind.Tree ? _treeCurrent : _tokensCurrent;

    public UpcastResult Upcast(DocumentKind kind, JsonNode? document)
    {
        if (document is not JsonObject root)
        {
            return new UpcastResult(UpcastStatus.Malformed, null, "Tài liệu gốc phải là object.");
        }

        if (!TryReadVersion(root, out var version, out var message))
        {
            return new UpcastResult(UpcastStatus.Malformed, null, message);
        }

        var current = Current(kind);
        if (version > current)
        {
            return new UpcastResult(UpcastStatus.Unsupported, null, $"schemaVersion {version} lớn hơn bản hiện hành {current}.");
        }

        if (version == current)
        {
            return new UpcastResult(UpcastStatus.Current, root, null);
        }

        if (version < 0)
        {
            return new UpcastResult(UpcastStatus.Malformed, null, "'schemaVersion' không được âm.");
        }

        var working = (JsonObject)root.DeepClone();
        var v = (int)version; // version < current nên nằm trong int (current là int)
        while (v < current)
        {
            if (!_upcasters.TryGetValue((kind, v), out var upcaster))
            {
                return new UpcastResult(UpcastStatus.Malformed, null, $"Không có bước nâng {kind} từ schemaVersion {v}.");
            }

            working = upcaster.Upcast(working);
            v++;
            working["schemaVersion"] = v;
        }

        return new UpcastResult(UpcastStatus.Upcasted, working, null);
    }

    private static bool TryReadVersion(JsonObject root, out BigInteger version, out string message)
    {
        version = default;
        if (!root.TryGetPropertyValue("schemaVersion", out var node) || node is null)
        {
            message = "Thiếu 'schemaVersion'.";
            return false;
        }

        if (node.GetValueKind() != JsonValueKind.Number)
        {
            message = "'schemaVersion' phải là số nguyên.";
            return false;
        }

        // Giữ nguyên chữ số gốc (99999999999999999999 vẫn là số nguyên, không tràn long).
        var raw = node.ToJsonString();
        if (BigInteger.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out version))
        {
            message = string.Empty;
            return true;
        }

        if (decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d == decimal.Truncate(d))
        {
            version = new BigInteger(d);
            message = string.Empty;
            return true;
        }

        message = "'schemaVersion' phải là số nguyên.";
        return false;
    }
}
