namespace Vsite.Domain.Media.Enums;

/// <summary>
/// REFACTOR-DB-001 — vai trò của một record <c>MediaAsset</c>. Trước đây suy ra từ
/// <c>IsInLibrary</c> + <c>Preset</c> + <c>SourceAssetId</c>, nhưng <see cref="Clone"/> và
/// <see cref="Derivative"/> có CÙNG bộ ba đó (<c>false</c>, preset, id bản Library) — resolver phái
/// sinh có thể trả nhầm crop của một slot, và job dọn clone không tham chiếu (#72) có thể xoá nhầm
/// phái sinh thật.
/// </summary>
public enum MediaAssetKind
{
    /// <summary>Bản gốc trong Media Library, chưa crop (<c>IsInLibrary = true</c>).</summary>
    Library,

    /// <summary>Upload thẳng vào slot, đã crop, không giữ bản gốc (<c>SourceAssetId = null</c>).</summary>
    Direct,

    /// <summary>Bản crop độc lập từ Library để đặt vào một slot của Component Tree (#71/#72) — có
    /// focal point riêng, mỗi slot một bản.</summary>
    Clone,

    /// <summary>Phái sinh của ảnh nghiệp vụ (vd. logo shop, #73) — đúng MỘT bản cho mỗi
    /// (bản Library, preset), sinh tự động theo `derivative-presets.json`.</summary>
    Derivative,
}
