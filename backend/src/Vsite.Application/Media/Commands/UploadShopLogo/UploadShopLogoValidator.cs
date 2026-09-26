using FluentValidation;

namespace Vsite.Application.Media.Commands.UploadShopLogo;

public sealed class UploadShopLogoValidator : AbstractValidator<UploadShopLogoCommand>
{
    public UploadShopLogoValidator()
    {
        RuleFor(x => x.ShopId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
    }
}
