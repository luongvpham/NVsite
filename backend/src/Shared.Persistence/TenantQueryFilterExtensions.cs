using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Shared.Domain;
using Shared.Domain.Abstractions;

namespace Shared.Persistence;

/// <summary>
/// Global Query Filter TỰ ĐỘNG cho mọi module — gọi MỘT LẦN trong `OnModelCreating` (xem
/// <see cref="AppDbContextBase"/>), không viết tay `HasQueryFilter` cho từng entity nữa. Entity
/// nào kế thừa <see cref="IShopScoped"/> (`ShopEntity`/`ShopAuditableEntity`) được lọc theo
/// `ShopId == TenantContext.ShopId`; entity nào kế thừa `BaseAuditableEntity` bị ẩn khi
/// `IsDeleted = true`. Hai điều kiện AND lại nếu entity thoả cả hai (vd. `ShopAuditableEntity`).
///
/// Fail-closed: `TenantContext.ShopId == null` không khớp `ShopId` (kiểu `Guid` non-null) của bất
/// kỳ hàng nào → query rỗng, không phải "bỏ qua filter". Đây là quyết định có chủ đích (Quyết định
/// #21.2) — xem `docs/tasks/BOOTSTRAP-002/changelog.md`-style ghi chú ở CLAUDE.md module Identity.
///
/// ⚠️ Filter tham chiếu `dbContext.TenantContext` (property trên CHÍNH instance DbContext, không
/// phải một biến rời) — đây là pattern chính thức của EF Core cho multi-tenancy (xem tài liệu EF
/// Core "Global query filters" mục dùng service injected qua DI). Model được cache theo TYPE của
/// DbContext, nhưng EF Core re-evaluate biểu thức tham chiếu instance này ở MỖI lần query thực thi
/// (không đóng băng giá trị lúc `OnModelCreating` chạy) — bắt buộc phải giữ nguyên dạng tham chiếu
/// `dbContext` (không đổi thành `Expression.Constant(tenantContext.ShopId)` — giá trị đó SẼ bị đóng
/// băng vĩnh viễn theo instance đầu tiên build model, gây rò dữ liệu xuyên tenant).
/// </summary>
public static class TenantQueryFilterExtensions
{
    public static void ApplyGlobalFilters<TContext>(this ModelBuilder modelBuilder, TContext dbContext)
        where TContext : DbContext, ITenantScopedDbContext
    {
        var dbContextConstant = Expression.Constant(dbContext);
        var tenantContextAccess = Expression.Property(dbContextConstant, nameof(ITenantScopedDbContext.TenantContext));
        var tenantShopIdAccess = Expression.Property(tenantContextAccess, nameof(ITenantContext.ShopId));

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            var isShopScoped = typeof(IShopScoped).IsAssignableFrom(clrType);
            var isAuditable = typeof(BaseAuditableEntity).IsAssignableFrom(clrType);

            if (!isShopScoped && !isAuditable)
            {
                continue;
            }

            var parameter = Expression.Parameter(clrType, "e");
            Expression? predicate = null;

            if (isShopScoped)
            {
                var shopId = Expression.Convert(Expression.Property(parameter, nameof(IShopScoped.ShopId)), typeof(Guid?));
                predicate = Expression.Equal(shopId, tenantShopIdAccess);
            }

            if (isAuditable)
            {
                var notDeleted = Expression.Not(Expression.Property(parameter, nameof(BaseAuditableEntity.IsDeleted)));
                predicate = predicate is null ? notDeleted : Expression.AndAlso(predicate, notDeleted);
            }

            var lambda = Expression.Lambda(predicate!, parameter);
            entityType.SetQueryFilter(lambda);
        }
    }
}
