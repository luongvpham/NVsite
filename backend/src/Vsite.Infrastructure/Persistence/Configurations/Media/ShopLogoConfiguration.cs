using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vsite.Domain.Media.Entities;
using ShopEntity = Vsite.Domain.Shop.Entities.Shop;

namespace Vsite.Infrastructure.Persistence.Configurations.Media;

/// <summary>
/// FK ghép `Shop(LogoId, Id) → MediaAsset(Id, ShopId)` (Quyết định #76), khai từ phía Media —
/// `Shop` không được reference kiểu của `Media` (Shop.dependsOn không gồm Media,
/// `Docs/architecture/dependency-map.json`). `Shop.LogoId` chỉ là `Guid?` thuần trên entity, không
/// navigation. Namespace `Persistence.Configurations` dùng chung (SharedSegments) nên một class
/// `IEntityTypeConfiguration&lt;Shop&gt;` thứ hai ở đây không vi phạm ranh giới module — EF Core áp
/// dụng TẤT CẢ config tìm thấy cho cùng entity type, `ShopConfiguration.cs` (module Shop) vẫn giữ
/// cấu hình còn lại của `Shop`.
///
/// Postgres `MATCH SIMPLE` (mặc định): khi `LogoId IS NULL` thì FK không áp dụng.
/// </summary>
public sealed class ShopLogoConfiguration : IEntityTypeConfiguration<ShopEntity>
{
    public void Configure(EntityTypeBuilder<ShopEntity> builder)
    {
        builder.HasOne<MediaAsset>()
            .WithMany()
            .HasForeignKey(s => new { s.LogoId, s.Id })
            .HasPrincipalKey(m => new { m.Id, m.ShopId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
