using System.Text.Json.Serialization;

namespace Shared.ReservedRoutes;

/// <summary>
/// Mirror của config/reserved-routes.json (Quyết định #24) — nguồn sự thật duy nhất,
/// dùng chung FE + BE. Không viết tay danh sách thứ hai.
/// </summary>
public sealed record ReservedRoutes(
    [property: JsonPropertyName("reservedPaths")] IReadOnlyList<string> ReservedPaths,
    [property: JsonPropertyName("reservedSubdomains")] IReadOnlyList<string> ReservedSubdomains);
