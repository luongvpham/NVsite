using System.Text.Json;
using Vsite.Application.Common.Imaging;

namespace Vsite.Infrastructure.Imaging;

/// <summary>
/// Đọc <c>packages/builder-components/generated/derivative-presets.json</c> một lần lúc startup,
/// cache trong memory (T3, MEDIA-001, Quyết định #86). Artifact này là FULL SET (manifest ∪
/// surfaces) — nguồn sự thật DUY NHẤT cho derivative cần pre-generate; class này KHÔNG đọc lại
/// <c>surfaces</c> trong <c>config/image-presets.json</c>. File được link vào output directory bởi
/// <c>Vsite.Api.csproj</c>, cùng cách <see cref="Vsite.Infrastructure.Configuration.ReservedRoutesProvider"/>
/// đang làm. Thiếu file hoặc sai shape → ném exception ngay ở constructor — Program.cs resolve
/// singleton này eager lúc startup để fail fast.
/// </summary>
public sealed class DerivativePresetCatalog : IDerivativePresetCatalog
{
    private readonly Dictionary<string, IReadOnlyList<string>> _bySource;

    public DerivativePresetCatalog(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"derivative-presets.json không tìm thấy tại '{filePath}'. " +
                "Kiểm tra CopyToOutputDirectory link trong Vsite.Api.csproj.",
                filePath);
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(filePath));

        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                $"derivative-presets.json tại '{filePath}' sai shape — kỳ vọng object cấp cao nhất (source → danh sách preset name).");
        }

        var bySource = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        foreach (var property in doc.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException(
                    $"derivative-presets.json tại '{filePath}' sai shape ở '{property.Name}' — kỳ vọng array of string.");
            }

            var names = new List<string>();
            foreach (var item in property.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidOperationException(
                        $"derivative-presets.json tại '{filePath}' sai shape — phần tử trong '{property.Name}' phải là string.");
                }

                names.Add(item.GetString()!);
            }

            bySource[property.Name] = names;
        }

        _bySource = bySource;
    }

    public IReadOnlyList<string> For(string source) =>
        _bySource.TryGetValue(source, out var names) ? names : Array.Empty<string>();
}
