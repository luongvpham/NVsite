using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Vsite.Domain.Identity.Entities;
using Vsite.Domain.Media.Entities;
using Vsite.Domain.Website.Entities;
using ShopEntity = Vsite.Domain.Shop.Entities.Shop;
using WebsiteEntity = Vsite.Domain.Website.Entities.Website;

namespace Vsite.Application.Common.Interfaces;

/// <summary>
/// Abstraction của `AppDbContext` (Infrastructure) — Application không được reference
/// Infrastructure trực tiếp (DesignIdeal/architecture-guide.md §1). `DbSet&lt;T&gt;` là pattern
/// chuẩn cho interface này (không phải vi phạm "no infra concerns" — DbSet là data-shape, không
/// phải implementation).
///
/// Module mới thêm `DbSet` của mình vào MỘT section riêng bên dưới — không tạo interface
/// `I{Module}DbContext` thứ hai (xem ghi chú lý do ở <c>AppDbContext</c>).
/// </summary>
public interface IAppDbContext
{
    // ---- Identity ----
    DbSet<User> Users { get; }
    DbSet<ExternalLogin> ExternalLogins { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserShop> UserShops { get; }
    DbSet<PendingRegistration> PendingRegistrations { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }

    // ---- Shop ----
    DbSet<ShopEntity> Shops { get; }

    // ---- Media ----
    DbSet<MediaAsset> MediaAssets { get; }

    // ---- Website ----
    DbSet<WebsiteEntity> Websites { get; }
    DbSet<Theme> Themes { get; }
    DbSet<Page> Pages { get; }
    DbSet<PageDraft> PageDrafts { get; }
    DbSet<NavigationConfig> NavigationConfigs { get; }
    DbSet<SitePublication> SitePublications { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>WEBSITE-008 (#117) — transaction tường minh khi một thao tác DB phải đi cùng một tác dụng ngoài DB
    /// (vd. job dọn ảnh: xoá record → xoá file → commit). <c>DbContext</c> cài sẵn; decorator test forward.</summary>
    DatabaseFacade Database { get; }
}
