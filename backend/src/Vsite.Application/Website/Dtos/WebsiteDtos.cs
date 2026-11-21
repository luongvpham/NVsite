using System.Text.Json;
using System.Text.Json.Serialization;
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
/// `data-needs` WEBSITE-007 — mục menu **cấp 1** (#113), dùng cho cả <c>getNavigation</c> lẫn body <c>saveNavigation</c>.
/// Menu lồng tối đa 3 cấp, diễn đạt bằng BA kiểu tường minh — <see cref="NavigationItemDto"/> → <see cref="NavigationChildItemDto"/> →
/// <see cref="NavigationGrandchildItemDto"/> — vì Microsoft.AspNetCore.OpenApi (9.0) sinh <c>items: {}</c> cho kiểu tự tham chiếu
/// (Orval ra <c>unknown[]</c>). Field giống nhau ở mọi cấp:
/// <c>Key</c> = <c>"page:{pageId}"</c> | <c>"custom"</c> (chuỗi, không enum — #19), <b>bắt buộc</b> trong body;
/// <c>Label</c> null = dùng tên trang (<c>custom</c> bắt buộc có); <c>Url</c> chỉ có giá trị ở <c>custom</c> (<c>null</c> với mục trang);
/// <c>Hidden</c> <b>bắt buộc</b> trong body (thiếu ⇒ 400, không âm thầm thành <c>false</c> làm hiện lại mục đang ẩn);
/// <c>Children</c> trả về luôn là mảng; thiếu trong body ⇒ rỗng.
/// </summary>
public sealed record NavigationItemDto(
    [property: JsonRequired] string Key,
    string? Label,
    string? Url,
    [property: JsonRequired] bool Hidden,
    IReadOnlyList<NavigationChildItemDto> Children);

/// <summary>Mục menu <b>cấp 2</b> — field như <see cref="NavigationItemDto"/>; <c>Children</c> là mục cấp 3.</summary>
public sealed record NavigationChildItemDto(
    [property: JsonRequired] string Key,
    string? Label,
    string? Url,
    [property: JsonRequired] bool Hidden,
    IReadOnlyList<NavigationGrandchildItemDto> Children);

/// <summary>
/// Mục menu <b>cấp 3</b> (cuối) — không có <c>children</c>. Từ chối thuộc tính lạ: body gửi <c>children</c> (cấp 4) hay field khác ở
/// cấp này ⇒ 400 thay vì âm thầm mất dữ liệu (#113).
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NavigationGrandchildItemDto(
    [property: JsonRequired] string Key,
    string? Label,
    string? Url,
    [property: JsonRequired] bool Hidden);

/// <summary>
/// `data-needs` WEBSITE-007 — màn "Menu". <paramref name="Items"/> ĐÃ GHÉP (#113): tự sinh nếu chưa lưu; trang chưa có nối cuối,
/// ẩn; mục trỏ trang đã xoá bị bỏ. <paramref name="Version"/> = <c>xmin</c> (opaque, gửi lại khi lưu), <c>null</c> khi chưa từng lưu —
/// khi đó <paramref name="IsDirty"/> = <c>true</c> (G6).
/// </summary>
public sealed record NavigationDto(long? Version, bool IsDirty, IReadOnlyList<NavigationItemDto> Items);

/// <summary>`data-needs` WEBSITE-007 — kết quả lưu menu: không trả lại items.</summary>
public sealed record SaveNavigationResultDto(long Version, bool IsDirty);
