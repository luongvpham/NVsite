using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vsite.Domain.Website.Entities;
using WebsiteEntity = Vsite.Domain.Website.Entities.Website;

namespace Vsite.Infrastructure.Persistence.Configurations.Website;

/// <summary>`05` §3 — bảng <c>theme</c>, PK = <c>website_id</c> (1:1). FK ghép <c>(website_id, shop_id) → website (id, shop_id)</c> (#92).</summary>
public sealed class ThemeConfiguration : IEntityTypeConfiguration<Theme>
{
    public void Configure(EntityTypeBuilder<Theme> builder)
    {
        builder.ToTable("theme");

        // Id của base chính là WebsiteId (bí danh trong entity) — đặt tên cột theo spec.
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("website_id").ValueGeneratedNever();
        builder.Ignore(t => t.WebsiteId);

        builder.Property(t => t.PresetKey).HasMaxLength(50);
        builder.Property(t => t.Tokens).HasColumnType("jsonb").IsRequired();

        builder.HasOne<WebsiteEntity>()
            .WithMany()
            .HasForeignKey(t => new { t.Id, t.ShopId })
            .HasPrincipalKey(w => new { w.Id, w.ShopId })
            .HasConstraintName("fk_theme_website")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
