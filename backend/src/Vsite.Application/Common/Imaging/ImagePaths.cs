namespace Vsite.Application.Common.Imaging;

/// <summary>
/// Build/validate object-storage key cho ảnh (T2 + T3, MEDIA-001). <see cref="ValidateKey"/> từ T2;
/// phần còn lại (build key theo entity, thumb/featured-thumb, kiểm subfolder) do T3 thêm.
/// </summary>
public static class ImagePaths
{
    /// <summary>Key cho ảnh website (banner, ảnh trong nội dung) — <c>shops/{shopId}/website/{yyyy}/{MM}/{uuid}.webp</c>.</summary>
    public static string NewWebsiteKey(Guid shopId, DateTimeOffset now) =>
        $"shops/{shopId}/website/{now:yyyy}/{now:MM}/{Guid.NewGuid()}.webp";

    /// <summary>Folder chứa mọi ảnh của một Listing — <c>shops/{shopId}/listings/{id}/</c>.</summary>
    public static string ListingFolder(Guid shopId, Guid listingId) =>
        $"shops/{shopId}/listings/{listingId}/";

    /// <summary>Folder chứa mọi ảnh của một Product — <c>shops/{shopId}/products/{id}/</c>.</summary>
    public static string ProductFolder(Guid shopId, Guid productId) =>
        $"shops/{shopId}/products/{productId}/";

    /// <summary>Folder chứa mọi ảnh của một attribute value — <c>shops/{shopId}/attributes/{id}/</c>.</summary>
    public static string AttributeFolder(Guid shopId, Guid attributeId) =>
        $"shops/{shopId}/attributes/{attributeId}/";

    /// <summary>Key file full mới trong một folder đã có sẵn — <c>{folder}{uuid}.webp</c>.</summary>
    public static string NewFullKey(string folder) => $"{folder}{Guid.NewGuid()}.webp";

    /// <summary>Key thumb (đứng cạnh key full trong cùng folder) — chèn <c>thumb_</c> trước filename.</summary>
    public static string Thumb(string fullKey) => WithFileNamePrefix(fullKey, "thumb_");

    /// <summary>Key featured-thumb — chèn <c>fthumb_</c> trước filename.</summary>
    public static string FeaturedThumb(string fullKey) => WithFileNamePrefix(fullKey, "fthumb_");

    /// <summary>
    /// True nếu <paramref name="key"/> nằm trong <paramref name="folder"/> — dùng cho kiểm ownership
    /// khi xoá/di chuyển ảnh (#81). <paramref name="folder"/> PHẢI kết thúc bằng <c>/</c> (mọi
    /// <c>*Folder</c> method ở trên đều trả về dạng đó) — nhờ vậy so khớp prefix đơn giản
    /// (<c>string.StartsWith</c>) đã đủ an toàn, không bị đánh lừa bởi folder tên gần giống
    /// (vd. <c>.../b-evil/...</c> so với folder <c>.../b/</c>, vì ký tự ngay sau "b" trong key phải
    /// khớp đúng '/' của folder).
    /// </summary>
    public static bool IsUnder(string key, string folder) => key.StartsWith(folder, StringComparison.Ordinal);

    private static string WithFileNamePrefix(string fullKey, string prefix)
    {
        var slashIndex = fullKey.LastIndexOf('/');
        var directory = slashIndex >= 0 ? fullKey[..(slashIndex + 1)] : string.Empty;
        var fileName = slashIndex >= 0 ? fullKey[(slashIndex + 1)..] : fullKey;
        return $"{directory}{prefix}{fileName}";
    }

    /// <summary>
    /// Key phải bắt đầu bằng <c>shops/</c>, không chứa segment <c>..</c>, không chứa <c>\</c> hay
    /// <c>//</c>, không có ký tự điều khiển, và không bắt đầu bằng <c>/</c>. Ném
    /// <see cref="ArgumentException"/> nếu vi phạm bất kỳ luật nào — không tự sửa/normalize key.
    /// </summary>
    public static void ValidateKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key không được rỗng.", nameof(key));
        }

        if (key.StartsWith('/'))
        {
            throw new ArgumentException("Key không được bắt đầu bằng '/'.", nameof(key));
        }

        if (!key.StartsWith("shops/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Key phải bắt đầu bằng 'shops/'.", nameof(key));
        }

        if (key.Contains('\\'))
        {
            throw new ArgumentException("Key không được chứa '\\'.", nameof(key));
        }

        if (key.Contains("//", StringComparison.Ordinal))
        {
            throw new ArgumentException("Key không được chứa '//'.", nameof(key));
        }

        foreach (var c in key)
        {
            if (char.IsControl(c))
            {
                throw new ArgumentException("Key không được chứa ký tự điều khiển.", nameof(key));
            }
        }

        foreach (var segment in key.Split('/'))
        {
            if (segment == "..")
            {
                throw new ArgumentException("Key không được chứa segment '..'.", nameof(key));
            }
        }
    }
}
