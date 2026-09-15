using Vsite.Domain.Shop.Enums;

namespace Vsite.Application.Shop.Dtos;

/// <summary>Trả về từ Create/Get/Update — một shop cụ thể, đủ field 04 §2.1.</summary>
public sealed record ShopDto(Guid Id, string Name, string Slug, ShopKind Kind, string? ExternalUrl, ShopStatus Status);

/// <summary>Trả về từ `GET /shops` — shop mà user hiện tại là thành viên, kèm vai trò để FE làm
/// shop switcher (thay thế hoàn toàn `GET /auth/me/shops` cũ — Quyết định ghi ở
/// `Docs/tasks/SHOP-001/contract-diff.md` mục 1).</summary>
public sealed record ShopSummaryDto(Guid Id, string Name, string Slug, ShopKind Kind, ShopStatus Status, string RoleCode);
