using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Common.Persistence;
using Vsite.Application.Media.Interfaces;
using Vsite.Application.Website.Dtos;
using Vsite.Application.Website.Validation;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Website.Enums;

namespace Vsite.Application.Website.Commands.UpdatePage;

public sealed class UpdatePageHandler(
    IAppDbContext db,
    ITenantContext tenant,
    SlugRules slugRules,
    IMediaReferenceValidator media)
    : IRequestHandler<UpdatePageCommand, PageDto>
{
    public async Task<PageDto> Handle(UpdatePageCommand request, CancellationToken cancellationToken)
    {
        var shopId = tenant.RequireShopId();

        // Ownership trong chính câu query (+ Global Filter): trang của shop khác = không tồn tại.
        var page = await db.Pages.FirstOrDefaultAsync(p => p.Id == request.PageId && p.ShopId == shopId, cancellationToken)
            ?? throw new NotFoundException("Page", request.PageId);

        if (request.Slug is not null && request.Slug != page.Slug)
        {
            if (page.Kind == PageKind.System)
            {
                throw new UnprocessableException("PAGE_SYSTEM_SLUG_FIXED", "Không đổi được slug của trang hệ thống.");
            }

            if (page.IsHomePage)
            {
                throw new UnprocessableException("PAGE_HOME_SLUG_FIXED", "Không đổi được slug của trang chủ.");
            }

            slugRules.EnsureAllowed(request.Slug);

            var taken = await db.Pages.AnyAsync(
                p => p.ShopId == shopId && p.WebsiteId == page.WebsiteId && p.Slug == request.Slug && p.Id != page.Id,
                cancellationToken);
            if (taken)
            {
                throw SlugTaken();
            }

            page.ChangeSlug(request.Slug);
        }

        if (!request.NoIndex && page.SystemType == SystemType.NotFound)
        {
            throw new UnprocessableException("PAGE_NOINDEX_REQUIRED", "Trang NotFound luôn NoIndex.");
        }

        // Chỉ kiểm khi ảnh THAY ĐỔI: ảnh đang gắn mà sau đó bị xoá mềm không được làm hỏng việc sửa tiêu đề.
        if (request.OgImageId is { } ogImageId && ogImageId != page.OgImageId)
        {
            await media.EnsureValidAsync([], [ogImageId], cancellationToken);
        }

        page.Rename(request.Title);
        page.SetSeo(request.SeoTitle, request.SeoDescription);
        page.SetOgImage(request.OgImageId);
        page.SetNoIndex(request.NoIndex);
        page.SetEnabled(request.IsEnabled);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (UniqueViolation.IsUniqueViolation(ex))
        {
            throw SlugTaken();
        }

        var isDirty = await db.PageDrafts
            .Where(d => d.Id == page.Id && d.ShopId == shopId)
            .Select(d => d.IsDirty)
            .FirstOrDefaultAsync(cancellationToken);

        return Mappings.ToDto(page, isDirty);
    }

    private static ConflictException SlugTaken() =>
        new("PAGE_SLUG_TAKEN", "Slug này đã được dùng bởi một trang khác trong website.");
}
