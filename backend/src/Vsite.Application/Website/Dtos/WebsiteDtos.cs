using System.Text.Json;
using Vsite.Domain.Website.Enums;

namespace Vsite.Application.Website.Dtos;

/// <summary>`data-needs` — màn "Tạo website". <paramref name="HomePageId"/> để Portal mở thẳng editor.
/// <paramref name="TrackingSnippets"/> là object JSON (<c>{ ga4?, metaPixel? }</c>). <paramref name="CurrentPublicationId"/> = bản đang
/// chạy (null khi chưa publish lần nào — WEBSITE-004, additive).</summary>
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
    Guid HomePageId,
    Guid? CurrentPublicationId);

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

/// <summary>`data-needs` — "Portal — nút Xuất bản" / "lịch sử xuất bản" (WEBSITE-004). KHÔNG kèm snapshot.
/// <paramref name="IsCurrent"/> = bản này là <c>Website.CurrentPublicationId</c>.</summary>
public sealed record PublicationDto(
    Guid Id,
    int Version,
    DateTimeOffset PublishedAt,
    Guid? PublishedByUserId,
    string? Note,
    bool IsCurrent);

/// <summary>
/// `data-needs` WEBSITE-007 — một mục menu cấp 1 (#113), dùng cho cả <c>getNavigation</c> lẫn body <c>saveNavigation</c>.
/// <paramref name="Key"/> = <c>"page:{pageId}"</c> | <c>"custom"</c> (chuỗi, không enum — #19). <paramref name="Label"/> null = dùng tên
/// trang (<c>custom</c> bắt buộc có). <paramref name="Url"/> chỉ có giá trị ở <c>custom</c> (<c>null</c> với mục trang).
/// <paramref name="Children"/>: mục con MỘT cấp, kiểu riêng <see cref="NavigationChildItemDto"/> không có <c>children</c> — không dùng kiểu
/// tự tham chiếu vì Microsoft.AspNetCore.OpenApi (9.0) sinh <c>items: {}</c> cho nó (Orval ra <c>unknown[]</c>). Trả về luôn là mảng.
/// </summary>
public sealed record NavigationItemDto(
    string Key,
    string? Label,
    string? Url,
    bool Hidden,
    IReadOnlyList<NavigationChildItemDto> Children);

/// <summary>Mục con của <see cref="NavigationItemDto"/> — cùng nghĩa các field, không lồng tiếp (#113: menu một cấp).</summary>
public sealed record NavigationChildItemDto(string Key, string? Label, string? Url, bool Hidden);

/// <summary>
/// `data-needs` WEBSITE-007 — màn "Menu". <paramref name="Items"/> ĐÃ GHÉP (#113): tự sinh nếu chưa lưu; trang chưa có nối cuối,
/// ẩn; mục trỏ trang đã xoá bị bỏ. <paramref name="Version"/> = <c>xmin</c> (opaque, gửi lại khi lưu), <c>null</c> khi chưa từng lưu —
/// khi đó <paramref name="IsDirty"/> = <c>true</c> (G6).
/// </summary>
public sealed record NavigationDto(long? Version, bool IsDirty, IReadOnlyList<NavigationItemDto> Items);

/// <summary>`data-needs` WEBSITE-007 — kết quả lưu menu: không trả lại items.</summary>
public sealed record SaveNavigationResultDto(long Version, bool IsDirty);
