using FluentValidation;

namespace Vsite.Application.Media.Queries.GetAssetsByIds;

public sealed class GetAssetsByIdsValidator : AbstractValidator<GetAssetsByIdsQuery>
{
    public const int MaxIds = 200;

    public GetAssetsByIdsValidator()
    {
        RuleFor(x => x.Ids).Must(ids => ids.Count <= MaxIds)
            .WithMessage($"Tối đa {MaxIds} id mỗi lần gọi.");
    }
}
