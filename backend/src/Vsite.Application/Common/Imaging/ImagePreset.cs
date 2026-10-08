namespace Vsite.Application.Common.Imaging;

/// <summary>
/// Một preset kích thước/cách crop ảnh đã cấu hình sẵn (T3, MEDIA-001, Quyết định #78). Đọc từ
/// <c>config/image-presets.json</c> qua <see cref="IImagePresetCatalog"/> — <see cref="Name"/> là
/// key trong file đó (vd. <c>"320x96,inside"</c>).
/// </summary>
public sealed record ImagePreset(string Name, int Width, int Height, PresetFit Fit)
{
    public ImageTransform ToTransform(FocalPoint focal) => Fit == PresetFit.Cover
        ? new ImageTransform.Cover(Width, Height, focal)
        : new ImageTransform.Inside(Width, Height);
}

public enum PresetFit
{
    Cover,
    Inside,
}

/// <summary>Toàn bộ preset đã cấu hình — nguồn sự thật duy nhất <c>config/image-presets.json</c>
/// (Quyết định #78). Thiếu file hoặc sai shape → ném exception lúc dựng (fail fast), không chạy tiếp
/// với danh sách rỗng.</summary>
public interface IImagePresetCatalog
{
    bool TryGet(string name, out ImagePreset preset);

    IReadOnlyCollection<ImagePreset> All { get; }
}

/// <summary>
/// Danh sách derivative cần pre-generate cho một loại nguồn nghiệp vụ (Shop logo, Service ảnh, v.v.
/// — Quyết định #86). Nguồn sự thật duy nhất
/// <c>packages/builder-components/generated/derivative-presets.json</c> — artifact FULL SET (manifest
/// ∪ surfaces đã build sẵn); BE KHÔNG đọc lại <c>surfaces</c> trong <c>config/image-presets.json</c>.
/// </summary>
public interface IDerivativePresetCatalog
{
    /// <summary>Rỗng nếu <paramref name="source"/> không có derivative nào cấu hình.</summary>
    IReadOnlyList<string> For(string source);
}
