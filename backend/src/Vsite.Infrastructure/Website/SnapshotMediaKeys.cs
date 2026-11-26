using System.Text.Json.Nodes;
using Vsite.Application.Website.Versioning;

namespace Vsite.Infrastructure.Website;

/// <summary>
/// Một chỗ duy nhất đọc các key của <c>snapshot.media</c> (id ảnh dùng trong một bản publish), dùng chung cho
/// <see cref="SitePublicationReferenceSource"/> và <see cref="SitePublicationUsageSource"/>. Đi qua
/// <see cref="StoredDocuments.ReadCurrent"/> nên snapshot hỏng/không hỗ trợ NÉM (không trả rỗng). `strict` ném khi thiếu `media`. Key so qua <see cref="Guid"/> (R14).
/// </summary>
internal static class SnapshotMediaKeys
{
    public static IEnumerable<Guid> Read(string stored, DocumentVersioning versioning, bool strict = false)
    {
        var snapshot = JsonNode.Parse(StoredDocuments.ReadCurrent(DocumentKind.Snapshot, stored, versioning));
        if (snapshot?["media"] is not JsonObject media)
        {
            // strict (đường quyết định xoá): thiếu/sai kiểu `media` là snapshot hỏng, không phải "không dùng ảnh nào" (R5).
            if (strict)
            {
                throw new InvalidOperationException("Snapshot đã lưu thiếu 'media' hoặc 'media' không phải object.");
            }

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
