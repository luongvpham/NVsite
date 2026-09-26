using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vsite.Domain.Media.Entities;
using ShopEntity = Vsite.Domain.Shop.Entities.Shop;

namespace Vsite.Infrastructure.Persistence.Configurations.Media;

/// <summary>`08-media-asset-design.md` §2.1 — ràng buộc DB cho <see cref="MediaAsset"/>. Tên bảng
/// `MediaAsset` (lệch có chủ đích so với `media_assets` của `08`, xem
/// `Docs/tasks/MEDIA-001/changelog.md`), theo đúng quy ước bảng hiện có (`Shop`, `UserShop`).</summary>
public sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable("MediaAsset", t =>
        {
            // [1] Bản Library chưa crop; mọi record khác đã crop theo đúng một preset (#69).
            t.HasCheckConstraint(
                "ck_media_library_preset",
                "(\"IsInLibrary\" = true AND \"Preset\" IS NULL) OR (\"IsInLibrary\" = false AND \"Preset\" IS NOT NULL)");
        });

        builder.HasKey(m => m.Id);

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
            .HasDatabaseName("ix_media_library")
            .IsDescending(false, true)
            .HasFilter("\"IsInLibrary\" AND NOT \"IsDeleted\"");

        // [6] Resolve phái sinh của ảnh nghiệp vụ theo (bản Library, preset) (Quyết định #73).
        builder.HasIndex(m => new { m.SourceAssetId, m.Preset })
            .HasDatabaseName("ix_media_derivative")
            .HasFilter("\"SourceAssetId\" IS NOT NULL");

        // FK ShopId → Shop — Media.dependsOn = ["Shop"], hợp lệ khai từ phía Media.
        builder.HasOne<ShopEntity>()
            .WithMany()
            .HasForeignKey(m => m.ShopId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK SourceAssetId → MediaAsset — self-reference, không phải quan hệ sống (#71).
        builder.HasOne<MediaAsset>()
            .WithMany()
            .HasForeignKey(m => m.SourceAssetId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
