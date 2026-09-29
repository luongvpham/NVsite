using FluentValidation;

namespace Vsite.Application.Media.Queries.GetDerivatives;

public sealed class GetDerivativesValidator : AbstractValidator<GetDerivativesQuery>
{
    /// <summary>Khớp kích thước cột `MediaAsset.Preset`. Không từ chối tên preset lạ — trả [] là đủ.</summary>
    public const int MaxPresetLength = 40;

    public GetDerivativesValidator()
    {
        RuleFor(x => x.Preset).MaximumLength(MaxPresetLength);
    }
}
