using System.Text.Json;

namespace Vsite.Infrastructure.Website;

/// <summary>Đọc + parse file config/artifact của module Website — thiếu file hay sai shape thì ném
/// exception NÊU TÊN FILE (Program.cs resolve eager để app không lên với catalog hỏng).</summary>
internal static class WebsiteConfigFile
{
    public static string ReadText(string filePath)
    {
        if (!File.Exists(filePath))
        {
            var name = Path.GetFileName(filePath);
            throw new FileNotFoundException(
                $"{name} không tìm thấy tại '{filePath}'. Kiểm tra <None Link> trong Vsite.Api.csproj; " +
                "artifact trong packages/builder-components/generated/ thì chạy `pnpm --filter @vsite/builder-components run gen:registry`. " +
                $"/ Missing {name}.",
                filePath);
        }

        return File.ReadAllText(filePath);
    }

    public static JsonDocument Parse(string filePath)
    {
        var text = ReadText(filePath);
        try
        {
            return JsonDocument.Parse(text);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{Path.GetFileName(filePath)} tại '{filePath}' không phải JSON hợp lệ: {ex.Message}", ex);
        }
    }

    public static InvalidOperationException BadShape(string filePath, string detail) =>
        new($"{Path.GetFileName(filePath)} tại '{filePath}' sai shape: {detail}");

    /// <summary>Mảng string (không rỗng nếu <paramref name="allowEmpty"/> = false).</summary>
    public static IReadOnlyList<string> StringArray(JsonElement element, string filePath, string what, bool allowEmpty = false)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw BadShape(filePath, $"'{what}' phải là mảng string.");
        }

        var list = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw BadShape(filePath, $"'{what}' chỉ được chứa string.");
            }

            list.Add(item.GetString()!);
        }

        if (!allowEmpty && list.Count == 0)
        {
            throw BadShape(filePath, $"'{what}' không được rỗng.");
        }

        return list;
    }

    public static JsonElement Required(JsonElement obj, string filePath, string name, JsonValueKind kind)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var value) || value.ValueKind != kind)
        {
            throw BadShape(filePath, $"thiếu hoặc sai kiểu '{name}' (kỳ vọng {kind}).");
        }

        return value;
    }
}
