using FluentValidation;
using Vsite.Domain.Website.Entities;

namespace Vsite.Application.Website.Commands.PublishWebsite;

public sealed class PublishWebsiteValidator : AbstractValidator<PublishWebsiteCommand>
{
    public PublishWebsiteValidator()
    {
        RuleFor(x => x.Note).MaximumLength(SitePublication.MaxNoteLength);
    }
}
