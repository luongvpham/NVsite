using FluentValidation;
using MediatR;
using Vsite.Application.Website.Dtos;

namespace Vsite.Application.Website.Commands.SaveNavigation;

/// <summary>
/// Lưu menu (WEBSITE-007, Flow 2) — thay toàn bộ. <paramref name="Version"/> = <c>xmin</c> nhận từ <c>getNavigation</c>/lần lưu trước;
/// <c>null</c> = "chưa từng lưu" (INSERT). Không mang <c>ShopId</c> (#99).
/// </summary>
public sealed record SaveNavigationCommand(long? Version, IReadOnlyList<NavigationItemDto>? Items) : IRequest<SaveNavigationResultDto>;

public sealed class SaveNavigationValidator : AbstractValidator<SaveNavigationCommand>
{
    public SaveNavigationValidator()
    {
        RuleFor(x => x.Items).NotNull();
        RuleFor(x => x.Version).GreaterThanOrEqualTo(0).When(x => x.Version is not null);
    }
}
