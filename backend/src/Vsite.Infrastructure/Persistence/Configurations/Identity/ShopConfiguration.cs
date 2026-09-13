using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vsite.Domain.Identity.Entities;
using Vsite.Domain.Identity.Enums;

namespace Vsite.Infrastructure.Persistence.Configurations.Identity;

/// <summary>Bản trích tối thiểu — xem ghi chú ở <see cref="Vsite.Domain.Identity.Shop"/>.</summary>
public sealed class ShopConfiguration : IEntityTypeConfiguration<Shop>
{
    public void Configure(EntityTypeBuilder<Shop> builder)
    {
        builder.ToTable("Shop");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Kind).HasConversion<string>().IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().IsRequired();

        builder.HasIndex(s => s.Slug).IsUnique();
    }
}
