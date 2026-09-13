using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.IntegrationTests;

/// <summary>
/// Phase 0 (hạ tầng, xem plan Bước 3) — chứng minh pipeline Postgres thật hoạt động TRƯỚC khi viết
/// entity ở Phase 1: kết nối được, migrate được. Không test nghiệp vụ gì ở đây.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class InfrastructurePipelineTests(PostgresFixture postgres)
{
    [Fact]
    public async Task DbContext_connects_to_real_postgres_and_can_migrate()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(postgres.ConnectionString)
            .Options;

        await using var db = new IdentityDbContext(options, new TestTenantContext());

        await db.Database.MigrateAsync();

        var canConnect = await db.Database.CanConnectAsync();
        Assert.True(canConnect);
    }
}
