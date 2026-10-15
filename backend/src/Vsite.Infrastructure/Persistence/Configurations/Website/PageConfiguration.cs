using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vsite.Domain.Media.Entities;
using Vsite.Domain.Website.Entities;
using WebsiteEntity = Vsite.Domain.Website.Entities.Website;

namespace Vsite.Infrastructure.Persistence.Configurations.Website;

/// <summary>
/// `05` §4 — bảng <c>page</c>. <c>UNIQUE (id, shop_id)</c> là đích FK ghép của <c>page_draft</c> và (sau này)
/// <c>Listing.TargetPageId</c>/<c>Service.DetailPageId</c>. Hai unique có lọc <c>NOT is_deleted</c> (G10): slug dùng
/// lại được sau khi xoá mềm. <c>slug</c> NULLABLE: NULL ⟺ <c>system_type = 'NotFound'</c> (plan §4, lệch `05` §4).
/// </summary>
public sealed class PageConfiguration : IEntityTypeConfiguration<Page>
{
    public void Configure(EntityTypeBuilder<Page> builder)
    {
        builder.ToTable("page", t =>
        {
            // Kind = System ⟺ SystemType ≠ null (#33).
            t.HasCheckConstraint("ck_page_kind_system_type", "(kind = 'System') = (system_type IS NOT NULL)");

            // Slug NULL ⟺ NotFound. `IS NOT DISTINCT FROM` (không phải `=`): với system_type NULL thì
            // `system_type = 'NotFound'` là NULL và CHECK sẽ lọt — Composable với slug NULL phải bị chặn.
            t.HasCheckConstraint("ck_page_slug_not_found", "(slug IS NULL) = (system_type IS NOT DISTINCT FROM 'NotFound')");

            // Slug rỗng (trang chủ) chỉ cho Composable.
            t.HasCheckConstraint("ck_page_empty_slug_composable", "slug IS DISTINCT FROM '' OR kind = 'Composable'");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.SystemType).HasConversion<string>().HasMaxLength(30);
        builder.Property(p => p.Slug).HasMaxLength(120);
        builder.Property(p => p.Title).HasMaxLength(160).IsRequired();
        builder.Property(p => p.SeoTitle).HasMaxLength(160);
        builder.Property(p => p.SeoDescription).HasMaxLength(320);

        // Đích FK ghép (#92).
        builder.HasAlternateKey(p => new { p.Id, p.ShopId });

        // Slug duy nhất trong website, CHỈ giữa các trang còn sống (G10, R13). NULL (NotFound) không đụng nhau.
        builder.HasIndex(p => new { p.WebsiteId, p.Slug })
            .IsUnique()
            .HasDatabaseName("ux_page_slug")
            .HasFilter("NOT is_deleted");

        // Mỗi loại trang hệ thống chỉ một (`05` §4).
        builder.HasIndex(p => new { p.WebsiteId, p.SystemType })
            .IsUnique()
            .HasDatabaseName("ux_page_system_type")
            .HasFilter("system_type IS NOT NULL AND NOT is_deleted");

        builder.HasOne<WebsiteEntity>()
            .WithMany()
            .HasForeignKey(p => new { p.WebsiteId, p.ShopId })
            .HasPrincipalKey(w => new { w.Id, w.ShopId })
            .HasConstraintName("fk_page_website")
            .OnDelete(DeleteBehavior.Restrict);

        // FK ghép (#76) — SQL thật là SET NULL (og_image_id); migration sửa tay (xem WebsiteConfiguration).
        builder.HasOne<MediaAsset>()
            .WithMany()
            .HasForeignKey(p => new { p.OgImageId, p.ShopId })
            .HasPrincipalKey(m => new { m.Id, m.ShopId })
            .HasConstraintName("fk_page_media_asset_og_image")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
