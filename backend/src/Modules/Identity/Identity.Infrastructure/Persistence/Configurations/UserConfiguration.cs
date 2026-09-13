using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

/// <summary>03 §3.1, §4.</summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("User", t =>
        {
            // [2] Có email <=> đã verify. Không tồn tại email chưa verify trong bảng User.
            t.HasCheckConstraint(
                "ck_user_email_verified",
                "(\"Email\" IS NULL AND \"EmailVerifiedAt\" IS NULL) OR (\"Email\" IS NOT NULL AND \"EmailVerifiedAt\" IS NOT NULL)");

            // [3] Nhánh Email của nguyên tắc "phải có email hoặc Zalo".
            t.HasCheckConstraint(
                "ck_user_primary_identity",
                "\"PrimaryIdentityKind\" <> 'Email' OR \"Email\" IS NOT NULL");
        });

        builder.HasKey(u => u.Id);

        builder.Property(u => u.PrimaryIdentityKind).HasConversion<string>().IsRequired();
        builder.Property(u => u.Status).HasConversion<string>().IsRequired();

        // [1] Email là identity key toàn cục, nhưng chỉ khi có.
        builder.HasIndex(u => u.EmailNormalized)
            .IsUnique()
            .HasFilter("\"EmailNormalized\" IS NOT NULL");

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
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.UserShops)
            .WithOne(us => us.User)
            .HasForeignKey(us => us.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
