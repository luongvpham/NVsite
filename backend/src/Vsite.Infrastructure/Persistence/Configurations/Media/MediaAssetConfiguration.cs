using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vsite.Domain.Media.Entities;
using ShopEntity = Vsite.Domain.Shop.Entities.Shop;

namespace Vsite.Infrastructure.Persistence.Configurations.Media;

/// <summary>`08-media-asset-design.md` §2.1 — ràng buộc DB cho <see cref="MediaAsset"/>. Bảng
/// `media_asset` (số ít snake_case theo quy ước DB, REFACTOR-DB-001 — `08` viết `media_assets`).</summary>
public sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable("media_asset", t =>
        {
            // [1] Bản Library chưa crop; mọi record khác đã crop theo đúng một preset (#69).
            t.HasCheckConstraint(
                "ck_media_library_preset",
                "(is_in_library = true AND preset IS NULL) OR (is_in_library = false AND preset IS NOT NULL)");

            // [2] Kind thuộc tập đóng, khớp IsInLibrary; bản gốc (Library/Direct) không có nguồn
            // (REFACTOR-DB-001). Clone/Derivative KHÔNG bắt buộc source_asset_id NOT NULL — FK là
            // ON DELETE SET NULL (#71).
            t.HasCheckConstraint(
                "ck_media_asset_kind",
                "kind IN ('Library', 'Direct', 'Clone', 'Derivative') AND (kind = 'Library') = is_in_library " +
                "AND (kind NOT IN ('Library', 'Direct') OR source_asset_id IS NULL)");
        });

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(m => m.StorageKey).HasMaxLength(300).IsRequired();
        builder.Property(m => m.MimeType).HasMaxLength(80).IsRequired();
        builder.Property(m => m.AltText).HasMaxLength(200);
        builder.Property(m => m.OriginalFileName).HasMaxLength(200);
        builder.Property(m => m.Folder).HasMaxLength(100);
        builder.Property(m => m.Preset).HasMaxLength(40);

        // [4] Đích của FK ghép (Quyết định #76): Shop(LogoId, Id) → MediaAsset(Id, ShopId) — khai
        // ở ShopLogoConfiguration.cs (phía Media, namespace Persistence dùng chung).
        builder.HasAlternateKey(m => new { m.Id, m.ShopId });

        // [3] StorageKey duy nhất toàn hệ thống (Quyết định #75 — file bất biến).
        builder.HasIndex(m => m.StorageKey).IsUnique();

        // [5] Picker — query nóng nhất.
        builder.HasIndex(m => new { m.ShopId, m.CreatedAt })
            .HasDatabaseName("ix_media_asset_library")
            .IsDescending(false, true)
            .HasFilter("is_in_library AND NOT is_deleted");

        // [6] Resolve phái sinh của ảnh nghiệp vụ theo (bản Library, preset) (Quyết định #73) — ĐÚNG MỘT
        // phái sinh còn sống cho mỗi cặp (REFACTOR-DB-001). Clone không bị ràng buộc này: nhiều slot
        // có thể clone cùng một bản Library với cùng preset nhưng focal point khác.
        builder.HasIndex(m => new { m.SourceAssetId, m.Preset })
            .HasDatabaseName("ux_media_asset_derivative")
            .IsUnique()
            .HasFilter("kind = 'Derivative' AND NOT is_deleted");

        // [7] Mọi clone/phái sinh của một bản Library (references, dọn rác #72).
        builder.HasIndex(m => m.SourceAssetId)
            .HasDatabaseName("ix_media_asset_source")
            .HasFilter("source_asset_id IS NOT NULL");

        // FK ShopId → Shop — Media.dependsOn = ["Shop"], hợp lệ khai từ phía Media.
        builder.HasOne<ShopEntity>()
            .WithMany()
            .HasForeignKey(m => m.ShopId)
            .HasConstraintName("fk_media_asset_shop_shop_id")
            .OnDelete(DeleteBehavior.Restrict);

        // FK SourceAssetId → MediaAsset — self-reference, không phải quan hệ sống (#71).
        builder.HasOne<MediaAsset>()
            .WithMany()
            .HasForeignKey(m => m.SourceAssetId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
