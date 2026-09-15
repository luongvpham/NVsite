using FluentValidation;
using Vsite.Domain.ReservedRoutes;
using Vsite.Domain.Shop.Enums;

namespace Vsite.Application.Shop.Commands.UpdateShop;

/// <summary>Cùng quy tắc slug với <see cref="Vsite.Application.Shop.Commands.CreateShop.CreateShopValidator"/>
/// (SHOP-001 §4.3) — đổi Slug vẫn phải đúng format/không reserved, dù giữ nguyên slug cũ.</summary>
public sealed class UpdateShopValidator : AbstractValidator<UpdateShopCommand>
{
    private const string SlugPattern = "^[a-z0-9]+(-[a-z0-9]+)*$";

    public UpdateShopValidator(IReservedRoutesProvider reservedRoutes)
    {
        RuleFor(x => x.ShopId).NotEmpty();

        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);

        RuleFor(x => x.Slug)
            .NotEmpty()
            .MinimumLength(3).MaximumLength(63)
            .Matches(SlugPattern)
                .WithMessage("Slug chỉ gồm chữ thường, số, dấu '-', không bắt đầu/kết thúc bằng '-'.")
            .Must(slug => !IsReserved(slug, reservedRoutes))
                .WithMessage("Slug này đã được hệ thống dùng riêng.");

        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();

        RuleFor(x => x.ExternalUrl)
            .NotEmpty()
            .When(x => x.Kind == ShopKind.ExternalOnly)
            .WithMessage("ExternalUrl bắt buộc khi Kind = ExternalOnly.");
    }

    private static bool IsReserved(string slug, IReservedRoutesProvider reservedRoutes) =>
        reservedRoutes.Routes.ReservedPaths.Contains(slug) || reservedRoutes.Routes.ReservedSubdomains.Contains(slug);
}
