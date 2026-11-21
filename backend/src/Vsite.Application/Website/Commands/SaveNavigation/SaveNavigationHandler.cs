using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Common.Persistence;
using Vsite.Application.Website.Dtos;
using Vsite.Application.Website.Exceptions;
using Vsite.Application.Website.Navigation;
using Vsite.Application.Website.Validation;
using Vsite.Application.Website.Versioning;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Website.Entities;

namespace Vsite.Application.Website.Commands.SaveNavigation;

/// <summary>
/// Flow 2 (WEBSITE-007, #113), khuôn <c>SavePageDraftHandler</c>. Thứ tự: website của shop (chưa có → 404) → dòng menu theo
/// <c>website_id</c> + <c>ShopId</c> → so <c>Version</c> (<c>null</c> mà đã có dòng, lệch <c>xmin</c>, hoặc có số mà chưa có dòng →
/// 409 <c>NAVIGATION_CONFLICT</c>) → <see cref="NavigationItems.Validate"/> với trang của ĐÚNG website (422 <c>NAVIGATION_INVALID</c>,
/// lỗi ở <c>params.errors[]</c>) → lưu <c>Serialize(Normalize(items))</c>, <c>IsDirty = true</c>.
/// Race: <c>23505</c> (hai INSERT cùng <c>website_id</c>) hoặc <see cref="DbUpdateConcurrencyException"/> (<c>xmin</c> đổi trước UPDATE)
/// → cũng <c>NAVIGATION_CONFLICT</c>, không 500 (R2).
///
/// ⚠️ <see cref="NavigationConfig"/> kế thừa soft delete nhưng PK là <c>website_id</c>: một dòng xoá mềm bị filter giấu mà vẫn giữ PK
/// ⇒ INSERT sau đó vướng 23505. KHÔNG được xoá mềm <c>NavigationConfig</c> (hiện không code nào làm); nếu lỡ có, nhánh 23505 bên dưới
/// vẫn trả 409 chứ không 500. Không dùng <c>IgnoreQueryFilters()</c> ở đây (allowlist #90).
/// </summary>
public sealed class SaveNavigationHandler(IAppDbContext db, ITenantContext tenant, DocumentVersioning versioning)
    : IRequestHandler<SaveNavigationCommand, SaveNavigationResultDto>
{
    /// <summary>Trần số lỗi trả về (như <c>PUBLISH_BLOCKED</c>) — body cực lớn không làm phình response.</summary>
    public const int MaxErrors = 100;

    public async Task<SaveNavigationResultDto> Handle(SaveNavigationCommand request, CancellationToken cancellationToken)
    {
        var shopId = tenant.RequireShopId();
        var websiteId = await NavigationQueries.RequireWebsiteIdAsync(db, shopId, cancellationToken);

        var nav = await db.NavigationConfigs
            .FirstOrDefaultAsync(n => n.Id == websiteId && n.ShopId == shopId, cancellationToken);

        if (nav is null && request.Version is not null)
        {
            throw Conflict(null, null);
        }

        if (nav is not null && request.Version != nav.Version)
        {
            throw Conflict(nav.Version, nav.UpdatedAt ?? nav.CreatedAt);
        }

        var items = NavigationQueries.FromDtos(request.Items ?? []);
        var pages = await NavigationQueries.LoadPagesAsync(db, shopId, websiteId, cancellationToken);
        var errors = NavigationItems.Validate(items, pages);
        if (errors.Count > 0)
        {
            throw new DocumentInvalidException(
                NavigationErrorCodes.Invalid,
                errors.Take(MaxErrors).Select(e => new TreeValidationError(e.Path, e.Code, e.Message)).ToList(),
                truncated: errors.Count > MaxErrors);
        }

        var json = NavigationItems.Serialize(NavigationItems.Normalize(items), versioning);
        if (nav is null)
        {
            nav = NavigationConfig.Create(websiteId, shopId, json);
            db.NavigationConfigs.Add(nav);
        }
        else
        {
            nav.Replace(json);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        // Không lọc 23505 theo tên constraint (`pk_navigation_config`): Application không tham chiếu Npgsql nên không đọc được
        // PostgresException.ConstraintName (UniqueViolation chỉ thấy DbException.SqlState — cùng khuôn CreatePage/PublishWebsite).
        // Không cần: SaveChanges này chỉ ghi đúng MỘT dòng navigation_config, và unique duy nhất của bảng là PK website_id
        // (ix_navigation_config_website_id_shop_id không unique) ⇒ 23505 ở đây chỉ có thể là hai INSERT cùng website.
        catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException || UniqueViolation.IsUniqueViolation(ex))
        {
            // Tab khác đã INSERT/UPDATE trước: đọc lại trạng thái thật (AsNoTracking — bỏ qua entity đang track) để FE hoà giải.
            var current = await db.NavigationConfigs
                .AsNoTracking()
                .Where(n => n.Id == websiteId && n.ShopId == shopId)
                .Select(n => new { n.Version, n.UpdatedAt, n.CreatedAt })
                .FirstOrDefaultAsync(CancellationToken.None);
            throw current is null ? Conflict(null, null) : Conflict(current.Version, current.UpdatedAt ?? current.CreatedAt);
        }

        // xmin mới được EF đọc lại qua RETURNING (cột store-generated) ngay sau INSERT/UPDATE.
        return new SaveNavigationResultDto(nav.Version, nav.IsDirty);
    }

    private static NavigationConflictException Conflict(long? version, DateTimeOffset? updatedAt) =>
        new(new NavigationConflictParams(version, updatedAt));
}
