using System.Text.Json.Nodes;
using Vsite.Application.Website.Versioning;
using Vsite.Domain.Website.Entities;
using WebsiteEntity = Vsite.Domain.Website.Entities.Website;

namespace Vsite.Application.Website.Publishing;

/// <summary>Một trang được đưa vào snapshot: entity <see cref="Page"/> + cây đã validate/sạch (<c>CleanTree</c> do
/// <see cref="Validation.PageTreeValidator"/> trả — đã upcast, richText đã sạch).</summary>
public sealed record SnapshotPage(Page Page, string CleanTree);

/// <summary>Một mục ảnh đóng băng trong snapshot (plan §4): <c>imageId → { storageKey, preset }</c>.
/// <paramref name="Preset"/> = <c>null</c> cho bản Library (file gốc).</summary>
public sealed record SnapshotMedia(Guid Id, string StorageKey, string? Preset);

/// <summary>
/// Dựng JSON vỏ <c>SiteSnapshot</c> v1 (8b, #94, #97, plan §4) — THUẦN, không chạm DB. Caller (PublishWebsiteHandler, T4)
/// gom entity + cây sạch + bảng ảnh rồi gọi <see cref="Build"/>; kết quả là một chuỗi bất biến lưu vào
/// <c>site_publication.snapshot</c>. Vỏ khớp <c>generated/site-snapshot.schema.json</c> (test T2 khẳng định).
/// </summary>
public static class SnapshotBuilder
{
    public static string Build(
        WebsiteEntity website,
        Theme theme,
        int version,
        IReadOnlyList<SnapshotPage> pages,
        IReadOnlyList<SnapshotMedia> media)
    {
        var mediaObj = new JsonObject();
        foreach (var m in media)
        {
            // Key GUID dạng "D" chữ thường (ToString mặc định). Indexer ghi đè nếu trùng id — an toàn.
            mediaObj[m.Id.ToString()] = new JsonObject
            {
                ["storageKey"] = m.StorageKey,
                ["preset"] = m.Preset,
            };
        }

        var pagesArr = new JsonArray();
        foreach (var sp in pages
            .OrderBy(p => p.Page.SortOrder)
            .ThenBy(p => p.Page.Title, StringComparer.Ordinal))
        {
            var page = sp.Page;
            pagesArr.Add(new JsonObject
            {
                ["id"] = page.Id.ToString(),
                ["slug"] = page.Slug,
                ["kind"] = page.Kind.ToString(),
                ["systemType"] = page.SystemType?.ToString(),
                ["title"] = page.Title,
                ["seo"] = new JsonObject
                {
                    ["title"] = page.SeoTitle,
                    ["description"] = page.SeoDescription,
                    ["ogImageId"] = page.OgImageId?.ToString(),
                    ["noIndex"] = page.NoIndex,
                },
                ["tree"] = JsonNode.Parse(sp.CleanTree),
            });
        }

        var localesArr = new JsonArray();
        foreach (var locale in website.Locales)
        {
            localesArr.Add(JsonValue.Create(locale));
        }

        var root = new JsonObject
        {
            ["schemaVersion"] = DocumentSchemaVersions.Snapshot,
            ["version"] = version,
            ["defaultLocale"] = website.DefaultLocale,
            ["locales"] = localesArr,
            ["theme"] = new JsonObject
            {
                ["presetKey"] = theme.PresetKey,
                // Nguyên văn tokens đã lưu (đã validate lúc ghi) — không upcast (plan §4).
                ["tokens"] = JsonNode.Parse(theme.Tokens),
            },
            ["navigation"] = new JsonObject { ["items"] = new JsonArray() }, // NavigationConfig thật là Bước 7.
            ["seo"] = new JsonObject
            {
                ["defaultTitle"] = website.DefaultSeoTitle,
                ["defaultDescription"] = website.DefaultSeoDescription,
                ["ogImageId"] = website.DefaultOgImageId?.ToString(),
                ["faviconAssetId"] = website.FaviconAssetId?.ToString(),
            },
            ["tracking"] = JsonNode.Parse(website.TrackingSnippets),
            ["media"] = mediaObj,
            ["pages"] = pagesArr,
        };

        return root.ToJsonString();
    }
}
