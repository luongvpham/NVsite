namespace Vsite.Application.Common.Imaging;

/// <summary>
/// Validate object-storage key cho ảnh (T2, MEDIA-001). Chỉ có <see cref="ValidateKey"/> ở task
/// này — các member khác (build key theo entity, tách extension, v.v.) do T3 thêm sau vào cùng
/// class.
/// </summary>
public static class ImagePaths
{
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
