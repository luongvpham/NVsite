namespace Vsite.Application.Media;

/// <summary>
/// MEDIA-001 D4 (#88) — tên preset derivative logo dùng để hiển thị (Header). MỘT hằng duy nhất ở phía
/// Media; test khẳng định <c>IDerivativePresetCatalog.For("Shop")</c> chứa nó để lệch cấu hình thì fail.
/// </summary>
public static class LogoPresets
{
    /// <summary>Logo trên Header, không crop (`config/image-presets.json`).</summary>
    public const string Header = "320x96,inside";
}
