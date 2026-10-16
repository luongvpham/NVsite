using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Exceptions;

namespace Vsite.Application.Website.Commands.DeletePage;

/// <summary>
/// Soft delete bằng <c>Page.SoftDelete()</c> (không <c>Remove()</c>). Draft của trang KHÔNG bị xoá theo: nó trở thành mồ côi vô hình
/// (mọi đường đọc đều join/lọc theo Page còn sống — GetPageDraft, và T11 cho <c>references</c>); slug được dùng lại (unique lọc <c>NOT is_deleted</c>, G10).
/// </summary>
public sealed class DeletePageHandler(IAppDbContext db, ITenantContext tenant) : IRequestHandler<DeletePageCommand>
{
    public async Task Handle(DeletePageCommand request, CancellationToken cancellationToken)
    {
        var shopId = tenant.RequireShopId();

        var page = await db.Pages.FirstOrDefaultAsync(p => p.Id == request.PageId && p.ShopId == shopId, cancellationToken)
            ?? throw new NotFoundException("Page", request.PageId);

        if (!page.CanBeDeleted)
        {
            throw new ConflictException("PAGE_NOT_DELETABLE", "Chỉ xoá được trang tự tạo, không phải trang chủ hay trang hệ thống.");
        }

        page.SoftDelete();
        await db.SaveChangesAsync(cancellationToken);
    }
}
