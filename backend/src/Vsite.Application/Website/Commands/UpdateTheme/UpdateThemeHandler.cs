using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Website.Dtos;
using Vsite.Application.Website.Exceptions;
using Vsite.Application.Website.Validation;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Exceptions;

namespace Vsite.Application.Website.Commands.UpdateTheme;

/// <summary>Flow 4: validate tokens (<see cref="ThemeTokensValidator"/>: whitelist màu/font/… + upcaster) → 422 <c>THEME_INVALID</c> với errors[]; ghi cả tài liệu.</summary>
public sealed class UpdateThemeHandler(IAppDbContext db, ITenantContext tenant, ThemeTokensValidator validator)
    : IRequestHandler<UpdateThemeCommand, ThemeDto>
{
    public async Task<ThemeDto> Handle(UpdateThemeCommand request, CancellationToken cancellationToken)
    {
        var shopId = tenant.RequireShopId();

        var theme = await db.Themes.FirstOrDefaultAsync(t => t.ShopId == shopId, cancellationToken)
            ?? throw new NotFoundException("Website", shopId);

        var result = validator.Validate(request.TokensJson);
        if (!result.IsValid)
        {
            throw new DocumentInvalidException(result.ProblemCode!, result.Errors, truncated: false);
        }

        theme.Replace(result.CleanTokens!, request.PresetKey);
        await db.SaveChangesAsync(cancellationToken);

        return new ThemeDto(theme.PresetKey, result.CleanTokens!.ToJsonElement(), theme.UpdatedAt ?? theme.CreatedAt);
    }
}
