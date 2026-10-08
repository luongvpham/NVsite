using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Domain.Shop.Enums;
using Vsite.Infrastructure.Persistence;
using ShopEntity = Vsite.Domain.Shop.Entities.Shop;

namespace Vsite.IntegrationTests;

/// <summary>REFACTOR-BE-001 — `CreatedByUserId`/`UpdatedByUserId` trước đây luôn NULL vì
/// `AppDbContext.StampAuditFields` không ghi. EF InMemory, không cần Docker.</summary>
public sealed class AuditStampingTests
{
    [Fact]
    public async Task Insert_update_and_soft_delete_stamp_the_acting_user()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var creator = Guid.NewGuid();
        var editor = Guid.NewGuid();
        var shop = new ShopEntity("Shop", $"shop-{Guid.NewGuid():N}", ShopKind.Hosted);

        await using (var db = CreateDb(dbName, creator))
        {
            db.Shops.Add(shop);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDb(dbName, editor))
        {
            var loaded = await db.Shops.SingleAsync(s => s.Id == shop.Id);
            Assert.Equal(creator, loaded.CreatedByUserId);
            Assert.Null(loaded.UpdatedByUserId);

            loaded.Update("Renamed", loaded.Slug, loaded.Kind, loaded.ExternalUrl, loaded.Status);
            await db.SaveChangesAsync();
            Assert.Equal(editor, loaded.UpdatedByUserId);
            Assert.Equal(creator, loaded.CreatedByUserId);

            db.Shops.Remove(loaded);
            await db.SaveChangesAsync();
            Assert.True(loaded.IsDeleted);
            Assert.Equal(editor, loaded.UpdatedByUserId);
        }
    }

    [Fact]
    public async Task Anonymous_actor_leaves_audit_user_null_without_throwing()
    {
        await using var db = CreateDb(Guid.NewGuid().ToString("N"), actor: null);
        var shop = new ShopEntity("Shop", $"shop-{Guid.NewGuid():N}", ShopKind.Hosted);
        db.Shops.Add(shop);

        await db.SaveChangesAsync();

        Assert.Null(shop.CreatedByUserId);
    }

    private static AppDbContext CreateDb(string name, Guid? actor) => new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options,
        new TestTenantContext(),
        publisher: null,
        auditActor: new FakeActor(actor));

    private sealed class FakeActor(Guid? userId) : IAuditActor
    {
        public Guid? UserId => userId;
    }
}
