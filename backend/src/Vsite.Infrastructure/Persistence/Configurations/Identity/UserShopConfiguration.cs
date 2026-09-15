using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vsite.Domain.Identity.Entities;
using Vsite.Domain.Identity.Enums;

namespace Vsite.Infrastructure.Persistence.Configurations.Identity;

/// <summary>03 §3.3, §4 ràng buộc [4] và [5]. Entity tenant-scoped DUY NHẤT ở Bước 3.</summary>
public sealed class UserShopConfiguration : IEntityTypeConfiguration<UserShop>
{
    public void Configure(EntityTypeBuilder<UserShop> builder)
    {
        builder.ToTable("UserShop");
        builder.HasKey(us => us.Id);

        builder.Property(us => us.Source).HasConversion<string>().IsRequired();
        builder.Property(us => us.Status).HasConversion<string>().IsRequired();

        builder.Property(us => us.RoleScope)
            .HasConversion<string>()
            .HasComputedColumnSql("'Shop'", stored: true);

        // [5] Một user chỉ có một membership per shop.
        builder.HasIndex(us => new { us.UserId, us.ShopId }).IsUnique();

        // [4] Composite FK — không cho gán role Platform vào UserShop.
        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(us => new { us.RoleId, us.RoleScope })
            .HasPrincipalKey(r => new { r.Id, r.Scope })
            .OnDelete(DeleteBehavior.Restrict);

        // FK UserShop.ShopId → Shop khai ở ShopConfiguration (phía Shop, module Shop.dependsOn =
        // ["Identity"]) — UserShop (Identity) không giữ navigation Shop, xem SHOP-001.
    }
}
