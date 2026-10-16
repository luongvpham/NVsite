using System.Text.Json.Nodes;

namespace Vsite.Application.Website.Versioning;

/// <summary>
/// Đọc tài liệu JSON đã lưu (tree/tokens) và đưa lên phiên bản hiện hành (#94). Dữ liệu lưu luôn đã qua validator nên
/// <c>Malformed</c>/<c>Unsupported</c> ở đây nghĩa là DB hỏng hoặc bản triển khai cũ hơn dữ liệu → lỗi hệ thống (500), không phải lỗi người dùng.
/// </summary>
public static class StoredDocuments
{
    public static string ReadCurrent(DocumentKind kind, string stored, DocumentVersioning versioning)
    {
        var result = versioning.Upcast(kind, JsonNode.Parse(stored));
        return result.Status switch
        {
            UpcastStatus.Current => stored,
            UpcastStatus.Upcasted => result.Document!.ToJsonString(),
            _ => throw new InvalidOperationException(
                $"Tài liệu {kind} đã lưu không đọc được ({result.Status}): {result.Message}"),
        };
    }
}
