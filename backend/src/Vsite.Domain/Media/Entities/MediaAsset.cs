using Vsite.Domain.Common;
using Vsite.Domain.Exceptions;

namespace Vsite.Domain.Media.Entities;

/// <summary>
/// `08-media-asset-design.md` §2 — một bảng duy nhất cho cả bản Library, ảnh upload thẳng và
/// clone/phái sinh (Quyết định #69), phân biệt bằng <see cref="IsInLibrary"/> + <see cref="Preset"/>.
///
/// Hai điểm lệch có chủ đích so với `08` §2 (task T4, xem `Docs/tasks/MEDIA-001/changelog.md`):
/// - Dùng <see cref="BaseAuditableEntity.IsDeleted"/> của base class thay cho cột `DeletedAt`.
/// - Tên bảng `MediaAsset` (số ít, PascalCase) theo quy ước bảng hiện có (`Shop`, `UserShop`),
///   không dùng `media_assets`.
///
/// `08` §2 mô tả factory nhận <c>EncodedImage</c>/<c>FocalPoint</c> (kiểu của
/// <c>Vsite.Application.Common.Imaging</c>) — Domain không được reference Application
/// (architecture-guide.md §1), nên factory ở đây nhận primitive tương đương; caller ở tầng
/// Application tự destructure trước khi gọi.
/// </summary>
public sealed class MediaAsset : ShopAuditableEntity
{
    public string StorageKey { get; private set; } = null!;

    /// <summary>Luôn là <c>"image/webp"</c> sau xử lý (Quyết định #53) — không nhận từ caller.</summary>
    public string MimeType { get; private set; } = "image/webp";

    public int Width { get; private set; }

    public int Height { get; private set; }

    public long SizeBytes { get; private set; }

    public string? AltText { get; set; }

    public float FocalPointX { get; private set; } = 0.5f;

    public float FocalPointY { get; private set; } = 0.5f;

    public string? OriginalFileName { get; private set; }

    /// <summary>Chỉ có nghĩa khi <see cref="IsInLibrary"/> = true (08 §2).</summary>
    public string? Folder { get; set; }

    public bool IsInLibrary { get; private set; }

    /// <summary><c>NULL</c> ⟺ <see cref="IsInLibrary"/> = true (CHECK ck_media_library_preset).</summary>
    public string? Preset { get; private set; }

    /// <summary>Clone/phái sinh sinh từ bản Library nào. <c>NULL</c> = bản gốc (Library hoặc upload
    /// thẳng). Không phải quan hệ sống — <c>ON DELETE SET NULL</c>.</summary>
    public Guid? SourceAssetId { get; private set; }

    private MediaAsset()
    {
    }

    /// <summary>Bản Library — chưa crop, ≤1600px cạnh dài, giữ tỉ lệ gốc.</summary>
    public static MediaAsset NewLibrary(
        Guid shopId,
        string storageKey,
        int width,
        int height,
        long sizeBytes,
        float focalX,
        float focalY,
        string? originalFileName,
        string? altText)
    {
        ValidateFocal(focalX, focalY);

        return new MediaAsset
        {
            ShopId = shopId,
            StorageKey = storageKey,
            Width = width,
            Height = height,
            SizeBytes = sizeBytes,
            FocalPointX = focalX,
            FocalPointY = focalY,
            OriginalFileName = originalFileName,
            AltText = altText,
            IsInLibrary = true,
            Preset = null,
            SourceAssetId = null,
        };
    }

    /// <summary>Ảnh upload thẳng vào slot (không tick "Lưu vào thư viện") — đã crop theo
    /// <paramref name="preset"/>, không giữ bản gốc, <see cref="SourceAssetId"/> = null.</summary>
    public static MediaAsset NewDirect(
        Guid shopId,
        string storageKey,
        int width,
        int height,
        long sizeBytes,
        string preset,
        float focalX,
        float focalY,
        string? originalFileName,
        string? altText)
    {
        ValidateFocal(focalX, focalY);

        return new MediaAsset
        {
            ShopId = shopId,
            StorageKey = storageKey,
            Width = width,
            Height = height,
            SizeBytes = sizeBytes,
            FocalPointX = focalX,
            FocalPointY = focalY,
            OriginalFileName = originalFileName,
            AltText = altText,
            IsInLibrary = false,
            Preset = preset,
            SourceAssetId = null,
        };
    }

    /// <summary>Clone/phái sinh từ một bản Library. Ném <see cref="DomainException"/>
    /// (<c>MEDIA_CLONE_FROM_CLONE</c>) nếu <paramref name="source"/> không phải bản Library
    /// (Quyết định #71 — Component Tree không bao giờ chứa id của một clone khác).</summary>
    public static MediaAsset NewDerived(
        MediaAsset source,
        string storageKey,
        int width,
        int height,
        long sizeBytes,
        string preset,
        float focalX,
        float focalY)
    {
        if (!source.IsInLibrary)
        {
            throw new DomainException(
                "MEDIA_CLONE_FROM_CLONE",
                "Chỉ được clone/phái sinh từ bản Library, không được clone từ một clone khác.");
        }

        ValidateFocal(focalX, focalY);

        return new MediaAsset
        {
            ShopId = source.ShopId,
            StorageKey = storageKey,
            Width = width,
            Height = height,
            SizeBytes = sizeBytes,
            FocalPointX = focalX,
            FocalPointY = focalY,
            OriginalFileName = source.OriginalFileName,
            AltText = source.AltText,
            IsInLibrary = false,
            Preset = preset,
            SourceAssetId = source.Id,
        };
    }

    /// <summary>
    /// T6, MEDIA-001 (#71/#72, review fix) — soft-delete bản Library. GỌI THẲNG method này, KHÔNG
    /// bao giờ qua `DbSet.Remove()`: `Remove()` đưa entity vào `EntityState.Deleted`, và với FK
    /// self-reference `SourceAssetId` cấu hình `OnDelete(DeleteBehavior.SetNull)`
    /// (`MediaAssetConfiguration`), EF Core cascade fix-up sẽ set `SourceAssetId` của MỌI clone ĐANG
    /// TRACKED trong CÙNG context về `null` NGAY LẬP TỨC — trước khi
    /// `AppDbContext.InterceptSoftDelete()` kịp chặn lại state của record NÀY thành `Modified`. Kết
    /// quả: clone bị mất `SourceAssetId` thật (không phải chỉ ở bộ nhớ) — vi phạm #72 (clone của một
    /// bản Library đã xoá vẫn phải giữ nguyên `SourceAssetId`, không biến thành ảnh "mồ côi" trông
    /// giống upload thẳng).
    ///
    /// Set `IsDeleted` trực tiếp giữ entity ở `EntityState.Modified` NGAY TỪ ĐẦU — không bao giờ đi
    /// qua `EntityState.Deleted`, nên không kích hoạt cascade fix-up. `UpdatedAt` không cần set tay ở
    /// đây — `AppDbContext.StampAuditFields()` tự stamp cho MỌI entry `Modified`.
    /// </summary>
    public void SoftDeleteFromLibrary()
    {
        IsDeleted = true;
    }

    private static void ValidateFocal(float x, float y)
    {
        if (x is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, "FocalPointX phải nằm trong [0, 1].");
        }

        if (y is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(y), y, "FocalPointY phải nằm trong [0, 1].");
        }
    }
}
