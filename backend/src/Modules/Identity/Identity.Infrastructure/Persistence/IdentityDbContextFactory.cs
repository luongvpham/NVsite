using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Shared.Domain.Abstractions;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Cho phép `dotnet ef migrations add` chạy trực tiếp trong project này (không cần host đủ DI của
/// Api). Connection string ở đây CHỈ dùng lúc design-time (sinh migration), không dùng lúc runtime —
/// runtime lấy qua <see cref="DependencyInjection.AddIdentityInfrastructure"/> từ appsettings.
/// </summary>
public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    /// <summary>Query filter không được đánh giá lúc sinh schema/migration nên ShopId=null là đủ.</summary>
    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public TenantAudienceKind AudienceKind => TenantAudienceKind.Main;
        public Guid? ShopId => null;
    }

    public IdentityDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=vsite_dev;Username=vsite;Password=vsite_dev_only");
        return new IdentityDbContext(optionsBuilder.Options, new DesignTimeTenantContext());
    }
}
