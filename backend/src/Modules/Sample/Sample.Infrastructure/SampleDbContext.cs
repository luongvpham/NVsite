using Microsoft.EntityFrameworkCore;
using DomainSample = Sample.Domain.Sample;

namespace Sample.Infrastructure;

/// <summary>EF Core InMemory — throwaway, KHÔNG migration, KHÔNG database thật (Bước 1).</summary>
public sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options)
{
    public DbSet<DomainSample> Samples => Set<DomainSample>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DomainSample>(builder =>
        {
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
            builder.Property(s => s.Status).HasConversion<string>();
        });
    }
}
