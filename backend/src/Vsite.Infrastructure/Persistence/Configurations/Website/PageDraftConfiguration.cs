using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vsite.Domain.Website.Entities;

namespace Vsite.Infrastructure.Persistence.Configurations.Website;

/// <summary>
/// `05` §5 — bảng <c>page_draft</c>, PK = <c>page_id</c> (1:1). FK ghép <c>(page_id, shop_id) → page (id, shop_id)</c>.
/// <c>Version</c> (uint) là cột hệ thống <c>xmin</c> làm concurrency token — Npgsql map <c>IsRowVersion()</c> trên
/// property <c>uint</c> thành <c>xmin</c>/<c>xid</c> (không tạo cột thật, không cần migration thêm cột).
/// </summary>
public sealed class PageDraftConfiguration : IEntityTypeConfiguration<PageDraft>
{
    public void Configure(EntityTypeBuilder<PageDraft> builder)
    {
        builder.ToTable("page_draft");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("page_id").ValueGeneratedNever();
        builder.Ignore(d => d.PageId);

        builder.Property(d => d.Tree).HasColumnType("jsonb").IsRequired();
        builder.Property(d => d.Version).IsRowVersion();

        builder.HasOne<Page>()
            .WithMany()
            .HasForeignKey(d => new { d.Id, d.ShopId })
            .HasPrincipalKey(p => new { p.Id, p.ShopId })
            .HasConstraintName("fk_page_draft_page")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
