using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Vsite.Application.Website.Interfaces;
using Vsite.Application.Website.Versioning;

namespace Vsite.Application.Website.Validation;

public static class ThemeErrorCodes
{
    public const string Invalid = "THEME_INVALID";
    public const string EnvelopeInvalid = "THEME_ENVELOPE_INVALID";
    public const string TooLarge = "THEME_TOKENS_TOO_LARGE";
    public const string TokenInvalid = "THEME_TOKEN_INVALID";
}

/// <param name="CleanTokens">Tài liệu đã nâng lên phiên bản hiện hành (#94) — chỉ có khi hợp lệ.</param>
/// <param name="ProblemCode"><c>THEME_INVALID</c> | <c>SCHEMA_VERSION_UNSUPPORTED</c>; null khi hợp lệ.</param>
public sealed record ThemeTokensResult(IReadOnlyList<TreeValidationError> Errors, string? CleanTokens, string? ProblemCode)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Validate <c>Theme.Tokens</c> (T6/T8, Flow 4, #94, #97): kích thước → cú pháp → phiên bản (upcaster) → JSON Schema
/// sinh từ Zod/<c>theme-tokens.json</c> (whitelist màu/font/scale…). Một nơi duy nhất cho cả <c>CreateWebsite</c> (tokens mặc định)
/// lẫn <c>UpdateTheme</c> — hai đường không thể lệch nhau. Không ném lỗi: trả danh sách {path, code, message}.
/// </summary>
public sealed class ThemeTokensValidator(IWebsiteSchemaProvider schemas, DocumentVersioning versioning)
{
    /// <summary>Tokens chỉ là vài chục khoá — 16 KB là rộng rãi và chặn thứ vô lý trước khi parse sâu.</summary>
    public const int MaxBytes = 16 * 1024;

    public ThemeTokensResult Validate(string tokensJson)
    {
        if (tokensJson is null || tokensJson.Length > MaxBytes || Encoding.UTF8.GetByteCount(tokensJson) > MaxBytes)
        {
            return Fail(ThemeErrorCodes.TooLarge, $"Tokens vượt quá {MaxBytes} byte.");
        }

        try
        {
            var root = JsonNode.Parse(tokensJson); // khoá trùng → ArgumentException
            var upcast = versioning.Upcast(DocumentKind.Tokens, root);
            switch (upcast.Status)
            {
                case UpcastStatus.Malformed:
                    return Fail(ThemeErrorCodes.EnvelopeInvalid, upcast.Message ?? "Tokens không hợp lệ.");

                case UpcastStatus.Unsupported:
                    return new ThemeTokensResult(
                        [new TreeValidationError("/schemaVersion", TreeErrorCodes.SchemaVersionUnsupported, upcast.Message ?? "Phiên bản không được hỗ trợ.")],
                        null,
                        TreeErrorCodes.SchemaVersionUnsupported);
            }

            var text = upcast.Document!.ToJsonString(); // surrogate lẻ ném InvalidOperationException
            var violations = schemas.ValidateThemeTokens(text);
            if (violations.Count > 0)
            {
                return new ThemeTokensResult(
                    [.. violations.Select(v => new TreeValidationError(v.Path, ThemeErrorCodes.TokenInvalid, v.Message))],
                    null,
                    ThemeErrorCodes.Invalid);
            }

            return new ThemeTokensResult([], text, null);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException or FormatException)
        {
            return Fail(ThemeErrorCodes.EnvelopeInvalid, "Tokens không phải JSON hợp lệ.");
        }
    }

    private static ThemeTokensResult Fail(string code, string message) =>
        new([new TreeValidationError("", code, message)], null, ThemeErrorCodes.Invalid);
}
