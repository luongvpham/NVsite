using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Website.Dtos;
using Vsite.Application.Website.Versioning;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Exceptions;

namespace Vsite.Application.Website.Queries.GetTheme;

public sealed class GetThemeHandler(IAppDbContext db, ITenantContext tenant, DocumentVersioning versioning)
    : IRequestHandler<GetThemeQuery, ThemeDto>
{
    public async Task<ThemeDto> Handle(GetThemeQuery request, CancellationToken cancellationToken)
    {
        var shopId = tenant.RequireShopId();

        // Theme 1:1 với website, mỗi shop một website → tra theo ShopId (cộng Global Filter).
        var theme = await db.Themes
            .AsNoTracking()
            .Where(t => t.ShopId == shopId)
            .Select(t => new { t.PresetKey, t.Tokens, t.UpdatedAt, t.CreatedAt })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Website", shopId);

        return new ThemeDto(
            theme.PresetKey,
            StoredDocuments.ReadCurrent(DocumentKind.Tokens, theme.Tokens, versioning).ToJsonElement(),
            theme.UpdatedAt ?? theme.CreatedAt);
    }
}
