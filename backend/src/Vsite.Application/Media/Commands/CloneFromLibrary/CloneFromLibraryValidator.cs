using FluentValidation;

namespace Vsite.Application.Media.Commands.CloneFromLibrary;

public sealed class CloneFromLibraryValidator : AbstractValidator<CloneFromLibraryCommand>
{
    public CloneFromLibraryValidator()
    {
        RuleFor(x => x.Preset).NotEmpty();

        RuleFor(x => x.FocalX).InclusiveBetween(0f, 1f).When(x => x.FocalX.HasValue);
        RuleFor(x => x.FocalY).InclusiveBetween(0f, 1f).When(x => x.FocalY.HasValue);
    }
}
