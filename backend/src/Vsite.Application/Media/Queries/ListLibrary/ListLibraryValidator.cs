using FluentValidation;

namespace Vsite.Application.Media.Queries.ListLibrary;

public sealed class ListLibraryValidator : AbstractValidator<ListLibraryQuery>
{
    public ListLibraryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
