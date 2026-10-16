using FluentValidation;
using MediatR;
using Vsite.Application.Website.Dtos;
using Vsite.Domain.Website.Entities;

namespace Vsite.Application.Website.Commands.UpdateTheme;

/// <summary>Ghi cả tài liệu tokens (không patch, <c>schemaVersion</c> ở gốc). Theme không có concurrency token — ghi cuối thắng (G7).</summary>
public sealed record UpdateThemeCommand(string? PresetKey, string TokensJson) : IRequest<ThemeDto>;

public sealed class UpdateThemeValidator : AbstractValidator<UpdateThemeCommand>
{
    public UpdateThemeValidator()
    {
        RuleFor(x => x.PresetKey).MaximumLength(Theme.MaxPresetKeyLength);
        RuleFor(x => x.TokensJson).NotNull();
    }
}
