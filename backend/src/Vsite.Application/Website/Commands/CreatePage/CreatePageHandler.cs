using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Common.Persistence;
using Vsite.Application.Media.Interfaces;
using Vsite.Application.Website.Dtos;
using Vsite.Application.Website.Seeding;
using Vsite.Application.Website.Validation;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Website.Entities;

namespace Vsite.Application.Website.Commands.CreatePage;

/// <summary>
/// Flow 3: thứ tự — website của tenant → <see cref="SlugRules"/> (422) → trùng slug trong website (409, kiểm trước) → ảnh OG qua
/// <see cref="IMediaReferenceValidator"/> (bản Library, #76/#77) → ghi Page + PageDraft rỗng (để mở editor được ngay). Race hai request
/// cùng slug: UNIQUE <c>ux_page_slug</c> chặn, 23505 được đổi thành cùng mã 409. <c>SortOrder</c> = cuối danh sách (G12).
/// </summary>
public sealed class CreatePageHandler(
    IAppDbContext db,
    ITenantContext tenant,
    SlugRules slugRules,
    IMediaReferenceValidator media,
    WebsiteSeeder seeder)
    : IRequestHandler<CreatePageCommand, PageDto>
{
    public async Task<PageDto> Handle(CreatePageCommand request, CancellationToken cancellationToken)
    {
        var shopId = tenant.RequireShopId();

        var websiteId = await db.Websites
            .Where(w => w.ShopId == shopId)
            .Select(w => (Guid?)w.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Website", shopId);

        slugRules.EnsureAllowed(request.Slug);

        if (await db.Pages.AnyAsync(p => p.ShopId == shopId && p.WebsiteId == websiteId && p.Slug == request.Slug, cancellationToken))
        {
            throw SlugTaken();
        }

        if (request.OgImageId is { } ogImageId)
        {
            await media.EnsureValidAsync([], [ogImageId], cancellationToken);
        }

        var lastSortOrder = await db.Pages
            .Where(p => p.ShopId == shopId && p.WebsiteId == websiteId)
            .MaxAsync(p => (int?)p.SortOrder, cancellationToken);

        var page = Page.NewComposable(shopId, websiteId, request.Slug, request.Title, (lastSortOrder ?? -1) + 1);
        page.SetSeo(request.SeoTitle, request.SeoDescription);
        page.SetOgImage(request.OgImageId);
        page.SetNoIndex(request.NoIndex);
        page.SetEnabled(request.IsEnabled);

        var draft = PageDraft.Create(shopId, page.Id, seeder.EmptyTree());
        db.Pages.Add(page);
        db.PageDrafts.Add(draft);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (UniqueViolation.IsUniqueViolation(ex))
        {
            throw SlugTaken();
        }

        return Mappings.ToDto(page, draft.IsDirty);
    }

    private static ConflictException SlugTaken() =>
        new("PAGE_SLUG_TAKEN", "Slug này đã được dùng bởi một trang khác trong website.");
}
