using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vsite.Domain.Media.Entities;
using Vsite.Domain.Shop.Entities;
using Vsite.Domain.Shop.Enums;
using Vsite.Infrastructure.Media;
using Vsite.Infrastructure.Persistence;

namespace Vsite.IntegrationTests.Media;

/// <summary>
/// T8, MEDIA-001 (#77, `08` §3.5) — "đúng 1 lệnh SQL cho cả hai danh sách". EF Core InMemory không
/// phát SQL nên phần còn lại của bộ test (<c>MediaReferenceValidatorTests</c>) không thể kiểm việc
/// này; đòi hỏi provider quan hệ thật → Postgres/Testcontainers (xem `Docs/DOCKER-TEST-DEBT.md`).
///
/// Chạy: <c>dotnet test backend/tests/IntegrationTests --filter "FullyQualifiedName~MediaReferenceValidatorSqlCountTests"</c>
/// (cần Docker daemon).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class MediaReferenceValidatorSqlCountTests(PostgresFixture postgres)
{
    [Fact]
    public async Task EnsureValidAsync_issues_exactly_one_sql_command_for_both_lists()
    {
        var shopId = Guid.NewGuid();
        var counter = new CommandCountInterceptor();

        await using (var setupDb = CreateContext(shopId, counter: null))
        {
            await setupDb.Database.MigrateAsync();

            var shop = new Shop(shopId, "Test Shop", $"test-shop-{shopId:N}", ShopKind.Hosted);
            setupDb.Shops.Add(shop);
            await setupDb.SaveChangesAsync();
        }

        Guid libraryId, cloneId;
        await using (var setupDb = CreateContext(shopId, counter: null))
        {
            var library = MediaAsset.NewLibrary(
                shopId, $"shops/{shopId}/library/{Guid.NewGuid():N}.webp", 1200, 800, 12345, 0.5f, 0.5f, null, null);
            setupDb.MediaAssets.Add(library);
            await setupDb.SaveChangesAsync();

            var clone = MediaAsset.NewClone(
                library, $"shops/{shopId}/clone/{Guid.NewGuid():N}.webp", 320, 96, 500, "320x96,inside", 0.5f, 0.5f);
            setupDb.MediaAssets.Add(clone);
            await setupDb.SaveChangesAsync();

            libraryId = library.Id;
            cloneId = clone.Id;
        }

        await using var db = CreateContext(shopId, counter);
        var tenantContext = new TestTenantContext(shopId);
        var validator = new MediaReferenceValidator(db, tenantContext);

        counter.Reset();
        await validator.EnsureValidAsync(
            treeImageIds: [cloneId, cloneId],
            businessImageIds: [libraryId],
            CancellationToken.None);

        Assert.Equal(1, counter.CommandCount);
    }

    private AppDbContext CreateContext(Guid shopId, CommandCountInterceptor? counter)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(postgres.ConnectionString);
        if (counter is not null)
        {
            builder.AddInterceptors(counter);
        }

        return new AppDbContext(builder.Options, new TestTenantContext(shopId));
    }

    /// <summary>Đếm số lệnh SQL thật gửi xuống Postgres (không đếm lệnh của phần setup — interceptor
    /// chỉ gắn vào <see cref="AppDbContext"/> dùng để gọi <c>EnsureValidAsync</c>, và được
    /// <see cref="Reset"/> ngay trước lệnh gọi cần đo).</summary>
    private sealed class CommandCountInterceptor : DbCommandInterceptor
    {
        public int CommandCount { get; private set; }

        public void Reset() => CommandCount = 0;

        public override InterceptionResult<System.Data.Common.DbDataReader> ReaderExecuting(
            System.Data.Common.DbCommand command, CommandEventData eventData, InterceptionResult<System.Data.Common.DbDataReader> result)
        {
            CommandCount++;
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, CommandEventData eventData, InterceptionResult<System.Data.Common.DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            CommandCount++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
