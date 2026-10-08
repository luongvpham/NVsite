using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vsite.Domain.Identity.Entities;
using Vsite.Domain.Identity.Enums;

namespace Vsite.Infrastructure.Persistence.Configurations.Identity;

/// <summary>03 §3.1, §4.</summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // `app_user`, không phải `user`: `user` là từ khoá Postgres — `SELECT * FROM user` không lỗi mà
        // trả về current_user, bẫy cho mọi SQL viết tay (REFACTOR-DB-001).
        builder.ToTable("app_user", t =>
        {
            // [2] Có email <=> đã verify. Không tồn tại email chưa verify trong bảng User.
            t.HasCheckConstraint(
                "ck_user_email_verified",
                "(email IS NULL AND email_verified_at IS NULL) OR (email IS NOT NULL AND email_verified_at IS NOT NULL)");

            // [3] Nhánh Email của nguyên tắc "phải có email hoặc Zalo".
            t.HasCheckConstraint(
                "ck_user_primary_identity",
                "primary_identity_kind <> 'Email' OR email IS NOT NULL");
        });

        builder.HasKey(u => u.Id);

        builder.Property(u => u.PrimaryIdentityKind).HasConversion<string>().IsRequired();
        builder.Property(u => u.Status).HasConversion<string>().IsRequired();

        // [1] Email là identity key toàn cục, nhưng chỉ khi có.
        builder.HasIndex(u => u.EmailNormalized)
            .IsUnique()
            .HasFilter("email_normalized IS NOT NULL");

        builder.Property(u => u.RoleScope)
            .HasConversion<string>()
            .HasComputedColumnSql("'Platform'", stored: true);

        // [4] Role scope phải khớp chỗ dùng — composite FK, không chỉ FK thường.
        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(u => new { u.RoleId, u.RoleScope })
            .HasPrincipalKey(r => new { r.Id, r.Scope })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.ExternalLogins)
            .WithOne(el => el.User)
            .HasForeignKey(el => el.UserId)
            .HasConstraintName("fk_external_login_app_user_user_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.UserShops)
            .WithOne(us => us.User)
            .HasForeignKey(us => us.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
