using System.Text.Json;
using Vsite.Domain.ReservedRoutes;

namespace Vsite.Infrastructure.Configuration;

/// <summary>
/// Đọc config/reserved-routes.json một lần lúc startup, cache trong memory.
/// File được copy vào output directory bởi Vsite.Api.csproj (xem CopyToOutputDirectory link).
/// </summary>
public sealed class ReservedRoutesProvider : IReservedRoutesProvider
{
    public ReservedRoutes Routes { get; }

    public ReservedRoutesProvider(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"reserved-routes.json không tìm thấy tại '{filePath}'. " +
                "Kiểm tra CopyToOutputDirectory link trong Vsite.Api.csproj.",
                filePath);
        }

        var json = File.ReadAllText(filePath);
        Routes = JsonSerializer.Deserialize<ReservedRoutes>(json)
            ?? throw new InvalidOperationException($"Không parse được reserved-routes.json tại '{filePath}'.");

        // Key bắt buộc (WEBSITE-001 #98/R9): thiếu key thì mặc định `[]` sẽ âm thầm cho phép slug trang `media`/`api`.
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("reservedShopSitePaths", out _))
        {
            throw new InvalidOperationException(
                $"reserved-routes.json tại '{filePath}' thiếu key bắt buộc 'reservedShopSitePaths'.");
        }
    }
}
