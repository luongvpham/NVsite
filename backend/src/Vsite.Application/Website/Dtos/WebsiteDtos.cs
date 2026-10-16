using System.Text.Json;
using Vsite.Domain.Website.Enums;

namespace Vsite.Application.Website.Dtos;

/// <summary>`data-needs` — màn "Tạo website". <paramref name="HomePageId"/> để Portal mở thẳng editor.
/// <paramref name="TrackingSnippets"/> là object JSON (<c>{ ga4?, metaPixel? }</c>).</summary>
public sealed record WebsiteDto(
    Guid Id,
    WebsiteStatus Status,
    string DefaultLocale,
    IReadOnlyList<string> Locales,
    string? DefaultSeoTitle,
    string? DefaultSeoDescription,
    Guid? DefaultOgImageId,
    Guid? FaviconAssetId,
    JsonElement TrackingSnippets,
    Guid HomePageId);

/// <summary>
/// `data-needs` — màn "Danh sách trang" và "Thuộc tính trang". <paramref name="IsHomePage"/> suy ra từ <c>Slug == ""</c>
/// (không phải cột); <paramref name="Deletable"/> là một nguồn luật duy nhất cho UI; <paramref name="IsDirty"/> lấy từ draft.
/// <paramref name="Slug"/> là <c>null</c> với trang NotFound (không có route công khai).
/// </summary>
public sealed record PageDto(
    Guid Id,
    PageKind Kind,
    SystemType? SystemType,
    string? Slug,
    string Title,
    string? SeoTitle,
    string? SeoDescription,
    Guid? OgImageId,
    bool NoIndex,
    bool IsEnabled,
    int SortOrder,
    bool IsHomePage,
    bool IsDirty,
    bool Deletable,
    DateTimeOffset UpdatedAt);

public sealed record PageDraftPageDto(Guid Id, PageKind Kind, SystemType? SystemType);

/// <summary>
/// `data-needs` — "Editor — mở trang". <paramref name="Version"/> = <c>xmin</c> (G5), opaque với FE: gửi lại nguyên văn.
/// <paramref name="Tree"/> đã qua upcaster (#94).
/// </summary>
public sealed record PageDraftDto(
    JsonElement Tree,
    long Version,
    bool IsDirty,
    DateTimeOffset UpdatedAt,
    PageDraftPageDto Page);

/// <summary>`data-needs` — "Editor — autosave": không trả lại cả tree. <paramref name="Sanitized"/> = bản lưu khác bản gửi.</summary>
public sealed record SavePageDraftResultDto(long Version, bool IsDirty, bool Sanitized);

/// <summary>`data-needs` — "Theme panel".</summary>
public sealed record ThemeDto(string? PresetKey, JsonElement Tokens, DateTimeOffset UpdatedAt);
