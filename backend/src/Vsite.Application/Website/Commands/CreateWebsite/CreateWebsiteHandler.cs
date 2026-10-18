using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Common.Persistence;
using Vsite.Application.Website.Dtos;
using Vsite.Application.Website.Interfaces;
using Vsite.Application.Website.Seeding;
using Vsite.Application.Website.Validation;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Shop.Enums;
using Vsite.Domain.Website.Entities;
using WebsiteEntity = Vsite.Domain.Website.Entities.Website;

namespace Vsite.Application.Website.Commands.CreateWebsite;

/// <summary>
/// Flow 1 (plan §3): shop phải tồn tại, thuộc tenant hiện tại và <c>Hosted</c>; chưa có website; rồi tạo Website + Theme + trang chủ +
/// Contact + NotFound + 3 PageDraft trong MỘT <c>SaveChanges</c> (một transaction). Kiểm trước để trả 409 đẹp; race hai request song song
/// thì UNIQUE <c>(shop_id)</c> chặn và 23505 được đổi thành cùng mã 409 ở đây (đường duy nhất có thể 23505 là website của shop này:
/// mọi id/slug khác đều mới sinh).
/// </summary>
public sealed class CreateWebsiteHandler(
    IAppDbContext db,
    ITenantContext tenant,
    WebsiteSeeder seeder,
    ThemeTokensValidator themeTokens,
    IThemeTokenCatalog themeCatalog)
    : IRequestHandler<CreateWebsiteCommand, WebsiteDto>
{
    public async Task<WebsiteDto> Handle(CreateWebsiteCommand request, CancellationToken cancellationToken)
    {
        var tenantShopId = tenant.RequireShopId();
        var shopId = request.ShopId;
        if (tenantShopId != shopId)
        {
            throw new NotFoundException("Shop", shopId); // shop của tenant khác = không tồn tại (không lộ)
        }

        var shop = await db.Shops.FirstOrDefaultAsync(s => s.Id == shopId, cancellationToken)
            ?? throw new NotFoundException("Shop", shopId);
        if (shop.Kind != ShopKind.Hosted)
        {
            throw new UnprocessableException(
                "WEBSITE_REQUIRES_HOSTED_SHOP", "Chỉ shop dùng website do vsite lưu trữ (Hosted) mới tạo được website.");
        }

        if (await db.Websites.AnyAsync(w => w.ShopId == shopId, cancellationToken))
        {
            throw AlreadyExists();
        }

        var tokens = themeTokens.Validate(themeCatalog.DefaultsJson);
        if (!tokens.IsValid)
        {
            throw new InvalidOperationException(
                "Tokens mặc định trong theme-tokens.json không qua validator: "
                + string.Join("; ", tokens.Errors.Select(e => $"{e.Path} {e.Code}")));
        }

        var website = WebsiteEntity.Create(shopId);
        var (home, homeDraft) = seeder.CreateHome(shopId, website.Id, sortOrder: 0);
        var system = seeder.EnsureSystemPages(shopId, website.Id, existing: [], startSortOrder: 1);

        db.Websites.Add(website);
        db.Themes.Add(Theme.Create(website.Id, shopId, tokens.CleanTokens!));
        db.Pages.Add(home);
        db.Pages.AddRange(system.Pages);
        db.PageDrafts.Add(homeDraft);
        db.PageDrafts.AddRange(system.Drafts);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (UniqueViolation.IsUniqueViolation(ex))
        {
            throw AlreadyExists();
        }

        return Mappings.ToDto(website, home.Id);
    }

    private static ConflictException AlreadyExists() =>
        new("WEBSITE_ALREADY_EXISTS", "Shop này đã có website.");
}
