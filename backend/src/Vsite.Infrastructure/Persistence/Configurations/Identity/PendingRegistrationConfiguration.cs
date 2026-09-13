using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vsite.Domain.Identity.Entities;
using Vsite.Domain.Identity.Enums;

namespace Vsite.Infrastructure.Persistence.Configurations.Identity;

/// <summary>03 §5.</summary>
public sealed class PendingRegistrationConfiguration : IEntityTypeConfiguration<PendingRegistration>
{
    public void Configure(EntityTypeBuilder<PendingRegistration> builder)
    {
        builder.ToTable("PendingRegistration");
        builder.HasKey(p => p.Id);

        builder.HasIndex(p => p.EmailNormalized);
        builder.HasIndex(p => p.ExpiresAt);
    }
}
