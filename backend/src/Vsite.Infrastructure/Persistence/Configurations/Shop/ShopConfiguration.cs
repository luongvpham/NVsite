using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vsite.Domain.Identity.Entities;
using ShopEntity = Vsite.Domain.Shop.Entities.Shop;

namespace Vsite.Infrastructure.Persistence.Configurations.Shop;

/// <summary>04 §2.1 — entity đầy đủ (chuyển giao từ Identity ở SHOP-001).</summary>
public sealed class ShopConfiguration : IEntityTypeConfiguration<ShopEntity>
{
    public void Configure(EntityTypeBuilder<ShopEntity> builder)
    {
        builder.ToTable("Shop", t =>
        {
            // 04 §2.1 — "NOT NULL khi Kind = ExternalOnly".
            t.HasCheckConstraint(
                "ck_shop_external_url",
                "\"Kind\" <> 'ExternalOnly' OR \"ExternalUrl\" IS NOT NULL");
        });

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Kind).HasConversion<string>().IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().IsRequired();

        builder.HasIndex(s => s.Slug).IsUnique();

        // FK UserShop.ShopId → Shop khai từ PHÍA Shop — UserShop thuộc Identity
        // (`Identity.dependsOn = []`), giữ navigation ở đó sẽ tạo phụ thuộc ngược, bị
        // ModuleBoundaryTests chặn. Chiều này hợp lệ vì `Shop.dependsOn = ["Identity"]`
        // (xem backend/docs/modules/identity.md mục "Nợ kỹ thuật đã biết").
        builder.HasMany<UserShop>()
            .WithOne()
            .HasForeignKey(us => us.ShopId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
