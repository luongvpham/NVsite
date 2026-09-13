using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

/// <summary>03 §3.5, §4.</summary>
public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Role");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Scope).HasConversion<string>().IsRequired();

        // Composite FK target (RoleId, RoleScope) → (Id, Scope) — 03 §4 ràng buộc [4].
        builder.HasAlternateKey(r => new { r.Id, r.Scope });

        builder.HasIndex(r => new { r.Code, r.Scope }).IsUnique();
    }
}
