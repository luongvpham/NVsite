using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Vsite.Application.Website.Validation;

/// <summary>
/// Luật url của prop kind <c>link</c> (kind <c>external</c>) — chặn stored XSS ở renderer (WEBSITE-001, review đợt B).
/// Hợp lệ: scheme https/http/mailto/tel, hoặc đường dẫn nội bộ <c>/x</c> (KHÔNG <c>//host</c>, không <c>/\host</c>), hoặc
/// <c>#neo</c>. Chuẩn hoá TRƯỚC khi so (giải mã entity, bỏ ký tự điều khiển/khoảng trắng, không phân biệt hoa/thường) vì
/// trình duyệt cũng làm vậy: <c>JaVa&#9;Script:</c> vẫn là <c>javascript:</c>. Phản chiếu <c>isSafeLinkUrl</c> ở
/// <c>packages/builder-components/tests/helpers/reference-tree-validator.ts</c> — hai phía chạy cùng fixture.
/// </summary>
public static partial class LinkUrlRules
{
    private static readonly string[] AllowedSchemes = ["https", "http", "mailto", "tel"];

    [GeneratedRegex(@"&#x([0-9a-f]+);?|&#([0-9]+);?|&(colon|tab|newline|lpar|rpar);", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EntityRegex();

    [GeneratedRegex(@"^([a-z][a-z0-9+.\-]*):", RegexOptions.CultureInvariant)]
    private static partial Regex SchemeRegex();

    public static bool IsSafe(string? url)
    {
        if (url is null)
        {
            return false;
        }

        var decoded = EntityRegex().Replace(url, DecodeEntity);
        var sb = new StringBuilder(decoded.Length);
        foreach (var c in decoded)
        {
            if (c > ' ' && c != '\u007f')
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        var normalized = sb.ToString();
        var scheme = SchemeRegex().Match(normalized);
        if (scheme.Success)
        {
            return AllowedSchemes.Contains(scheme.Groups[1].Value);
        }

        if (normalized.StartsWith('#'))
        {
            return true;
        }

        return normalized.StartsWith('/') && !normalized.StartsWith("//", StringComparison.Ordinal) && !normalized.StartsWith("/\\", StringComparison.Ordinal);
    }

    private static string DecodeEntity(Match m)
    {
        if (m.Groups[3].Success)
        {
            return m.Groups[3].Value.ToLowerInvariant() switch
            {
                "colon" => ":",
                "tab" => "\t",
                "newline" => "\n",
                "lpar" => "(",
                _ => ")",
            };
        }

        var number = m.Groups[1].Success
            ? int.TryParse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var h) ? h : -1
            : int.TryParse(m.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var d) ? d : -1;
        return number is >= 0 and <= 0x10FFFF && !(number is >= 0xD800 and <= 0xDFFF)
            ? char.ConvertFromUtf32(number)
            : "�"; // không giải mã được → ký tự thay thế (không bao giờ khớp scheme hợp lệ)
    }
}
