using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using Ganss.Xss;
using Vsite.Application.Website.Interfaces;

namespace Vsite.Infrastructure.Website;

/// <summary>
/// <see cref="IRichTextSanitizer"/> bằng thư viện <c>HtmlSanitizer</c> (Ganss.Xss, MIT, parser AngleSharp — G4), cấu hình
/// từ <c>config/sanitize-profiles.json</c>: thẻ/thuộc tính theo profile của prop, scheme chỉ https/http/mailto/tel,
/// <c>target=_blank</c> bị ép <c>rel="noopener noreferrer"</c>. <see cref="RichTextSanitizeResult.Unsafe"/> bật qua SỰ KIỆN
/// của thư viện (thẻ/thuộc tính/URL bị cắt) khi thứ bị cắt thuộc <c>dangerous</c> — không so chuỗi trước/sau (R5).
/// </summary>
public sealed class RichTextSanitizer(ISanitizeProfileCatalog catalog) : IRichTextSanitizer
{
    private static readonly string[] AllowedSchemes = ["https", "http", "mailto", "tel"];

    public RichTextSanitizeResult Sanitize(string html, string profile)
    {
        if (!catalog.Profiles.TryGetValue(profile, out var rules))
        {
            throw new ArgumentException($"Profile '{profile}' không có trong sanitize-profiles.json.", nameof(profile));
        }

        var danger = catalog.Dangerous;
        var dangerous = false;

        // Thư viện cho phép thuộc tính theo tập CHUNG; ràng buộc theo từng thẻ làm ở PostProcessNode.
        var allowedAttrNames = rules.Attrs.Values.SelectMany(v => v).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var options = new HtmlSanitizerOptions
        {
            AllowedTags = new HashSet<string>(rules.Tags, StringComparer.OrdinalIgnoreCase),
            AllowedAttributes = allowedAttrNames,
            AllowedSchemes = new HashSet<string>(AllowedSchemes, StringComparer.OrdinalIgnoreCase),
            AllowedCssProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            // HtmlSanitizerOptions KHÔNG tự điền mặc định (khác ctor rỗng của HtmlSanitizer): thiếu dòng này thì href/src
            // không bị kiểm scheme — javascript:/ftp: lọt qua nguyên vẹn.
            UriAttributes = new HashSet<string>(HtmlSanitizerDefaults.UriAttributes, StringComparer.OrdinalIgnoreCase),
            AllowedCssClasses = new HashSet<string>(StringComparer.Ordinal),
            AllowedAtRules = new HashSet<CssRuleType>(),
        };

        // Một instance mỗi lần gọi: cờ `dangerous` là trạng thái của lần gọi, không chia sẻ giữa các request.
        var sanitizer = new HtmlSanitizer(options) { KeepChildNodes = true };
        var uriAttributes = sanitizer.UriAttributes;

        sanitizer.RemovingTag += (_, e) =>
        {
            // Thẻ bị cắt cũng mang theo thuộc tính của nó (<img src=x onerror=...>): thuộc tính nguy hiểm trên thẻ bị cắt
            // vẫn là thứ nguy hiểm đã bị cắt.
            if (e.Tag.Attributes.Any(a => IsDangerousAttribute(a, uriAttributes, danger)))
            {
                dangerous = true;
            }

            if (danger.Tags.Contains(e.Tag.NodeName, StringComparer.OrdinalIgnoreCase))
            {
                dangerous = true;
                // KeepChildNodes giữ chữ của thẻ bị cắt; với thẻ nguy hiểm thì bỏ luôn ruột (không để mã script thành văn bản).
                e.Tag.InnerHtml = string.Empty;
            }
        };
        sanitizer.RemovingAttribute += (_, e) =>
        {
            if (IsDangerousAttribute(e.Attribute, uriAttributes, danger))
            {
                dangerous = true;
            }
        };
        sanitizer.FilterUrl += (_, e) =>
        {
            if (e.SanitizedUrl is null && HasDangerousScheme(e.OriginalUrl, danger))
            {
                dangerous = true;
            }
        };
        sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is not IElement element)
            {
                return;
            }

            if (rules.Attrs.TryGetValue(element.LocalName, out var allowedHere) is false)
            {
                allowedHere = [];
            }

            foreach (var attribute in element.Attributes.ToList())
            {
                if (!allowedHere!.Contains(attribute.Name, StringComparer.OrdinalIgnoreCase))
                {
                    element.RemoveAttribute(attribute.Name);
                }
            }

            if (element.LocalName == "a"
                && string.Equals(element.GetAttribute("target"), "_blank", StringComparison.OrdinalIgnoreCase))
            {
                element.SetAttribute("rel", "noopener noreferrer");
            }
        };

        var clean = sanitizer.Sanitize(html);
        return new RichTextSanitizeResult(clean, dangerous);
    }

    private static bool IsDangerousAttribute(IAttr attribute, ISet<string> uriAttributes, DangerousSanitizeRules danger)
    {
        if (danger.AttrPrefixes.Any(prefix => attribute.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return uriAttributes.Contains(attribute.Name) && HasDangerousScheme(attribute.Value, danger);
    }

    /// <summary>
    /// Scheme của một URL bị cắt, theo cách trình duyệt đọc: bỏ mọi ký tự điều khiển/khoảng trắng (<c>jav&#9;ascript:</c>),
    /// không phân biệt hoa/thường. AngleSharp đã giải mã entity trước khi tới đây.
    /// </summary>
    private static bool HasDangerousScheme(string? url, DangerousSanitizeRules danger)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        var normalized = new string(url.Where(c => c > ' ' && c != '\u007f').ToArray());
        var colon = normalized.IndexOf(':');
        if (colon <= 0)
        {
            return false;
        }

        var scheme = normalized[..colon];
        return danger.Schemes.Contains(scheme, StringComparer.OrdinalIgnoreCase);
    }
}
