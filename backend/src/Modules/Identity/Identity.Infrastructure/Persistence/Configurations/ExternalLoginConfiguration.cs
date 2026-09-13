using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

/// <summary>03 §3.2, §4 ràng buộc [6].</summary>
public sealed class ExternalLoginConfiguration : IEntityTypeConfiguration<ExternalLogin>
{
    public void Configure(EntityTypeBuilder<ExternalLogin> builder)
    {
        builder.ToTable("ExternalLogin");
        builder.HasKey(el => el.Id);

        builder.HasIndex(el => new { el.Provider, el.ProviderUserId }).IsUnique();
    }
}
