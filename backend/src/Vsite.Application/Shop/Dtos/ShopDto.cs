using Vsite.Domain.Shop.Enums;

namespace Vsite.Application.Shop.Dtos;

/// <summary>Trả về từ Create/Get/Update — một shop cụ thể, đủ field 04 §2.1. `LogoId` thêm ở T7,
/// MEDIA-001 (#73, #82) — additive trên contract `shop`, trỏ bản Library trong module Media
/// (`Shop` không reference kiểu `MediaAsset`, chỉ `Guid?` thuần). `LogoUrl` thêm ở D4 (#88) —
/// `/media/{storageKey}` của phái sinh logo `320x96,inside` (đường dẫn tương đối theo domain),
/// `null` khi chưa có logo/phái sinh.</summary>
public sealed record ShopDto(Guid Id, string Name, string Slug, ShopKind Kind, string? ExternalUrl, ShopStatus Status, Guid? LogoId, string? LogoUrl);

/// <summary>Trả về từ `GET /shops` — shop mà user hiện tại là thành viên, kèm vai trò để FE làm
/// shop switcher (thay thế hoàn toàn `GET /auth/me/shops` cũ — Quyết định ghi ở
/// `Docs/tasks/SHOP-001/contract-diff.md` mục 1). `LogoUrl` thêm ở D4 (#88), tra theo lô một câu SQL.</summary>
public sealed record ShopSummaryDto(Guid Id, string Name, string Slug, ShopKind Kind, ShopStatus Status, string RoleCode, string? LogoUrl);
