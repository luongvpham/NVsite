using Vsite.Application.Website.Validation;
using Vsite.Domain.Website.Enums;

namespace Vsite.Infrastructure.Website;

/// <summary>
/// Một chỗ duy nhất đọc <c>imageId</c> trong tree draft (walker của <see cref="PageTreeValidator"/>), dùng chung cho
/// <see cref="PageDraftReferenceSource"/> và <see cref="PageDraftUsageSource"/>. So theo <see cref="Guid"/> (hoa/thường không quan trọng);
/// id không phải GUID bị bỏ qua (không thể khớp asset nào). <paramref name="strict"/>: tree không qua validator thì NÉM thay vì trả rỗng
/// (validator trả danh sách ảnh rỗng khi lỗi) — bắt buộc cho đường quyết định xoá.
/// </summary>
internal static class PageTreeImageIds
{
    public static IEnumerable<Guid> Read(PageTreeValidator validator, string tree, PageKind kind, bool strict)
    {
        var result = validator.Validate(tree, kind);
        if (strict && !result.IsValid)
        {
            throw new InvalidOperationException(
                $"Tree draft không đọc được để thu ảnh đang dùng: {result.Errors[0].Code} {result.Errors[0].Message}");
        }

        foreach (var raw in result.ImageIds)
        {
            if (Guid.TryParse(raw, out var id))
            {
                yield return id;
            }
        }
    }
}
