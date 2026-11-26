using System.Text.Json.Nodes;
using Vsite.Application.Website.Versioning;

namespace Vsite.Infrastructure.Website;

/// <summary>
/// Một chỗ duy nhất đọc các key của <c>snapshot.media</c> (id ảnh dùng trong một bản publish), dùng chung cho
/// <see cref="SitePublicationReferenceSource"/> và <see cref="SitePublicationUsageSource"/>. Đi qua
/// <see cref="StoredDocuments.ReadCurrent"/> nên snapshot hỏng/không hỗ trợ NÉM (không trả rỗng). Key so qua <see cref="Guid"/> (R14).
/// </summary>
internal static class SnapshotMediaKeys
{
    public static IEnumerable<Guid> Read(string stored, DocumentVersioning versioning)
    {
        var snapshot = JsonNode.Parse(StoredDocuments.ReadCurrent(DocumentKind.Snapshot, stored, versioning));
        if (snapshot?["media"] is not JsonObject media)
        {
            yield break;
        }

        foreach (var (key, _) in media)
        {
            // Guid.TryParse không phân biệt hoa/thường nên R14 (key viết thường) khớp mà không phải so chuỗi.
            if (Guid.TryParse(key, out var id))
            {
                yield return id;
            }
        }
    }
}
