using FluentValidation;
using Vsite.Domain.ReservedRoutes;
using Vsite.Domain.Shop.Enums;

namespace Vsite.Application.Shop.Commands.CreateShop;

/// <summary>SHOP-001 §4.3 — slug không được nằm trong `config/reserved-routes.json` (Quyết định
/// #8, #24 — một nguồn duy nhất, đọc qua <see cref="IReservedRoutesProvider"/>, không viết tay
/// danh sách thứ hai), đúng format, và ràng buộc `ExternalOnly ⇒ ExternalUrl` (04 §2.1).</summary>
public sealed class CreateShopValidator : AbstractValidator<CreateShopCommand>
{
    private const string SlugPattern = "^[a-z0-9]+(-[a-z0-9]+)*$";

    public CreateShopValidator(IReservedRoutesProvider reservedRoutes)
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);

        RuleFor(x => x.Slug)
            .NotEmpty()
            .MinimumLength(3).MaximumLength(63)
            .Matches(SlugPattern)
                .WithMessage("Slug chỉ gồm chữ thường, số, dấu '-', không bắt đầu/kết thúc bằng '-'.")
            .Must(slug => !IsReserved(slug, reservedRoutes))
                .WithMessage("Slug này đã được hệ thống dùng riêng.");

        RuleFor(x => x.Kind).IsInEnum();

        RuleFor(x => x.ExternalUrl)
            .NotEmpty()
            .When(x => x.Kind == ShopKind.ExternalOnly)
            .WithMessage("ExternalUrl bắt buộc khi Kind = ExternalOnly.");
    }

    private static bool IsReserved(string slug, IReservedRoutesProvider reservedRoutes) =>
        reservedRoutes.Routes.ReservedPaths.Contains(slug) || reservedRoutes.Routes.ReservedSubdomains.Contains(slug);
}
