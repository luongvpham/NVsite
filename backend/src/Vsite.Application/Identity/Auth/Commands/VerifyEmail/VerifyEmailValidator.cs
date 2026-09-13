using FluentValidation;

namespace Vsite.Application.Identity.Auth.Commands.VerifyEmail;

public sealed class VerifyEmailValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
    }
}
