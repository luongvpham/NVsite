using FluentValidation;
using MediatR;
using Vsite.Application.Website.Dtos;
using Vsite.Domain.Website.Entities;

namespace Vsite.Application.Website.Commands.UpdatePage;

/// <summary>
/// Sửa metadata trang (Flow 3). Ngữ nghĩa GHI ĐÈ cho mọi trường trừ <paramref name="Slug"/>: <c>null</c> = không đổi slug (SeoTitle/
/// SeoDescription/OgImageId <c>null</c> = xoá). Trang System chỉ được đổi title/SEO/isEnabled/noIndex — gửi slug khác hiện tại → 422
/// <c>PAGE_SYSTEM_SLUG_FIXED</c> (trang chủ: <c>PAGE_HOME_SLUG_FIXED</c>). Kind/SystemType không nhận từ client.
/// </summary>
public sealed record UpdatePageCommand(
    Guid PageId,
    string? Slug,
    string Title,
    string? SeoTitle,
    string? SeoDescription,
    Guid? OgImageId,
    bool NoIndex,
    bool IsEnabled) : IRequest<PageDto>;

public sealed class UpdatePageValidator : AbstractValidator<UpdatePageCommand>
{
    public UpdatePageValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(Page.MaxTitleLength);
        RuleFor(x => x.SeoTitle).MaximumLength(Page.MaxSeoTitleLength);
        RuleFor(x => x.SeoDescription).MaximumLength(Page.MaxSeoDescriptionLength);
    }
}
