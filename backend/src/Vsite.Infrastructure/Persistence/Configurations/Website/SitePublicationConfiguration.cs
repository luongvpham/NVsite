using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vsite.Domain.Identity.Entities;
using Vsite.Domain.Website.Entities;
using WebsiteEntity = Vsite.Domain.Website.Entities.Website;

namespace Vsite.Infrastructure.Persistence.Configurations.Website;

/// <summary>
/// `05` §7 (8b, WEBSITE-004) — bảng <c>site_publication</c>: bản xuất bản bất biến của toàn site.
/// UNIQUE <c>(website_id, version)</c> (không tái dùng số version); alternate key <c>(id, website_id)</c>
/// là đích FK ghép của <c>website.current_publication_id</c> (#92); FK ghép
/// <c>(website_id, shop_id) → website (id, shop_id)</c> chặn trỏ sang shop khác (R4); CHECK <c>version &gt; 0</c>.
/// <c>published_by_user_id</c> FK <c>app_user</c> <c>Restrict</c> (ghim tên bằng <c>HasConstraintName</c>).
/// </summary>
public sealed class SitePublicationConfiguration : IEntityTypeConfiguration<SitePublication>
{
    public void Configure(EntityTypeBuilder<SitePublication> builder)
    {
        builder.ToTable("site_publication", t =>
        {
            t.HasCheckConstraint("ck_site_publication_version_positive", "version > 0");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Version).IsRequired();
        builder.Property(p => p.Snapshot).HasColumnType("jsonb").IsRequired();
        builder.Property(p => p.Note).HasMaxLength(SitePublication.MaxNoteLength);
        builder.Property(p => p.PublishedAt).IsRequired();

        // Không tái dùng số version trong một website.
        builder.HasIndex(p => new { p.WebsiteId, p.Version })
            .IsUnique()
            .HasDatabaseName("ux_site_publication_website_version");

        // Đích FK ghép của website.current_publication_id (#92).
        builder.HasAlternateKey(p => new { p.Id, p.WebsiteId }).HasName("ak_site_publication_id_website_id");

        // FK ghép (#92) — bản luôn thuộc đúng (website, shop).
        builder.HasOne<WebsiteEntity>()
            .WithMany()
            .HasForeignKey(p => new { p.WebsiteId, p.ShopId })
            .HasPrincipalKey(w => new { w.Id, w.ShopId })
            .HasConstraintName("fk_site_publication_website")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(p => p.PublishedByUserId)
            .HasConstraintName("fk_site_publication_app_user_published_by_user_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
