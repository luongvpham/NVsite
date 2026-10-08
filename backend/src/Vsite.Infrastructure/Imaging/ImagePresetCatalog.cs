using System.Text.Json;
using Vsite.Application.Common.Imaging;

namespace Vsite.Infrastructure.Imaging;

/// <summary>
/// Đọc <c>config/image-presets.json</c> một lần lúc startup, cache trong memory (T3, MEDIA-001,
/// Quyết định #78). File được link vào output directory bởi <c>Vsite.Api.csproj</c> (xem
/// CopyToOutputDirectory link, cùng cách <see cref="Vsite.Infrastructure.Configuration.ReservedRoutesProvider"/>
/// đang làm). Thiếu file hoặc sai shape → ném exception ngay ở constructor — Program.cs resolve
/// singleton này eager lúc startup để fail fast, không chạy tiếp với danh sách rỗng.
/// </summary>
public sealed class ImagePresetCatalog : IImagePresetCatalog
{
    private readonly Dictionary<string, ImagePreset> _presets;

    public ImagePresetCatalog(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"image-presets.json không tìm thấy tại '{filePath}'. " +
                "Kiểm tra CopyToOutputDirectory link trong Vsite.Api.csproj.",
                filePath);
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(filePath));

        if (!doc.RootElement.TryGetProperty("presets", out var presetsElement) ||
            presetsElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                $"image-presets.json tại '{filePath}' thiếu hoặc sai shape trường 'presets' (kỳ vọng object).");
        }

        var presets = new Dictionary<string, ImagePreset>(StringComparer.Ordinal);

        foreach (var property in presetsElement.EnumerateObject())
        {
            var name = property.Name;
            var value = property.Value;

            if (!value.TryGetProperty("width", out var widthElement) || widthElement.ValueKind != JsonValueKind.Number ||
                !value.TryGetProperty("height", out var heightElement) || heightElement.ValueKind != JsonValueKind.Number ||
                !value.TryGetProperty("fit", out var fitElement) || fitElement.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException(
                    $"image-presets.json tại '{filePath}' sai shape ở preset '{name}' — cần 'width' (number), 'height' (number), 'fit' (string).");
            }

            var fitText = fitElement.GetString();
            if (!Enum.TryParse<PresetFit>(fitText, ignoreCase: true, out var fit))
            {
                throw new InvalidOperationException(
                    $"image-presets.json tại '{filePath}': giá trị 'fit' không hợp lệ '{fitText}' ở preset '{name}' (chỉ nhận 'cover'/'inside').");
            }

            presets[name] = new ImagePreset(name, widthElement.GetInt32(), heightElement.GetInt32(), fit);
        }

        if (presets.Count == 0)
        {
            throw new InvalidOperationException($"image-presets.json tại '{filePath}' không có preset nào.");
        }

        _presets = presets;
    }

    public bool TryGet(string name, out ImagePreset preset)
    {
        if (_presets.TryGetValue(name, out var found))
        {
            preset = found;
            return true;
        }

        preset = null!;
        return false;
    }

    public IReadOnlyCollection<ImagePreset> All => _presets.Values;
}
