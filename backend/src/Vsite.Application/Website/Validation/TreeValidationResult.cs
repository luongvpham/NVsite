namespace Vsite.Application.Website.Validation;

/// <param name="Path">JSON Pointer tới chỗ lỗi trong cây gửi lên (rỗng = cả tài liệu).</param>
public sealed record TreeValidationError(string Path, string Code, string Message);

/// <summary>Mã lỗi chi tiết của cây (khớp bảng ở packages/builder-components/tests/fixtures/tree/README.md).</summary>
public static class TreeErrorCodes
{
    public const string Invalid = "TREE_INVALID";
    public const string EnvelopeInvalid = "TREE_ENVELOPE_INVALID";
    public const string SchemaVersionUnsupported = "SCHEMA_VERSION_UNSUPPORTED";
    public const string BodyTooLarge = "TREE_BODY_TOO_LARGE";
    public const string TooDeep = "TREE_TOO_DEEP";
    public const string TooManyNodes = "TREE_TOO_MANY_NODES";
    public const string NodeTypeUnknown = "NODE_TYPE_UNKNOWN";
    public const string NodeVariantUnknown = "NODE_VARIANT_UNKNOWN";
    public const string NodeIdDuplicate = "NODE_ID_DUPLICATE";
    public const string NodeNotAllowedInPageKind = "NODE_NOT_ALLOWED_IN_PAGE_KIND";
    public const string NodeChildrenNotAccepted = "NODE_CHILDREN_NOT_ACCEPTED";
    public const string NodeChildTypeNotAllowed = "NODE_CHILD_TYPE_NOT_ALLOWED";
    public const string NodeMaxPerPageExceeded = "NODE_MAX_PER_PAGE_EXCEEDED";
    public const string PropUnknown = "PROP_UNKNOWN";
    public const string PropInvalid = "PROP_INVALID";
    public const string I18nNotEnabled = "I18N_NOT_ENABLED";
    public const string BindingSourceNotAllowed = "BINDING_SOURCE_NOT_ALLOWED";
    public const string RichTextUnsafe = "RICHTEXT_UNSAFE";
    public const string LinkUrlUnsafe = "LINK_URL_UNSAFE";
    public const string SystemPageStructureChanged = "SYSTEM_PAGE_STRUCTURE_CHANGED";
    public const string SystemPagePropNotEditable = "SYSTEM_PAGE_PROP_NOT_EDITABLE";
}

/// <summary>
/// Kết quả validate cây — KHÔNG ném lỗi ở lần đầu thấy (T4). <see cref="CleanTree"/> chỉ có khi không có lỗi: cây
/// (đã nâng phiên bản, richText đã thay bằng bản sạch) để lưu. <see cref="ImageIds"/> là mọi <c>imageId</c> thu được
/// (kể cả trong list/group), để T7 đưa cho <c>IMediaReferenceValidator</c>.
/// </summary>
public sealed record TreeValidationResult(
    IReadOnlyList<TreeValidationError> Errors,
    IReadOnlyList<string> ImageIds,
    string? CleanTree,
    bool Sanitized,
    bool ErrorsTruncated)
{
    public bool IsValid => Errors.Count == 0;

    /// <summary>Mã cấp problem (HTTP 422 <c>error_code</c>); null khi hợp lệ.</summary>
    public string? ProblemCode =>
        Errors.Count == 0 ? null
        : Errors.Any(e => e.Code == TreeErrorCodes.SchemaVersionUnsupported) ? TreeErrorCodes.SchemaVersionUnsupported
        : TreeErrorCodes.Invalid;
}
