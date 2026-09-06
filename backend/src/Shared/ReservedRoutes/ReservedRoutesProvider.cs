using System.Text.Json;

namespace Shared.ReservedRoutes;

/// <summary>
/// Đọc config/reserved-routes.json một lần lúc startup, cache trong memory.
/// File được copy vào output directory bởi Api.csproj (xem CopyToOutputDirectory link).
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
                "Kiểm tra CopyToOutputDirectory link trong Api.csproj.",
                filePath);
        }

        var json = File.ReadAllText(filePath);
        Routes = JsonSerializer.Deserialize<ReservedRoutes>(json)
            ?? throw new InvalidOperationException($"Không parse được reserved-routes.json tại '{filePath}'.");
    }
}
