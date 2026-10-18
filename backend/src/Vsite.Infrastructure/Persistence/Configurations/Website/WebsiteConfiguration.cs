using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vsite.Domain.Media.Entities;
using ShopEntity = Vsite.Domain.Shop.Entities.Shop;
using WebsiteEntity = Vsite.Domain.Website.Entities.Website;

namespace Vsite.Infrastructure.Persistence.Configurations.Website;

/// <summary>
/// `05` §2 — bảng <c>website</c>. UNIQUE <c>(shop_id)</c> (1:1) + UNIQUE <c>(id, shop_id)</c> làm đích FK ghép
/// của <c>theme</c>/<c>page</c> (#92). FK ghép ảnh → <c>media_asset (id, shop_id)</c> chặn trỏ ảnh của shop khác (#76).
///
/// ⚠️ <c>ON DELETE SET NULL (cột)</c> (Postgres 15+) cho hai FK ảnh KHÔNG diễn đạt được bằng EF Core/Npgsql
/// (<c>DeleteBehavior.SetNull</c> sẽ null cả <c>shop_id</c> NOT NULL) — model khai <c>Restrict</c> và migration
/// <c>AddWebsiteModule</c> được SỬA TAY để thêm hai FK này bằng SQL thô. Đừng "sửa lại" bằng cách sinh lại migration
/// mà không giữ phần SQL đó.
/// </summary>
public sealed class WebsiteConfiguration : IEntityTypeConfiguration<WebsiteEntity>
{
    public void Configure(EntityTypeBuilder<WebsiteEntity> builder)
    {
        builder.ToTable("website", t =>
        {
            // `05` §2 — DefaultLocale phải thuộc Locales (#98).
            t.HasCheckConstraint("ck_website_default_locale", "default_locale = ANY(locales)");
        });

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(w => w.DefaultLocale).HasMaxLength(10).IsRequired();

        // `Locales` là IReadOnlyList (không map được) — map thẳng field backing thành cột text[].
        builder.Ignore(w => w.Locales);
        builder.Property<List<string>>("_locales").HasColumnName("locales").HasColumnType("text[]").IsRequired();

        builder.Property(w => w.DefaultSeoTitle).HasMaxLength(160);
        builder.Property(w => w.DefaultSeoDescription).HasMaxLength(320);
        builder.Property(w => w.TrackingSnippets).HasColumnType("jsonb").IsRequired();

        // 1:1 với shop (không lọc xoá mềm — có chủ đích, plan R11) + đích FK ghép của bảng con (#92).
        builder.HasIndex(w => w.ShopId).IsUnique().HasDatabaseName("ux_website_shop");
        builder.HasAlternateKey(w => new { w.Id, w.ShopId }).HasName("ak_website_id_shop_id");

        builder.HasOne<ShopEntity>()
            .WithMany()
            .HasForeignKey(w => w.ShopId)
            .HasConstraintName("fk_website_shop")
            .OnDelete(DeleteBehavior.Restrict);

        // FK ghép (#76) — SQL thật là SET NULL (cột), xem doc class; migration sửa tay.
        builder.HasOne<MediaAsset>()
            .WithMany()
            .HasForeignKey(w => new { w.DefaultOgImageId, w.ShopId })
            .HasPrincipalKey(m => new { m.Id, m.ShopId })
            .HasConstraintName("fk_website_media_asset_default_og_image")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<MediaAsset>()
            .WithMany()
            .HasForeignKey(w => new { w.FaviconAssetId, w.ShopId })
            .HasPrincipalKey(m => new { m.Id, m.ShopId })
            .HasConstraintName("fk_website_media_asset_favicon")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
