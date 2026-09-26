using FluentValidation;

namespace Vsite.Application.Media.Commands.UploadToLibrary;

public sealed class UploadToLibraryValidator : AbstractValidator<UploadToLibraryCommand>
{
    public UploadToLibraryValidator()
    {
        RuleFor(x => x.ShopId).NotEmpty();
        RuleFor(x => x.AltText).MaximumLength(200);
        RuleFor(x => x.Folder).MaximumLength(100);
    }
}
