using FluentValidation;
using MediatR;
using Vsite.Application.Website.Dtos;

namespace Vsite.Application.Website.Commands.SavePageDraft;

/// <summary>
/// Autosave draft (Flow 2). <paramref name="TreeJson"/> là văn bản JSON của cây (endpoint đọc body thô để PageTreeValidator tự áp
/// giới hạn kích thước/độ sâu trước khi parse sâu); <paramref name="Version"/> là token <c>xmin</c> client nhận từ <c>GET draft</c>
/// hoặc lần lưu trước — opaque, gửi lại nguyên văn (G5). Không mang <c>ShopId</c> (#99).
/// </summary>
public sealed record SavePageDraftCommand(Guid PageId, string TreeJson, long Version) : IRequest<SavePageDraftResultDto>;

public sealed class SavePageDraftValidator : AbstractValidator<SavePageDraftCommand>
{
    public SavePageDraftValidator()
    {
        RuleFor(x => x.TreeJson).NotNull();
        RuleFor(x => x.Version).GreaterThanOrEqualTo(0);
    }
}
