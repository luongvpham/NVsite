using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

/// <summary>Bản trích tối thiểu — xem ghi chú ở <see cref="Identity.Domain.Shop"/>.</summary>
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
