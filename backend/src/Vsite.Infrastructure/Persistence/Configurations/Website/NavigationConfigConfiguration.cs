using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vsite.Domain.Website.Entities;
using WebsiteEntity = Vsite.Domain.Website.Entities.Website;

namespace Vsite.Infrastructure.Persistence.Configurations.Website;

/// <summary>
/// WEBSITE-007 — bảng <c>navigation_config</c>, PK = <c>website_id</c> (1:1). FK ghép
/// <c>(website_id, shop_id) → website (id, shop_id)</c> (#92). <c>Version</c> là cột hệ thống <c>xmin</c>.
/// </summary>
public sealed class NavigationConfigConfiguration : IEntityTypeConfiguration<NavigationConfig>
{
    public void Configure(EntityTypeBuilder<NavigationConfig> builder)
    {
        builder.ToTable("navigation_config");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasColumnName("website_id").ValueGeneratedNever();
        builder.Ignore(n => n.WebsiteId);

        builder.Property(n => n.Items).HasColumnType("jsonb").IsRequired();
        builder.Property(n => n.Version).IsRowVersion();

        builder.HasOne<WebsiteEntity>()
            .WithMany()
            .HasForeignKey(n => new { n.Id, n.ShopId })
            .HasPrincipalKey(w => new { w.Id, w.ShopId })
            .HasConstraintName("fk_navigation_config_website")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
