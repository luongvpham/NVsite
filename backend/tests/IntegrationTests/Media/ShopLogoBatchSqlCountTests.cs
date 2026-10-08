using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vsite.Domain.Media.Entities;
using Vsite.Domain.Shop.Entities;
using Vsite.Domain.Shop.Enums;
using Vsite.Infrastructure.Media;
using Vsite.Infrastructure.Persistence;

namespace Vsite.IntegrationTests.Media;

/// <summary>
/// MEDIA-001 D4 (#88) — <c>ShopLogoReader.GetLogoUrlsAsync</c> (GET /shops) phải là ĐÚNG MỘT lệnh SQL cho
/// mọi shop, không N+1. Cùng cách đo <c>MediaReferenceValidatorSqlCountTests</c>: Postgres thật +
/// <see cref="DbCommandInterceptor"/>. CẦN Docker (xem `Docs/DOCKER-TEST-DEBT.md`).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ShopLogoBatchSqlCountTests(PostgresFixture postgres)
{
    [Fact]
    public async Task GetLogoUrlsAsync_issues_exactly_one_sql_command_for_many_shops_and_returns_correct_urls()
    {
        var noLogo = Guid.NewGuid();
        var softDeleted = Guid.NewGuid();
        var normal = Guid.NewGuid();
        string softKey, normalKey;

        await using (var setup = CreateContext(counter: null))
        {
            await setup.Database.MigrateAsync();

            foreach (var id in new[] { noLogo, softDeleted, normal })
            {
                setup.Shops.Add(new Shop(id, "S", $"s-{id:N}", ShopKind.Hosted));
            }

            await setup.SaveChangesAsync();
        }

        await using (var setup = CreateContext(counter: null))
        {
            var (softSource, softHeader) = NewLogo(softDeleted);
            var (normalSource, normalHeader) = NewLogo(normal);
            setup.MediaAssets.AddRange(softSource, softHeader, normalSource, normalHeader);
            await setup.SaveChangesAsync();

            (await setup.Shops.FirstAsync(s => s.Id == softDeleted)).SetLogo(softSource.Id);
            (await setup.Shops.FirstAsync(s => s.Id == normal)).SetLogo(normalSource.Id);
            await setup.SaveChangesAsync();

            softSource.SoftDeleteFromLibrary();
            await setup.SaveChangesAsync();

            softKey = softHeader.StorageKey;
            normalKey = normalHeader.StorageKey;
        }

        var counter = new CommandCountInterceptor();
        await using var db = CreateContext(counter);
        var reader = new ShopLogoReader(db);

        counter.Reset();
        var result = await reader.GetLogoUrlsAsync([noLogo, softDeleted, normal], CancellationToken.None);

        Assert.Equal(1, counter.CommandCount);
        Assert.Equal(2, result.Count);
        Assert.False(result.ContainsKey(noLogo));
        Assert.Equal("/media/" + softKey, result[softDeleted]);
        Assert.Equal("/media/" + normalKey, result[normal]);
    }

    private static (MediaAsset Source, MediaAsset Header) NewLogo(Guid shopId)
    {
        var source = MediaAsset.NewLibrary(
            shopId, $"shops/{shopId}/library/{Guid.NewGuid():N}.webp", 1200, 800, 12345, 0.5f, 0.5f, null, null);
        var header = MediaAsset.NewDerivative(
            source, $"shops/{shopId}/logo/{Guid.NewGuid():N}.webp", 320, 96, 500, "320x96,inside", 0.5f, 0.5f);
        return (source, header);
    }

    private AppDbContext CreateContext(CommandCountInterceptor? counter)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(postgres.ConnectionString);
        if (counter is not null)
        {
            builder.AddInterceptors(counter);
        }

        return new AppDbContext(builder.Options, new TestTenantContext(Guid.NewGuid()));
    }

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
