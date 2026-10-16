using FluentValidation;
using MediatR;
using Vsite.Application.Website.Dtos;
using Vsite.Domain.Website.Entities;

namespace Vsite.Application.Website.Commands.CreatePage;

/// <summary>
/// Tạo trang Composable (Flow 3, R10). KHÔNG có <c>Kind</c>/<c>SystemType</c>/<c>ShopId</c>/<c>WebsiteId</c>: kind luôn
/// <c>Composable</c> (System page chỉ sinh từ seed), shop lấy từ <c>ITenantContext</c> (#99, #21.4). Slug sai/chiếm chỗ/trùng là
/// việc của <c>SlugRules</c> và handler (cần catalog + DB); validator này chỉ kiểm độ dài chữ.
/// </summary>
public sealed record CreatePageCommand(
    string Slug,
    string Title,
    string? SeoTitle = null,
    string? SeoDescription = null,
    Guid? OgImageId = null,
    bool NoIndex = false,
    bool IsEnabled = true) : IRequest<PageDto>;

public sealed class CreatePageValidator : AbstractValidator<CreatePageCommand>
{
    public CreatePageValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(Page.MaxTitleLength);
        RuleFor(x => x.SeoTitle).MaximumLength(Page.MaxSeoTitleLength);
        RuleFor(x => x.SeoDescription).MaximumLength(Page.MaxSeoDescriptionLength);
    }
}
